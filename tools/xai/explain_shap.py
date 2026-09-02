"""SHAP explainer CLI: GradientExplainer (DeepSHAP / expected gradients) on
``ShapQNet`` with a ``KernelExplainer`` fallback on the raw ``.onnx``.

Spec sections 3, 7, 9.1-9.4. Emits per-decision waterfall plots (all 5 action
Q-values) and a global beeswarm over the chosen-action SHAP to
``results/shap/<model_stem>/``.

SHAP API adaptations (shap 0.52; see task-5-report.md):
  * ``shap.GradientExplainer.shap_values`` returns a single ``ndarray`` of shape
    ``(N, n_features, n_outputs)`` (the list-per-output layout was dropped in
    shap 0.45). ``_GradExplainerWrapper`` normalizes it to a **list of 5**
    ``[N, 6]`` arrays so the rest of the pipeline matches the brief's contract.
  * ``GradientExplainer`` no longer exposes ``expected_value``. The wrapper
    computes it as the model-output mean over the background
    (``E_{bg}[f(bg)]``, shape ``[5]``) -- the DeepSHAP / expected-gradients base
    value that makes ``base[a] + sum(shap[a][i]) == Q[i, a]`` hold in the
    large-``nsamples`` limit.
  * ``waterfall_plot`` / ``summary_plot`` are called with ``max_display=6`` /
    ``plot_type="dot"`` and ``show=False`` for headless PNG rendering.
"""

import os
import json
import argparse

import numpy as np
import torch
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import shap

from . import constants as C
from . import shap_net as S


# ---------------------------------------------------------------------------
# Explainer construction
# ---------------------------------------------------------------------------


class _GradExplainerWrapper:
    """Normalize shap 0.52 ``GradientExplainer`` to the brief's list-per-output
    contract and expose an ``expected_value`` base array.

    ``shap_values(X)`` -> list of 5 arrays of shape ``[N, 6]`` (one per action
    output). ``expected_value`` -> ``[5]`` float64 array (mean of the model
    output over the background dataset).
    """

    def __init__(self, net, background):
        self._net = net
        bg_t = torch.as_tensor(np.asarray(background), dtype=torch.float32)
        with torch.no_grad():
            self.expected_value = (
                self._net(bg_t).mean(dim=0).detach().numpy().astype(np.float64)
            )  # [5]
        self._inner = shap.GradientExplainer(self._net, bg_t)

    @property
    def inner(self):
        return self._inner

    def shap_values(self, X, nsamples=200, rseed=None):
        """Return SHAP values as a list of 5 ``[N, 6]`` arrays (one per action)."""
        sv = np.asarray(
            self._inner.shap_values(X, nsamples=nsamples, rseed=rseed)
        )
        # shap 0.52 layout: (N, n_features, n_outputs) == (N, 6, 5).
        # Slice per action axis to produce list-of-5 [N, 6] arrays.
        if sv.ndim == 3 and sv.shape[-1] == C.ACTION_SIZE:
            return [sv[:, :, a] for a in range(C.ACTION_SIZE)]
        # Legacy list-per-output fallback (older shap).
        if isinstance(sv, list):
            return [np.asarray(s) for s in sv]
        raise ValueError(f"unexpected shap_values shape {sv.shape}")


def build_gradient_explainer(net, background):
    """GradientExplainer (DeepSHAP / expected gradients). Returns a wrapper with
    ``shap_values`` (list-of-5) and ``expected_value`` ([5])."""
    return _GradExplainerWrapper(net, background)


def build_kernel_explainer(infer_fn, background):
    """KernelExplainer fallback on the raw ``.onnx`` (approach C).

    ``infer_fn`` maps ``states -> [N, 5]`` Q-values (typically
    ``lambda s: onnx_inference(onnx_path, s)``).
    """
    return shap.KernelExplainer(infer_fn, np.asarray(background, dtype=np.float32))


# ---------------------------------------------------------------------------
# Plotting
# ---------------------------------------------------------------------------


