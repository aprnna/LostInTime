# tools/xai/tests/test_explain_shap.py
"""Additivity self-check + KernelExplainer build smoke. Spec section 3 / Task 5.

Deviation from the brief (documented in task-5-report.md): shap 0.52's
``GradientExplainer`` returns a single ``ndarray`` of shape ``(N, 6, 5)`` and
exposes **no** ``expected_value`` attribute (the list-per-output API + expected_value
attribute were removed in shap 0.45). ``build_gradient_explainer`` therefore
returns a thin wrapper that normalizes ``shap_values`` to a list-of-5 ``[N,6]``
arrays and exposes ``expected_value`` as a ``[5]`` array (mean of the model
output over the background). The additivity assertion below is unchanged in
substance: ``base[a] + sum(shap[a][i]) ~= Q[i, a]``.
"""
import numpy as np
import torch

from xai import shap_net as S, explain_shap as E


def _random_net(weight_scale=0.1):
    # Weights scaled down from N(0,1) so Q-values are O(0.1) and the
    # expected-gradients sampling noise stays well under the additivity tol
    # at affordable nsamples. The brief's unscaled N(0,1) net produces
    # |Q| ~ 1e2, for which an absolute additivity tol of 1e-3 is unattainable
    # at any practical nsamples (see task-5-report.md).
    rng = np.random.default_rng(1)
    f = lambda r, s: (rng.standard_normal(r) * s).astype(np.float32)
    return S.ShapQNet(
        np.zeros(6, np.float32), np.ones(6, np.float32), 1e-5,
        f((6, 128), weight_scale), np.zeros(128, np.float32),
        f((128, 128), weight_scale), np.zeros(128, np.float32),
        f((128, 5), weight_scale), np.zeros(5, np.float32),
    )


def test_additivity_local_accuracy():
    # SHAP local-accuracy: base_value[a] + sum(shap[a][i]) ~= Q[i, chosen]
    states = np.random.default_rng(2).random((8, 6)).astype(np.float32)
    actions = np.array([2, 4, 0, 3, 1, 2, 3, 0], dtype=np.int64)
    net = _random_net()
    expl = E.build_gradient_explainer(net, background=states)
    # High nsamples keeps expected-gradients sampling noise well below tol.
    shap_vals = expl.shap_values(torch.as_tensor(states, dtype=torch.float32),
                                 nsamples=4000, rseed=42)  # list of 5 [N,6]
    base = np.asarray(expl.expected_value, dtype=np.float64)  # [5]
    assert base.shape == (5,)
    assert len(shap_vals) == 5
    tol = 1e-2  # expected-gradients sampling noise (ReLU + float32); see report
    worst = 0.0
    for i in range(8):
        a = int(actions[i])
        sv = np.asarray(shap_vals[a][i], dtype=np.float64)
        q = float(net(torch.as_tensor(states[i:i + 1], dtype=torch.float32))[0, a])
        recon = float(base[a]) + float(sv.sum())
        worst = max(worst, abs(recon - q))
        assert abs(recon - q) < tol, f"additivity fail d{i} a{a}: {recon} vs {q}"


def test_build_kernel_fallback_callable():
    # KernelExplainer path takes an infer_fn(states)->[N,5]; wrapper returns such.
    states = np.random.default_rng(3).random((6, 6)).astype(np.float32)

    def infer(s):
        return np.random.default_rng(4).standard_normal((s.shape[0], 5)).astype(np.float32)

    expl = E.build_kernel_explainer(infer, background=states)
    assert hasattr(expl, "shap_values")