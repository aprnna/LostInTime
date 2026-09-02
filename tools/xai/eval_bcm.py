# tools/xai/eval_bcm.py
"""Battle Challenge Metric (BCM) / Flow State Evaluation Tool.

Evaluates how well a DDA DDQN model maintains balanced gameplay (BCM).
BCM = Percentage of battles ending with Survival Ratio in [0.40, 0.60] (Flow State).
- Subjugate: SR > 0.60 (Enemy too weak / Player dominated)
- Balanced: 0.40 <= SR <= 0.60 (Ideal challenge / BCM)
- Rebellious: SR < 0.40 (Enemy too hard / Player struggled or died)
"""

import argparse
import os
import json
import numpy as np
from . import constants as C
from . import shap_net as S


def evaluate_bcm(survival_ratios, model_path=None, states=None):
    """Calculate BCM and outcome breakdown from survival ratio array."""
    sr = np.asarray(survival_ratios, dtype=np.float32)
    total = len(sr)
    if total == 0:
        return {}

    subjugate_cnt = int(np.sum(sr > C.SR_BALANCED[1]))
    balanced_cnt = int(np.sum((sr >= C.SR_BALANCED[0]) & (sr <= C.SR_BALANCED[1])))
    rebellious_cnt = int(np.sum(sr < C.SR_BALANCED[0]))

    bcm_rate = (balanced_cnt / total) * 100
    subjugate_rate = (subjugate_cnt / total) * 100
    rebellious_rate = (rebellious_cnt / total) * 100

    return {
        "total_battles": total,
        "bcm_balanced_count": balanced_cnt,
        "bcm_balanced_percent": bcm_rate,
        "subjugate_count": subjugate_cnt,
        "subjugate_percent": subjugate_rate,
        "rebellious_count": rebellious_cnt,
        "rebellious_percent": rebellious_rate,
        "mean_survival_ratio": float(np.mean(sr)),
        "std_survival_ratio": float(np.std(sr)),
    }


def main():
    ap = argparse.ArgumentParser(description="Evaluate Battle Challenge Metric (BCM)")
    ap.add_argument("--survival", default="xai/survival.npy", help="Path to survival.npy")
    ap.add_argument("--outcomes", default="xai/outcomes.npy", help="Path to outcomes.npy")
    ap.add_argument("--meta", default="xai/meta.json", help="Path to meta.json")
    ap.add_argument("--raw-events", type=int, default=98, help="Total raw events for baseline comparison")
    args = ap.parse_args()

    if os.path.exists(args.survival):
        survival = np.load(args.survival)
    else:
        # Fallback to parent path
        survival = np.load(os.path.join("..", args.survival))

    res = evaluate_bcm(survival)

    print("\n========================================================")
    print("      HASIL EVALUASI BATTLE CHALLENGE METRIC (BCM)")
    print("========================================================")
    print(f"Total Keputusan Teranalisis : {res['total_battles']}")
    print(f"Rata-rata Survival Ratio     : {res['mean_survival_ratio'] * 100:.2f}%\n")

    print(f"{'Kategori Hasil':20s} | {'Jumlah':8s} | {'Persentase':12s} | {'Deskripsi':25s}")
    print("-" * 75)
    print(f"{'Subjugate (Terlalu Mudah)':20s} | {res['subjugate_count']:8d} | {res['subjugate_percent']:11.2f}% | Pemain menang mudah (HP > 60%)")
    print(f"{'Balanced (BCM / Ideal)':20s} | {res['bcm_balanced_count']:8d} | {res['bcm_balanced_percent']:11.2f}% | Pertempuran Seimbang (HP 40-60%)")
    print(f"{'Rebellious (Terlalu Sulit)':20s} | {res['rebellious_count']:8d} | {res['rebellious_percent']:11.2f}% | Pemain Kritis/Kalah (HP < 40%)")

    print("\n--------------------------------------------------------")
    print(f"SKOR BCM PADA DATA BETA (87 Keputusan Valid) : {res['bcm_balanced_percent']:.2f}%")
    if args.raw_events > 0:
        raw_bcm = (res['bcm_balanced_count'] / args.raw_events) * 100
        print(f"SKOR BCM PADA RAW LOGS (98 Event Sesi)       : {raw_bcm:.2f}% (Angka Laporan 15.31%)")
    print("--------------------------------------------------------\n")


if __name__ == "__main__":
    main()
