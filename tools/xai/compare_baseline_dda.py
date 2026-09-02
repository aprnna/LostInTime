# tools/xai/compare_baseline_dda.py
import os
import glob
from .benchmark_models_bcm import benchmark_model

def run_deep_comparison():
    models_dir = "../Assets/Resources/DDA/Models"
    if not os.path.exists(models_dir):
        models_dir = "Assets/Resources/DDA/Models"

    models = sorted(glob.glob(os.path.join(models_dir, "*.onnx")))

    print("=========================================================================================")
    print("           ANALISIS MENDALAM: BASELINE (TANPA DDA) VS MODEL DDQN DDA")
    print("=========================================================================================\n")

    # 1. Baseline
    res_base = benchmark_model(None, "Baseline (Tanpa DDA)", runs_per_profile=80)
    print(f"--- 1. BASELINE (TANPA DDA / STATIS) ---")
    print(f"BCM Rata-Rata: {res_base['overall_bcm']:.2f}%\n")
    print(f"{'Kategori Skill':16s} | {'Win Rate (Tamat)':18s} | {'BCM (Flow State)':18s} | {'Rebellious (Kalah/Kritis)':24s}")
    print("-" * 85)
    for p, v in res_base['profiles'].items():
        reb_pct = v['n_ur'] / v['total_battles'] * 100
        print(f"{p:16s} | {v['win_rate']:17.1f}% | {v['bcm']:17.1f}% | {reb_pct:23.1f}%")

    # 2. Each Model
    for m in models:
        name = os.path.basename(m)
        res = benchmark_model(m, name, runs_per_profile=80)
        print(f"\n--- MODEL: {name} ---")
        print(f"BCM Rata-Rata: {res['overall_bcm']:.2f}%\n")
        print(f"{'Kategori Skill':16s} | {'Win Rate (Tamat)':18s} | {'BCM (Flow State)':18s} | {'Rebellious (Kalah/Kritis)':24s}")
        print("-" * 85)
        for p, v in res['profiles'].items():
            reb_pct = v['n_ur'] / v['total_battles'] * 100
            print(f"{p:16s} | {v['win_rate']:17.1f}% | {v['bcm']:17.1f}% | {reb_pct:23.1f}%")

if __name__ == "__main__":
    run_deep_comparison()
