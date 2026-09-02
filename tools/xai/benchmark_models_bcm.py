# tools/xai/benchmark_models_bcm.py
"""Comprehensive BCM Simulation Benchmark for Multiple DDQN DDA Models.

Simulates full 12-area runs across multiple player skill archetypes:
- Low Skill (QTE: 0.2-0.4, slower turns)
- Moderate Skill (QTE: 0.5-0.7)
- High Skill / Pro (QTE: 0.85-1.0, optimal attacks)

Evaluates and compares:
1. ddqn_dda_sidang1.onnx
2. ddqn_dda_final.onnx
3. ddqn_dda5.onnx
4. ddqn_dda_retrain.onnx
5. Static Baseline (No DDA / Fixed Normal Difficulty)

Calculates:
- Overall BCM Rate (% Flow State / Balanced battles)
- BCM per Player Skill Category (Cross-Skill Consistency)
- Win Rate & Outcome Breakdown (Subjugate / Balanced / Rebellious)
- Ranking of the best-performing model.
"""

import os
import glob
import json
import random
import numpy as np
import matplotlib.pyplot as plt
from . import constants as C
from . import shap_net as S


DIFF_LEVELS = [0.75, 0.875, 1.0, 1.125, 1.25]
DIFF_NAMES = ["Very Easy", "Easy", "Normal", "Hard", "Very Hard"]


class SimEnemy:
    def __init__(self, name, max_hp, base_dmg, interval):
        self.name = name
        self.base_max_hp = max_hp
        self.base_dmg = base_dmg
        self.base_interval = interval
        self.max_hp = max_hp
        self.current_hp = max_hp
        self.base_damage = base_dmg
        self.interval_damage = interval

    def apply_difficulty(self, hp_mult, dmg_mult):
        self.max_hp = int(round(self.base_max_hp * hp_mult))
        self.current_hp = self.max_hp
        self.base_damage = int(round(self.base_dmg * dmg_mult))
        self.interval_damage = int(round(self.base_interval * dmg_mult))

    def calculate_damage(self):
        min_dmg = max(1, self.base_damage - self.interval_damage)
        max_dmg = max(2, self.base_damage + self.interval_damage)
        return random.randint(min_dmg, max_dmg)

    def take_damage(self, dmg):
        self.current_hp = max(0, self.current_hp - dmg)

    def is_alive(self):
        return self.current_hp > 0


class SimPlayer:
    def __init__(self, skill_level="moderate"):
        self.max_hp = 100
        self.current_hp = 100
        self.base_damage = 12
        self.base_defend = 5
        self.defend = 0
        self.level = 1
        self.skill_level = skill_level  # "low", "moderate", "high"

        # Persistent global action limits for 1 run
        self.max_sword = 15
        self.max_gun = 10
        self.max_defend = 2

        self.sword_uses = self.max_sword
        self.gun_uses = self.max_gun
        self.defend_uses = self.max_defend

    def reset_for_run(self):
        self.current_hp = self.max_hp
        self.defend = 0
        self.level = 1
        self.sword_uses = self.max_sword
        self.gun_uses = self.max_gun
        self.defend_uses = self.max_defend

    def is_alive(self):
        return self.current_hp > 0

    def take_damage(self, dmg):
        if dmg <= 0: return
        if self.defend > 0:
            absorbed = min(self.defend, dmg)
            self.defend -= absorbed
            rem = dmg - absorbed
            self.current_hp = max(0, self.current_hp - rem)
        else:
            self.current_hp = max(0, self.current_hp - dmg)

    def perform_attack(self):
        """Simulate action choice and QTE accuracy based on skill level."""
        # QTE success probability
        if self.skill_level == "low":
            qte_prob = random.uniform(0.20, 0.45)
        elif self.skill_level == "high":
            qte_prob = random.uniform(0.85, 1.00)
        else:
            qte_prob = random.uniform(0.50, 0.75)

        qte_success = random.random() < qte_prob
        crit_multiplier = 1.35 if qte_success else 1.0

        # Choose action
        if self.sword_uses > 0 and random.random() < (0.6 if self.skill_level == "high" else 0.4):
            self.sword_uses -= 1
            raw_dmg = self.base_damage * 1.5
            action_type = "sword"
        elif self.gun_uses > 0 and random.random() < 0.3:
            self.gun_uses -= 1
            raw_dmg = self.base_damage * 1.8
            action_type = "gun"
        else:
            raw_dmg = self.base_damage * 1.0
            action_type = "fist"

        dmg = int(round(raw_dmg * crit_multiplier))
        return dmg, qte_success, action_type


