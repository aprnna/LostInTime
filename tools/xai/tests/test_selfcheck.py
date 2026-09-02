# tools/xai/tests/test_selfcheck.py
"""Self-check unit tests (spec §9). Spec section 9 / Task 7.

Adaptation from the brief (per task-7 instructions, adaptation #3):
  The brief's ``test_additivity_check_function`` uses an unscaled N(0,1) net
  with ``tol=1e-3``. On shap 0.52's expected-gradient sampling that is
  unattainable: an N(0,1) net produces |Q| ~ 1e2, and the sampling noise
  dwarfs an absolute 1e-3 tolerance at any practical ``nsamples`` (see
  task-5-report.md for the same finding). We mirror Task 5's fix: scale the
  weights by 0.1 so |Q| = O(0.1), seed the rng, and use ``tol=2e-3`` (holds
  for the controlled net at ``nsamples=200``). The assertion
  ``check_additivity(...) is True`` is unchanged in substance.
"""
import numpy as np
import torch

from xai import explain_shap as E, shap_net as S, constants as C


def test_additivity_check_function():
    # Scaled net (weights x0.1) so |Q| is bounded and expected-gradients
    # sampling noise stays under tol at affordable nsamples. Seeded for
    # reproducibility (adaptation #3).
    rng = np.random.default_rng(7)
    scale = 0.1
    f = lambda shape: (rng.standard_normal(shape) * scale).astype(np.float32)
    net = S.ShapQNet(
        np.zeros(6, np.float32), np.ones(6, np.float32), 1e-5,
        f((6, 128)), np.zeros(128, np.float32),
        f((128, 128)), np.zeros(128, np.float32),
        f((128, 5)), np.zeros(5, np.float32),
    )
    states = rng.random((5, 6)).astype(np.float32)
    actions = np.array([2, 4, 0, 3, 1], dtype=np.int64)
    ok = E.check_additivity(net, states, actions, tol=2e-3, nsamples=200)
    assert ok is True


def test_outcome_coverage_check():
    outcomes = np.array([0, 1, 2, 0, 1, 2], dtype=np.int64)  # all 3 present
    assert E.check_outcome_coverage(outcomes) is True
    outcomes2 = np.array([0, 0, 0], dtype=np.int64)  # missing 1,2
    assert E.check_outcome_coverage(outcomes2) is False