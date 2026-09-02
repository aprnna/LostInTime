# tools/xai/counterfactual.py
"""Multi-feature Counterfactual Explainer and Feature Sweep for DDA DDQN.

Allows researchers to:
1. Perform multi-feature 'what-if' counterfactual perturbations (e.g., HP + QTE + Turns).
2. Plot and save visual comparison bar charts (Q-values before vs after).
3. Perform 1D feature sweeps to find decision boundary tipping points.
4. Use standard preset player archetypes (pro, desperate, casual).
"""

import argparse
import os
import json
import numpy as np
import matplotlib.pyplot as plt
from . import constants as C
from . import shap_net as S


PRESETS = {
    "desperate": {
        "HP Ratio": 0.20,
        "Turn Count": 0.80,
        "Dmg Dealt Ratio": 0.30,
        "QTE Accuracy": 0.10,
        "Resource Depletion": 0.85,
    },
    "pro": {
        "HP Ratio": 0.95,
        "Turn Count": 0.10,
        "Dmg Dealt Ratio": 0.95,
        "QTE Accuracy": 1.00,
        "Resource Depletion": 0.05,
    },
    "struggling": {
        "HP Ratio": 0.30,
        "QTE Accuracy": 0.00,
    },
    "balanced": {
        "HP Ratio": 0.50,
        "Turn Count": 0.40,
        "QTE Accuracy": 0.60,
        "Resource Depletion": 0.40,
    }
}


def run_counterfactual(state, onnx_path, perturbations, decision_idx=0, out_dir="results/shap"):
    """Run a multi-feature counterfactual experiment on a single state vector.

    state: [6] or [1, 6] float32 array
    perturbations: dict mapping feature name (or index) to new value, e.g. {"HP Ratio": 0.30, "QTE Accuracy": 0.0}
    returns: dict with before/after states, Q-values, action names, and plot path.
    """
    os.makedirs(out_dir, exist_ok=True)
    state = np.asarray(state, dtype=np.float32).reshape(1, C.OBS_SIZE)
    cf_state = state.copy()

    for k, v in perturbations.items():
        if isinstance(k, str) and k in C.FEATURE_NAMES:
            idx = C.FEATURE_NAMES.index(k)
        elif isinstance(k, int) and 0 <= k < C.OBS_SIZE:
            idx = k
        else:
            continue
        cf_state[0, idx] = float(v)

    # Inference
    q_before = S.onnx_inference(onnx_path, state)[0]
    q_after = S.onnx_inference(onnx_path, cf_state)[0]
    diffs = q_after - q_before

    act_before_idx = int(np.argmax(q_before))
    act_after_idx = int(np.argmax(q_after))
    act_before_name = C.ACTION_INT_TO_NAME[act_before_idx]
    act_after_name = C.ACTION_INT_TO_NAME[act_after_idx]

    # Generate Bar Chart
    act_names = list(C.ACTION_NAME_TO_INT.keys())
    x = np.arange(len(act_names))
    width = 0.35

    fig, ax = plt.subplots(figsize=(10, 5.5))
    r1 = ax.bar(x - width/2, q_before, width, label='Sebelum Intervensi', color='#3498db', alpha=0.85)
    r2 = ax.bar(x + width/2, q_after, width, label='Setelah Intervensi (Counterfactual)', color='#e74c3c', alpha=0.85)

    ax.set_ylabel('Nilai Q (Preferensi Agen)')
    changes_str = ", ".join([f"{k}: {float(state[0, C.FEATURE_NAMES.index(k) if isinstance(k, str) else k]):.2f} -> {v:.2f}" 
                             for k, v in perturbations.items()])
    ax.set_title(f'Eksperimen Counterfactual (Decision {decision_idx})\nPerubahan: [{changes_str}]\nPilihan Agen: {act_before_name} -> {act_after_name}', fontsize=11)
    ax.set_xticks(x)
    ax.set_xticklabels(act_names)
    ax.legend(loc='upper right')
    ax.grid(axis='y', linestyle='--', alpha=0.6)

    for r in r1:
        h = r.get_height()
        ax.annotate(f'{h:.2f}', xy=(r.get_x() + r.get_width()/2, h), xytext=(0, 3 if h >= 0 else -10),
                    textcoords='offset points', ha='center', va='bottom', fontsize=9)
    for r in r2:
        h = r.get_height()
        ax.annotate(f'{h:.2f}', xy=(r.get_x() + r.get_width()/2, h), xytext=(0, 3 if h >= 0 else -10),
                    textcoords='offset points', ha='center', va='bottom', fontsize=9)

    plt.tight_layout()
    plot_name = f"counterfactual_decision{decision_idx}.png"
    plot_path = os.path.join(out_dir, plot_name)
    plt.savefig(plot_path, dpi=150)
    plt.close()

    return {
        "state_before": state[0],
        "state_after": cf_state[0],
        "q_before": q_before,
        "q_after": q_after,
        "diffs": diffs,
        "action_before": act_before_name,
        "action_after": act_after_name,
        "plot_path": plot_path
    }