def get_default_areas():
    """Generates the 12-area sequence from design spec."""
    return [
        {"type": "enemy", "name": "Caveman", "enemies": [SimEnemy("Caveman", 15, 6, 2)]},
        {"type": "enemy", "name": "Sabertooth+Caveman", "enemies": [SimEnemy("Sabertooth", 18, 8, 3), SimEnemy("Caveman", 15, 6, 2)]},
        {"type": "rest"},
        {"type": "enemy", "name": "Sabertooth+Caveman", "enemies": [SimEnemy("Sabertooth", 18, 8, 3), SimEnemy("Caveman", 15, 6, 2)]},
        {"type": "enemy", "name": "Raptor x2", "enemies": [SimEnemy("Raptor", 24, 9, 5), SimEnemy("Raptor", 24, 9, 5)]},
        {"type": "enemy", "name": "Raptor x2", "enemies": [SimEnemy("Raptor", 24, 9, 5), SimEnemy("Raptor", 24, 9, 5)]},
        {"type": "shop"},
        {"type": "enemy", "name": "Raptor+Sabertooth+Caveman", "enemies": [SimEnemy("Raptor", 24, 9, 5), SimEnemy("Sabertooth", 18, 8, 3), SimEnemy("Caveman", 15, 6, 2)]},
        {"type": "enemy", "name": "Raptor+Sabertooth+Caveman", "enemies": [SimEnemy("Raptor", 24, 9, 5), SimEnemy("Sabertooth", 18, 8, 3), SimEnemy("Caveman", 15, 6, 2)]},
        {"type": "rest"},
        {"type": "shop"},
        {"type": "boss", "name": "Trex", "enemies": [SimEnemy("Trex", 35, 20, 5)]},
    ]


