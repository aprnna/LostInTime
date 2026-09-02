"""Generate report.md: global ranking + failure-pattern sections + counterfactual notes.
Spec §4 component 4, §4.1, §5.1.
"""
import os
import numpy as np
from . import constants as C


def _mean_abs_shap(sv):
    return np.mean(np.abs(sv), axis=0)


def _category_block(name, states, actions, outcomes, sv, code):
    mask = outcomes == code
    n = int(mask.sum())
    lines = [f"# Pola kegagalan ({name})", ""]
    if n == 0:
        lines += [
            f"No {name} decisions in the data (n=0) — insufficient data; "
            f"section skipped (not fabricated).",
            "",
        ]
        return "\n".join(lines)
    sv_c = sv[mask]
    ma = _mean_abs_shap(sv_c)
    order = np.argsort(ma)[::-1]
    lines += [
        f"n = {n} decisions (outcome code {code}).",
        "",
        "| Rank | Feature | mean |SHAP| |",
        "|------|---------|-------------|",
    ]
    for r, i in enumerate(order, 1):
        lines.append(f"| {r} | {C.FEATURE_NAMES[i]} | {ma[i]:.4f} |")
    # typical action in this category
    act_c = actions[mask]
    act_name = C.ACTION_INT_TO_NAME[int(np.bincount(act_c).argmax())]
    lines += ["", f"Typical action chosen: **{act_name}**.", ""]
    return "\n".join(lines)


def generate_report(states, actions, outcomes, sv, base, model_meta, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    n = states.shape[0]
    ma = _mean_abs_shap(sv)
    order = np.argsort(ma)[::-1]
    lines = [
        "# SHAP XAI Report — DDQN DDA",
        "",
        f"**Model:** `{os.path.basename(model_meta.get('beta_model', '?'))}`",
        f"**Beta-model match rate:** {model_meta.get('beta_match_rate', float('nan'))*100:.1f}% (spec §9.7)",
        f"**Decisions explained:** {n}",
        "",
        "## Global feature ranking (mean |SHAP|, chosen-action)",
        "",
        "| Rank | Feature | mean |SHAP| |",
        "|------|---------|-------------|",
    ]
    for r, i in enumerate(order, 1):
        lines.append(f"| {r} | {C.FEATURE_NAMES[i]} | {ma[i]:.4f} |")
    lines += [
        "",
        "## Validitas (spec §5.1)",
        "",
        "SHAP explanations are computed on the real closed-beta `dda_event` states "
        f"(DataPost, {n} real agent decisions (cross-event pairing) = basis BCM 15.31%). "
        "Weights are extracted from the deployed beta `.onnx` identified by the "
        "match-rate probe. Explanations are descriptive of the deployed policy on "
        "states it actually met — not causal claims about training. Counterfactuals "
        "outside the observed observation range are extrapolation.",
        "",
    ]
    lines.append(_category_block("Subjugate", states, actions, outcomes, sv, 0))
    lines.append("")
    lines.append(_category_block("Rebellious", states, actions, outcomes, sv, 2))
    lines += [
        "",
        "## Counterfactual boundary notes",
        "",
        "See `counterfactual_*.png` and diff tables. Perturbations that move an "
        "observation outside the observed beta range are flagged as extrapolation.",
        "",
    ]
    path = os.path.join(out_dir, "report.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return path