def sweep_feature(state, onnx_path, feature_name, values=None, decision_idx=0, out_dir="results/shap"):
    """Perform a 1D sweep across a feature's values to find decision tipping points."""
    os.makedirs(out_dir, exist_ok=True)
    if values is None:
        values = np.linspace(0.0, 1.0, 21)

    f_idx = C.FEATURE_NAMES.index(feature_name) if isinstance(feature_name, str) else feature_name
    f_name = C.FEATURE_NAMES[f_idx]

    state = np.asarray(state, dtype=np.float32).reshape(1, C.OBS_SIZE)
    q_history = []
    actions_chosen = []

    for v in values:
        s = state.copy()
        s[0, f_idx] = float(v)
        q = S.onnx_inference(onnx_path, s)[0]
        q_history.append(q)
        actions_chosen.append(C.ACTION_INT_TO_NAME[int(np.argmax(q))])

    q_history = np.array(q_history) # [len(values), 5]

    fig, ax = plt.subplots(figsize=(10, 5.5))
    colors = ['#27ae60', '#2ecc71', '#f39c12', '#e67e22', '#e74c3c']
    for a_idx, a_name in enumerate(C.ACTION_NAME_TO_INT.keys()):
        ax.plot(values, q_history[:, a_idx], label=a_name, color=colors[a_idx], linewidth=2.2)

    ax.set_xlabel(f'Nilai {f_name} (Variasi 0.0 s/d 1.0)')
    ax.set_ylabel('Nilai Q')
    ax.set_title(f'1D Feature Sweep: {f_name} (Decision {decision_idx})\nTitik Balik Keputusan Agen', fontsize=12)
    ax.legend()
    ax.grid(True, linestyle='--', alpha=0.6)

    plt.tight_layout()
    plot_name = f"sweep_{f_name.replace(' ', '_').lower()}_decision{decision_idx}.png"
    plot_path = os.path.join(out_dir, plot_name)
    plt.savefig(plot_path, dpi=150)
    plt.close()

    return {
        "values": values,
        "q_history": q_history,
        "actions_chosen": actions_chosen,
        "plot_path": plot_path
    }