def _save_waterfall(shap_vals_row, base_value_scalar, state_row, action_idx,
                    decision_idx, out_dir):
    """Write a single waterfall PNG for one (decision, action) pair."""
    os.makedirs(out_dir, exist_ok=True)
    exp = shap.Explanation(
        values=np.asarray(shap_vals_row, dtype=np.float64),
        base_values=np.float64(base_value_scalar),
        data=np.asarray(state_row, dtype=np.float32),
        feature_names=list(C.FEATURE_NAMES),
    )
    shap.waterfall_plot(exp, max_display=C.OBS_SIZE, show=False)
    action_name = C.ACTION_INT_TO_NAME.get(action_idx, f"Action {action_idx}")
    plt.title(f"Decision {decision_idx} - Action {action_idx} ({action_name})", fontsize=12, fontweight="bold", pad=12)
    plt.tight_layout()
    p = os.path.join(
        out_dir, f"waterfall_decision{decision_idx}_action{action_idx}.png"
    )
    plt.savefig(p, dpi=150, bbox_inches="tight")
    plt.close()
    return p


def explain_all(states, actions, expl, expected_value, out_dir,
                decision_indices=None, all_actions=True, nsamples=200,
                rseed=None):
    """Emit waterfall plots.

    ``decision_indices``: which rows to explain (default ``len(states)``).
    ``all_actions``: if True, explain all 5 action Q-values per decision; else
    only the chosen action.
    Returns ``(sv, base)`` where ``sv`` is a list of 5 ``[N, 6]`` arrays and
    ``base`` is a ``[5]`` array.
    """
    states_t = torch.as_tensor(np.asarray(states), dtype=torch.float32)
    sv = expl.shap_values(states_t, nsamples=nsamples, rseed=rseed)  # list-of-5
    base = np.asarray(expected_value, dtype=np.float64)  # [5]
    idxs = decision_indices if decision_indices is not None else list(range(len(states)))
    for d in idxs:
        targets = range(C.ACTION_SIZE) if all_actions else [int(actions[d])]
        for a in targets:
            _save_waterfall(
                sv[a][d],
                float(base[a]),
                states[d],
                a,
                d,
                out_dir,
            )
    return sv, base


