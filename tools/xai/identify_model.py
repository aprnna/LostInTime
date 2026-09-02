# tools/xai/identify_model.py
"""Identify the deployed beta model by argmax match rate. Spec §9.7.

For each candidate .onnx, infer Q on the 87 beta states (cross-event pairing:
actions.npy[k] is the action chosen from states.npy[k], i.e. act_{k+1} applied
to the next battle, paired with obs_k) and compare argmax to the logged
dda_action_taken. Highest match wins. Allows ~5% epsilon-greedy random
actions (exploration_final_eps=0.05), so >=90% = the true beta model.
"""
import json, os
import numpy as np
from . import shap_net as S


def identify_model(candidate_paths, states_np, actions_np, infer_fn=None):
    """Rank candidate .onnx by argmax match rate vs logged actions.

    infer_fn(path, states) -> [N,5] Q-values; default onnx_inference (DI for tests).
    Returns {best_path, best_match_rate, rankings: [(path, rate)], verdict}.
    """
    if infer_fn is None:
        infer_fn = S.onnx_inference
    rankings = []
    for path in candidate_paths:
        try:
            q = infer_fn(path, states_np)
            pred = np.argmax(q, axis=1)
            rate = float(np.mean(pred == actions_np))
        except Exception as e:
            rate = -1.0
            print(f"[identify_model] {path}: inference failed ({e!r})")
        rankings.append((path, rate))
    if not rankings:
        return {"best_path": None, "best_match_rate": -1.0,
                "rankings": [], "verdict": "flag"}
    rankings.sort(key=lambda r: r[1], reverse=True)
    best_path, best_rate = rankings[0]
    verdict = "pass" if best_rate >= 0.90 else ("warn" if best_rate >= 0.70 else "flag")
    return {"best_path": best_path, "best_match_rate": best_rate,
            "rankings": rankings, "verdict": verdict}


def main():
    import argparse, glob
    from . import constants as C
    ap = argparse.ArgumentParser()
    ap.add_argument("--states", default="xai/states.npy")
    ap.add_argument("--actions", default="xai/actions.npy")
    ap.add_argument("--models-dir", default="../Assets/Resources/DDA/Models")
    ap.add_argument("--meta", default="xai/meta.json")
    a = ap.parse_args()
    states = np.load(a.states)
    actions = np.load(a.actions)
    cands = sorted(glob.glob(os.path.join(a.models_dir, "*.onnx")))
    res = identify_model(cands, states, actions)
    print("=== beta-model probe (spec §9.7) ===")
    for path, rate in res["rankings"]:
        print(f"  {rate*100:5.1f}%  {os.path.basename(path)}")
    best_name = os.path.basename(res["best_path"]) if res["best_path"] else "<none>"
    print(f"verdict: {res['verdict']}  best: {best_name}")
    # merge into meta.json
    if os.path.exists(a.meta):
        with open(a.meta, encoding="utf-8") as f:
            meta = json.load(f)
    else:
        meta = {}
    meta["beta_model"] = res["best_path"]
    meta["beta_match_rate"] = res["best_match_rate"]
    meta["model_rankings"] = [(os.path.basename(p), r) for p, r in res["rankings"]]
    with open(a.meta, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    if res["verdict"] == "flag":
        raise SystemExit("HARD FLAG: no candidate matches beta policy >=70% (spec §9.7). "
                         "The deployed beta model may be missing from --models-dir.")


if __name__ == "__main__":
    main()