def simulate_single_run(model_path, skill_level="moderate"):
    """Simulate 1 full 12-area run and collect battle results."""
    player = SimPlayer(skill_level=skill_level)
    areas = get_default_areas()
    battle_results = []

    # Run starts at Normal difficulty (index 2)
    current_diff_idx = 2

    # State tracking
    last_hp_ratio = 1.0
    last_turn_norm = 0.0
    last_level_norm = 0.2
    last_dmg_ratio = 0.5
    last_qte_acc = 0.7
    last_res_depl = 0.0

    for area_idx, area in enumerate(areas):
        if not player.is_alive():
            break

        if area["type"] in ["rest", "shop"]:
            # Rest / Shop
            if area["type"] == "rest":
                player.current_hp = min(player.max_hp, player.current_hp + random.randint(15, 25))
            continue

        # Battle Area
        enemies = area["enemies"]
        hp_mult = DIFF_LEVELS[current_diff_idx]
        dmg_mult = DIFF_LEVELS[current_diff_idx]
        for e in enemies:
            e.apply_difficulty(hp_mult, dmg_mult)

        total_enemy_hp = sum(e.max_hp for e in enemies)
        start_hp = player.current_hp
        start_sword = player.sword_uses
        start_gun = player.gun_uses
        start_defend = player.defend_uses

        # Turn Loop
        turns = 0
        total_dmg_dealt = 0
        qte_hits = 0
        qte_total = 0

        while player.is_alive() and any(e.is_alive() for e in enemies):
            turns += 1
            # Player turn
            target = next((e for e in enemies if e.is_alive()), None)
            if target:
                dmg, qte_ok, act_type = player.perform_attack()
                target.take_damage(dmg)
                total_dmg_dealt += dmg
                qte_total += 1
                if qte_ok: qte_hits += 1

            # Enemy turns
            for e in enemies:
                if e.is_alive() and player.is_alive():
                    e_dmg = e.calculate_damage()
                    player.take_damage(e_dmg)

            if turns >= 20:  # turn cap
                break

        end_hp = player.current_hp
        sr = float(end_hp) / float(start_hp) if start_hp > 0 else 0.0
        sr = min(1.0, max(0.0, sr))
        condition = C.survival_to_outcome(sr)

        battle_results.append({
            "area_idx": area_idx,
            "difficulty_used": current_diff_idx,
            "sr": sr,
            "condition": condition,
            "player_won": player.is_alive()
        })

        # Calculate observation vector for next decision
        last_hp_ratio = min(1.0, max(0.0, float(end_hp) / float(player.max_hp)))
        last_turn_norm = min(1.0, float(turns) / 15.0)
        last_level_norm = min(1.0, float(player.level) / 5.0)
        last_dmg_ratio = min(1.0, float(total_enemy_hp) / total_dmg_dealt) if total_dmg_dealt > 0 else 0.0
        last_qte_acc = float(qte_hits) / float(qte_total) if qte_total > 0 else 0.0

        # Delta resource depletion
        sw_dep = min(1.0, max(0.0, float(start_sword - player.sword_uses) / player.max_sword))
        gn_dep = min(1.0, max(0.0, float(start_gun - player.gun_uses) / player.max_gun))
        df_dep = min(1.0, max(0.0, float(start_defend - player.defend_uses) / player.max_defend))
        last_res_depl = (sw_dep + gn_dep + df_dep) / 3.0

        # Agent decision for next battle
        if model_path:
            obs = np.array([[last_hp_ratio, last_turn_norm, last_level_norm,
                             last_dmg_ratio, last_qte_acc, last_res_depl]], dtype=np.float32)
            q_vals = S.onnx_inference(model_path, obs)[0]
            current_diff_idx = int(np.argmax(q_vals))
        else:
            # Baseline (Static Normal)
            current_diff_idx = 2

    return battle_results


def benchmark_model(model_path, model_name, runs_per_profile=50):
    """Run benchmark for a model across low, moderate, high skill profiles."""
    results_by_profile = {}
    all_sr = []

    for skill in ["low", "moderate", "high"]:
        profile_battles = []
        wins = 0
        for _ in range(runs_per_profile):
            run_res = simulate_single_run(model_path, skill_level=skill)
            profile_battles.extend(run_res)
            if run_res and run_res[-1]["player_won"]:
                wins += 1

        sr_list = [b["sr"] for b in profile_battles]
        all_sr.extend(sr_list)

        n_total = len(sr_list)
        n_b = sum(1 for s in sr_list if C.SR_BALANCED[0] <= s <= C.SR_BALANCED[1])
        n_us = sum(1 for s in sr_list if s > C.SR_BALANCED[1])
        n_ur = sum(1 for s in sr_list if s < C.SR_BALANCED[0])

        bcm = (n_b / n_total * 100) if n_total > 0 else 0.0
        results_by_profile[skill] = {
            "bcm": bcm,
            "n_b": n_b,
            "n_us": n_us,
            "n_ur": n_ur,
            "total_battles": n_total,
            "win_rate": (wins / runs_per_profile * 100)
        }

    total_all = len(all_sr)
    total_b = sum(1 for s in all_sr if C.SR_BALANCED[0] <= s <= C.SR_BALANCED[1])
    total_us = sum(1 for s in all_sr if s > C.SR_BALANCED[1])
    total_ur = sum(1 for s in all_sr if s < C.SR_BALANCED[0])
    overall_bcm = (total_b / total_all * 100) if total_all > 0 else 0.0

    return {
        "model_name": model_name,
        "overall_bcm": overall_bcm,
        "total_battles": total_all,
        "n_b": total_b,
        "n_us": total_us,
        "n_ur": total_ur,
        "profiles": results_by_profile
    }