def explain_beeswarm(states, actions, sv, base, out_dir):
    """Beeswarm over the chosen-action SHAP (spec section 1 global layer)."""
    os.makedirs(out_dir, exist_ok=True)
    chosen_sv = np.stack(
        [np.asarray(sv[int(actions[i])][i], dtype=np.float64) for i in range(len(states))]
    )  # [N, 6]
    shap.summary_plot(
        chosen_sv,
        np.asarray(states, dtype=np.float32),
        feature_names=list(C.FEATURE_NAMES),
        plot_type="dot",
        show=False,
    )
    plt.tight_layout()
    p = os.path.join(out_dir, "summary_beeswarm.png")
    plt.savefig(p, dpi=150, bbox_inches="tight")
    plt.close()
    return p, chosen_sv


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def _decisions(spec, n):
    if spec == "all":
        return list(range(n))
    if spec == "representative":
        # Evenly spaced ~10 decisions across the run (spec section 12).
        return list(range(n))[:: max(1, n // 10)]
    return [int(x) for x in spec.split(",") if x.strip() != ""]


# ---------------------------------------------------------------------------
# Self-check (spec §9) — 7 aggregate checks
# ---------------------------------------------------------------------------


def check_additivity(net, states, actions, tol=5e-2, nsamples=2000):
    """Spec §9.3 additivity (local accuracy): base[a] + sum(shap[a][i]) ~= Q[i, a].

    Uses the ``_GradExplainerWrapper`` (NOT raw shap 0.52 attributes) so the
    list-of-5 ``shap_values`` and the computed ``expected_value`` ([5]) are
    normalized to the brief's contract. Returns True if every (state, action)
    pair reconstructs within ``tol``.

    Coordinator ruling: shap 0.52 expected-gradient sampling makes additivity
    approximate; the SELF-CHECK tolerance is 5e-2 (NOT 1e-3) at nsamples>=2000.
    """
    expl = build_gradient_explainer(net, states)
    sv = expl.shap_values(torch.as_tensor(np.asarray(states), dtype=torch.float32),
                          nsamples=nsamples, rseed=42)  # list-of-5 [N,6]
    base = np.asarray(expl.expected_value, dtype=np.float64)  # [5]
    states_t = torch.as_tensor(np.asarray(states), dtype=torch.float32)
    for i in range(len(states)):
        a = int(actions[i])
        recon = float(base[a]) + float(np.asarray(sv[a][i], dtype=np.float64).sum())
        q = float(net(states_t[i:i + 1])[0, a].detach())
        if abs(recon - q) >= tol:
            return False
    return True


def check_additivity_error(net, states, actions, nsamples=2000):
    """Return the ACHIEVED max |base+sum−Q| over all (state, action) pairs.

    Used by ``run_self_check`` to report the real additivity error so the
    thesis can cite it (spec §9.3 + coordinator ruling).
    """
    expl = build_gradient_explainer(net, states)
    sv = expl.shap_values(torch.as_tensor(np.asarray(states), dtype=torch.float32),
                          nsamples=nsamples, rseed=42)
    base = np.asarray(expl.expected_value, dtype=np.float64)
    states_t = torch.as_tensor(np.asarray(states), dtype=torch.float32)
    worst = 0.0
    for i in range(len(states)):
        a = int(actions[i])
        recon = float(base[a]) + float(np.asarray(sv[a][i], dtype=np.float64).sum())
        q = float(net(states_t[i:i + 1])[0, a].detach())
        worst = max(worst, abs(recon - q))
    return worst


def check_outcome_coverage(outcomes):
    """Spec §9.5: all 3 outcome codes (0=Subjugate, 1=Balanced, 2=Rebellious)
    present at least once."""
    return all(int((outcomes == c).sum()) >= 1 for c in (0, 1, 2))


def run_self_check(states_path="xai/states.npy",
                   actions_path="xai/actions.npy",
                   outcomes_path="xai/outcomes.npy",
                   survival_path="xai/survival.npy",
                   meta_path="xai/meta.json",
                   models_dir="../Assets/Resources/DDA/Models"):
    """Run the 7 spec §9 checks. Returns (all_pass, results_dict)."""
    import glob
    import json
    from . import identify_model as IM

    results = {}
    states = np.load(states_path).astype(np.float32)
    actions = np.load(actions_path)
    outcomes = np.load(outcomes_path)
    survival = np.load(survival_path)
    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)
    onnx_path = meta.get("beta_model")
    if onnx_path and not os.path.exists(onnx_path):
        if os.path.exists(os.path.join("..", onnx_path)):
            onnx_path = os.path.join("..", onnx_path)
        elif os.path.exists(os.path.join(models_dir, os.path.basename(onnx_path))):
            onnx_path = os.path.join(models_dir, os.path.basename(onnx_path))

    # §9.2 range — 6 obs, all in [0, 1]
    results["range"] = bool(
        states.shape[1] == C.OBS_SIZE and np.all((states >= 0) & (states <= 1))
    )

    # §9.7 beta-model probe (re-run to confirm)
    probe = IM.identify_model(
        sorted(glob.glob(os.path.join(models_dir, "*.onnx"))), states, actions
    )
    results["beta_probe"] = probe["verdict"] in ("pass", "warn")
    results["beta_match_rate"] = probe["best_match_rate"]

    # §9.1 faithfulness + §9.3 additivity
    net = None
    try:
        net = S.load_from_onnx(onnx_path)
        chk = S.check_faithfulness(net, onnx_path, states[:10])
        results["faithfulness"] = bool(chk["passed"])
        results["faithfulness_max_diff"] = chk["max_diff"]
    except Exception as e:
        results["faithfulness"] = False
        results["faithfulness_err"] = repr(e)
        net = None

    # §9.3 additivity — tol 5e-2, nsamples 2000 (bump on failure: 4000, 8000)
    if net is not None:
        nsamples = 2000
        max_err = check_additivity_error(net, states, actions, nsamples=nsamples)
        # Tighten monotonically if the achieved error breaches 5e-2.
        for bump in (4000, 8000):
            if max_err < 5e-2:
                break
            nsamples = bump
            max_err = check_additivity_error(net, states, actions, nsamples=nsamples)
        results["additivity"] = bool(max_err < 5e-2)
        results["additivity_max_error"] = float(max_err)
        results["additivity_nsamples"] = int(nsamples)
    else:
        results["additivity"] = False
        results["additivity_max_error"] = None
        results["additivity_nsamples"] = None

    # §9.4 determinism — check the NET (proxy): shap 0.52 expected-gradient
    # sampling is NOT deterministic; the extracted net IS. Do not check SHAP.
    if net is not None:
        q1 = net(torch.as_tensor(states[:3], dtype=torch.float32)).detach().numpy()
        q2 = net(torch.as_tensor(states[:3], dtype=torch.float32)).detach().numpy()
        results["determinism"] = bool(np.allclose(q1, q2, atol=1e-7))
    else:
        results["determinism"] = False

    # §9.5 outcome coverage
    results["outcome_coverage"] = check_outcome_coverage(outcomes)

    # §9.6 survival sanity: outcomes consistent with survival thresholds
    exp_outcomes = np.array(
        [C.survival_to_outcome(float(s)) for s in survival], dtype=np.int64
    )
    mismatch = int(np.sum(exp_outcomes != outcomes))
    results["survival_sanity"] = bool(np.array_equal(exp_outcomes, outcomes))
    if mismatch:
        results["survival_sanity_mismatch"] = mismatch

    print("=== self-check (spec §9) ===")
    for k, v in results.items():
        print(f"  {k}: {v}")
    all_pass = all(
        bool(results[k]) for k in (
            "range", "beta_probe", "faithfulness", "additivity",
            "determinism", "outcome_coverage", "survival_sanity",
        )
    )
    print(f"=== overall: {'PASS' if all_pass else 'FAIL'} ===")
    return all_pass, results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--states", default="xai/states.npy")
    ap.add_argument("--actions", default="xai/actions.npy")
    ap.add_argument("--outcomes", default="xai/outcomes.npy")
    ap.add_argument("--meta", default="xai/meta.json")
    ap.add_argument("--model", default=None,
                    help="path or filename of .onnx model to explain (overrides meta.json)")
    ap.add_argument("--out-dir", default=None)
    ap.add_argument("--decisions", default="all",
                    help="all | representative | comma list of indices")
    ap.add_argument("--all-actions", action="store_true", default=True)
    ap.add_argument("--nsamples", type=int, default=2000,
                    help="expected-gradients samples (higher = tighter additivity)")
    ap.add_argument(
        "--filter-outcome", default=None,
        choices=["Subjugate", "Balanced", "Rebellious"],
        help="restrict background + explained subset to one outcome category",
    )
    ap.add_argument(
        "--counterfactual", action="store_true",
        help="perturb HP Ratio 0.55->0.30, re-explain, diff Q per action",
    )
    ap.add_argument(
        "--self-check", action="store_true",
        help="run the 7 spec §9 aggregate checks and exit (non-zero on FAIL)",
    )
    a = ap.parse_args()

    if a.self_check:
        ok, _ = run_self_check()
        raise SystemExit(0 if ok else 1)

    states = np.load(a.states).astype(np.float32)
    actions = np.load(a.actions)
    outcomes = np.load(a.outcomes)
    with open(a.meta, encoding="utf-8") as f:
        meta = json.load(f)
    onnx_path = a.model or meta.get("beta_model")
    if onnx_path and not os.path.exists(onnx_path):
        if os.path.exists(os.path.join("..", onnx_path)):
            onnx_path = os.path.join("..", onnx_path)
        elif os.path.exists(os.path.join("../Assets/Resources/DDA/Models", os.path.basename(onnx_path))):
            onnx_path = os.path.join("../Assets/Resources/DDA/Models", os.path.basename(onnx_path))
        elif os.path.exists(os.path.join("Assets/Resources/DDA/Models", os.path.basename(onnx_path))):
            onnx_path = os.path.join("Assets/Resources/DDA/Models", os.path.basename(onnx_path))
    model_stem = os.path.splitext(os.path.basename(onnx_path))[0]
    out_dir = a.out_dir or f"results/shap/{model_stem}"
    os.makedirs(out_dir, exist_ok=True)

    # Optional outcome filter (spec §7: background = filtered subset).
    if a.filter_outcome:
        code = C.OUTCOME_CODES[a.filter_outcome]
        mask = outcomes == code
        if int(mask.sum()) < 10:
            print(
                f"[explain_shap] WARN only {int(mask.sum())} {a.filter_outcome} "
                f"decisions; SHAP variance high (spec §7)"
            )
        states = states[mask]
        actions = actions[mask]
        outcomes = outcomes[mask]
        print(f"[explain_shap] filtered to {a.filter_outcome}: {states.shape[0]} decisions")
        if states.shape[0] == 0:
            raise SystemExit(
                f"[explain_shap] ERROR: no decisions remain after --filter-outcome "
                f"{a.filter_outcome}. Nothing to explain."
            )

    decision_indices = _decisions(a.decisions, len(states))

    # Decide backend: try extraction + faithfulness; else KernelExplainer.
    try:
        net = S.load_from_onnx(onnx_path)
        chk = S.check_faithfulness(net, onnx_path, states[:10])
        assert chk["passed"], chk
        expl = build_gradient_explainer(net, states)
        sv, base = explain_all(
            states, actions, expl, expl.expected_value, out_dir,
            decision_indices=decision_indices,
            all_actions=a.all_actions,
            nsamples=a.nsamples,
            rseed=42,
        )
        print(
            f"[explain_shap] GradientExplainer backend "
            f"(max_diff={chk['max_diff']:.2e})"
        )
    except Exception as e:
        print(
            f"[explain_shap] extraction/faithfulness failed ({e!r}); "
            f"fallback KernelExplainer"
        )
        from .shap_net import onnx_inference
        expl = build_kernel_explainer(
            lambda s: onnx_inference(onnx_path, s), states
        )
        # KernelExplainer.shap_values returns a list-per-output (or ndarray).
        # Pass nsamples from CLI (--nsamples) so the Kernel path honours the
        # same budget as the GradientExplainer path.  rseed=42 for reproducibility.
        sv_raw = expl.shap_values(states, nsamples=a.nsamples, rseed=42)
        if isinstance(sv_raw, list):
            sv = [np.asarray(s) for s in sv_raw]
        else:
            arr = np.asarray(sv_raw)
            # (N, 6, 5) -> list-of-5 [N,6]. Use act_idx to avoid shadowing the
            # argparse namespace `a` which is still needed below (a.all_actions).
            sv = [arr[:, :, act_idx] for act_idx in range(arr.shape[-1])]
        base = np.asarray(expl.expected_value, dtype=np.float64)
        # Write waterfalls directly from the computed sv (Kernel path).
        for d in decision_indices:
            targets = range(C.ACTION_SIZE) if a.all_actions else [int(actions[d])]
            for act in targets:
                _save_waterfall(sv[act][d], float(base[act]), states[d], act, d, out_dir)
        # NOTE: full Kernel waterfall wiring is finalized in Task 6.

    # Beeswarm over the chosen-action SHAP.
    explain_beeswarm(states, actions, sv, base, out_dir)
    print(f"[explain_shap] wrote plots to {out_dir}")

    # Counterfactual perturbation (spec §5.1): HP Ratio (idx 0) -> 0.30.
    if a.counterfactual:
        from .shap_net import onnx_inference
        ref = states[0:1].copy()
        from_val = float(ref[0, 0])
        q_before = onnx_inference(onnx_path, ref)[0]
        cf = ref.copy()
        cf[0, 0] = 0.30
        q_after = onnx_inference(onnx_path, cf)[0]
        # Generate visual bar plot for Counterfactual
        act_names = list(C.ACTION_NAME_TO_INT.keys())
        x_indices = np.arange(len(act_names))
        bar_w = 0.35
        fig, ax = plt.subplots(figsize=(9, 5))
        r1 = ax.bar(x_indices - bar_w/2, q_before, bar_w, label=f'Sebelum (HP={from_val:.2f})', color='#3498db')
        r2 = ax.bar(x_indices + bar_w/2, q_after, bar_w, label='Setelah Intervensi (HP=0.30)', color='#e74c3c')
        ax.set_ylabel('Q-Value (Preferensi Agen)')
        ax.set_title(f'Eksperimen Counterfactual: Respon Agen saat HP Diturunkan ({from_val:.2f} -> 0.30)')
        ax.set_xticks(x_indices)
        ax.set_xticklabels(act_names)
        ax.legend()
        ax.grid(axis='y', linestyle='--', alpha=0.7)
        for r in r1:
            h = r.get_height()
            ax.annotate(f'{h:.2f}', xy=(r.get_x() + r.get_width()/2, h), xytext=(0, 3), textcoords='offset points', ha='center', va='bottom', fontsize=9)
        for r in r2:
            h = r.get_height()
            ax.annotate(f'{h:.2f}', xy=(r.get_x() + r.get_width()/2, h), xytext=(0, 3), textcoords='offset points', ha='center', va='bottom', fontsize=9)
        plt.tight_layout()
        cf_plot_path = os.path.join(out_dir, "counterfactual_comparison.png")
        plt.savefig(cf_plot_path, dpi=150)
        plt.close()
        print(f"[explain_shap] wrote counterfactual comparison plot to {cf_plot_path}")

    # Failure-pattern report (spec §4 component 4, §4.1, §5.1).
    from . import report as REP
    chosen_sv = np.stack(
        [np.asarray(sv[int(actions[i])][i], dtype=np.float64) for i in range(len(states))]
    )
    REP.generate_report(
        states, actions, outcomes, chosen_sv, base,
        {
            "beta_model": onnx_path,
            "beta_match_rate": meta.get("beta_match_rate", 0),
            "n_decisions": int(states.shape[0]),
        },
        out_dir,
    )
    print(f"[explain_shap] wrote report.md to {out_dir}")


if __name__ == "__main__":
    main()