def main():
    ap = argparse.ArgumentParser(description="Multi-Feature Counterfactual Analysis for DDA")
    ap.add_argument("--states", default="xai/states.npy")
    ap.add_argument("--meta", default="xai/meta.json")
    ap.add_argument("--model", default=None, help="Path or name of ONNX model")
    ap.add_argument("--decision", type=int, default=0, help="Decision index to explain (0-86)")
    ap.add_argument("--out-dir", default=None)

    # Feature perturbation arguments
    ap.add_argument("--hp", type=float, default=None, help="Set new HP Ratio [0.0 - 1.0]")
    ap.add_argument("--turn", type=float, default=None, help="Set new Turn Count norm [0.0 - 1.0]")
    ap.add_argument("--level", type=float, default=None, help="Set new Player Level norm [0.0 - 1.0]")
    ap.add_argument("--dmg", type=float, default=None, help="Set new Dmg Dealt Ratio [0.0 - 1.0]")
    ap.add_argument("--qte", type=float, default=None, help="Set new QTE Accuracy [0.0 - 1.0]")
    ap.add_argument("--res", type=float, default=None, help="Set new Resource Depletion [0.0 - 1.0]")

    ap.add_argument("--preset", choices=list(PRESETS.keys()), default=None, help="Use a preset archetype")
    ap.add_argument("--sweep", choices=["hp", "qte", "turn", "dmg", "res"], default=None, help="Run 1D sweep on a feature")
    args = ap.parse_args()

    states = np.load(args.states).astype(np.float32)
    with open(args.meta, encoding="utf-8") as f:
        meta = json.load(f)

    onnx_path = args.model or meta.get("beta_model")
    if onnx_path and not os.path.exists(onnx_path):
        for candidate_dir in ["..", "../Assets/Resources/DDA/Models", "Assets/Resources/DDA/Models"]:
            p = os.path.join(candidate_dir, os.path.basename(onnx_path))
            if os.path.exists(p):
                onnx_path = p
                break

    model_stem = os.path.splitext(os.path.basename(onnx_path))[0]
    out_dir = args.out_dir or f"results/shap/{model_stem}"

    state = states[args.decision]

    if args.sweep:
        sweep_map = {
            "hp": "HP Ratio",
            "qte": "QTE Accuracy",
            "turn": "Turn Count",
            "dmg": "Dmg Dealt Ratio",
            "res": "Resource Depletion"
        }
        f_name = sweep_map[args.sweep]
        res = sweep_feature(state, onnx_path, f_name, decision_idx=args.decision, out_dir=out_dir)
        print(f"\n=== HASIL 1D SWEEP: {f_name} ===")
        print(f"Grafik disimpan di: {res['plot_path']}")
        print(f"Perubahan Pilihan Aksi:")
        for v, a in zip(res["values"][::4], res["actions_chosen"][::4]):
            print(f"  {f_name}={v:.2f} -> Pilihan: {a}")
        return

    perturbations = {}
    if args.preset:
        perturbations.update(PRESETS[args.preset])

    if args.hp is not None: perturbations["HP Ratio"] = args.hp
    if args.turn is not None: perturbations["Turn Count"] = args.turn
    if args.level is not None: perturbations["Player Level"] = args.level
    if args.dmg is not None: perturbations["Dmg Dealt Ratio"] = args.dmg
    if args.qte is not None: perturbations["QTE Accuracy"] = args.qte
    if args.res is not None: perturbations["Resource Depletion"] = args.res

    if not perturbations:
        # Default example: HP 0.30 + QTE 0.0 (struggling)
        perturbations = {"HP Ratio": 0.30, "QTE Accuracy": 0.00}

    res = run_counterfactual(state, onnx_path, perturbations, decision_idx=args.decision, out_dir=out_dir)

    print(f"\n========================================================")
    print(f"      HASIL EKSPERIMEN COUNTERFACTUAL (DECISION {args.decision})")
    print(f"========================================================")
    print(f"Model ONNX      : {os.path.basename(onnx_path)}")
    print(f"Pilihan Semula  : {res['action_before']} (Q-Max: {np.max(res['q_before']):.2f})")
    print(f"Pilihan Baru    : {res['action_after']} (Q-Max: {np.max(res['q_after']):.2f})")
    print(f"Grafik Hasil    : {res['plot_path']}\n")

    print(f"{'Fitur':20s} | {'Sebelum':8s} | {'Sesudah':8s} | {'Perubahan':10s}")
    print(f"-" * 55)
    for i, name in enumerate(C.FEATURE_NAMES):
        b_val = res['state_before'][i]
        a_val = res['state_after'][i]
        diff = a_val - b_val
        flag = f"({diff:+.2f})" if abs(diff) > 1e-4 else ""
        print(f"{name:20s} | {b_val:8.2f} | {a_val:8.2f} | {flag}")

    print(f"\n{'Tingkat Kesulitan':18s} | {'Q-Sebelum':10s} | {'Q-Sesudah':10s} | {'Delta Q':10s}")
    print(f"-" * 55)
    for i, name in enumerate(C.ACTION_NAME_TO_INT.keys()):
        qb = res['q_before'][i]
        qa = res['q_after'][i]
        dq = res['diffs'][i]
        star = " <-- TERPILIH" if name == res['action_after'] else ""
        print(f"{name:18s} | {qb:10.2f} | {qa:10.2f} | {dq:+10.2f}{star}")


if __name__ == "__main__":
    main()