def main():
    models_dir = "Assets/Resources/DDA/Models"
    if not os.path.exists(models_dir):
        models_dir = "../Assets/Resources/DDA/Models"

    candidate_models = sorted(glob.glob(os.path.join(models_dir, "*.onnx")))

    print("====================================================================")
    print("      BENCHMARK SIMULASI BCM: PERBANDINGAN BERBAGAI MODEL DDA")
    print("====================================================================")
    print(f"Ditemukan {len(candidate_models)} model ONNX untuk diuji.\n")

    benchmarks = []

    # 1. Test Baseline (No DDA / Static Normal)
    print("[1/N] Menjalankan Simulasi Baseline (Static Normal - Tanpa DDA)...")
    res_baseline = benchmark_model(None, "Baseline (Tanpa DDA)", runs_per_profile=60)
    benchmarks.append(res_baseline)

    # 2. Test each ONNX model
    for i, m_path in enumerate(candidate_models):
        m_name = os.path.basename(m_path)
        print(f"[{i+2}/N] Menguji Model: {m_name}...")
        try:
            res_m = benchmark_model(m_path, m_name, runs_per_profile=60)
            benchmarks.append(res_m)
        except Exception as e:
            print(f"  Error pada {m_name}: {e}")

    # Rank by Overall BCM
    benchmarks.sort(key=lambda x: x["overall_bcm"], reverse=True)

    print("\n====================================================================")
    print("                     TABEL PERINGKAT HASIL BCM")
    print("====================================================================")
    print(f"{'Peringkat':10s} | {'Nama Model':24s} | {'BCM Total':10s} | {'Low BCM':8s} | {'Mid BCM':8s} | {'Pro BCM':8s}")
    print("-" * 78)
    for rank, b in enumerate(benchmarks, start=1):
        p = b["profiles"]
        low_bcm = p["low"]["bcm"]
        mid_bcm = p["moderate"]["bcm"]
        pro_bcm = p["high"]["bcm"]
        star = " <-- TERBAIK" if rank == 1 else ""
        print(f"#{rank:<9d} | {b['model_name']:24s} | {b['overall_bcm']:8.2f}% | {low_bcm:6.1f}% | {mid_bcm:6.1f}% | {pro_bcm:6.1f}%{star}")

    # Plot Comparison Chart
    fig, ax = plt.subplots(figsize=(12, 6))
    names = [b["model_name"].replace(".onnx", "") for b in benchmarks]
    bcm_totals = [b["overall_bcm"] for b in benchmarks]
    colors = ['#2ecc71' if i == 0 else '#3498db' for i in range(len(benchmarks))]

    bars = ax.bar(names, bcm_totals, color=colors, edgecolor='black', linewidth=0.8, alpha=0.85, width=0.55)
    for bar in bars:
        h = bar.get_height()
        ax.annotate(f'{h:.2f}%', xy=(bar.get_x() + bar.get_width()/2, h), xytext=(0, 4),
                    textcoords='offset points', ha='center', va='bottom', fontsize=10, fontweight='bold')

    ax.set_ylabel('Skor BCM (Persentase Pertempuran Seimbang %)', fontsize=12)
    ax.set_title('Perbandingan Skor BCM Antar Model DDA (Simulasi 12 Area)', fontsize=14, fontweight='bold')
    ax.grid(axis='y', linestyle='--', alpha=0.6)
    plt.xticks(rotation=15, ha='right')
    plt.tight_layout()

    out_plot = "results/shap/bcm_model_benchmark_comparison.png"
    if not os.path.exists("results/shap"):
        out_plot = "../results/shap/bcm_model_benchmark_comparison.png"
    plt.savefig(out_plot, dpi=150)
    plt.close()
    print(f"\nGrafik perbandingan model berhasil disimpan di: {out_plot}")


if __name__ == "__main__":
    main()
