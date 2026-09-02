# tools/xai/tests/test_report.py
import numpy as np
from xai import report as R, constants as C


def _fake_sv(n):
    # chosen-action shap [n,6]; HP Ratio dominates
    sv = np.zeros((n, 6), dtype=np.float64)
    sv[:, 0] = np.linspace(0.5, -0.3, n)  # HP Ratio
    sv[:, 3] = np.linspace(0.1, -0.05, n)  # Dmg ratio
    return sv


def test_failure_pattern_sections_present(tmp_path):
    n = 10
    states = np.random.default_rng(0).random((n, 6)).astype(np.float32)
    actions = np.array([4, 4, 0, 3, 2, 4, 3, 0, 4, 2], dtype=np.int64)
    outcomes = np.array([0, 2, 0, 2, 1, 0, 2, 0, 0, 1], dtype=np.int64)  # mix
    sv = _fake_sv(n)
    base = np.zeros(5, dtype=np.float64)
    meta = {"beta_model": "fake.onnx", "beta_match_rate": 0.94, "n_decisions": n}
    path = R.generate_report(states, actions, outcomes, sv, base, meta, str(tmp_path))
    txt = open(path, encoding="utf-8").read()
    assert "# Pola kegagalan (Subjugate)" in txt
    assert "# Pola kegagalan (Rebellious)" in txt
    assert "HP Ratio" in txt  # dominant feature named


def test_empty_category_is_noted_not_fabricated(tmp_path):
    n = 5
    states = np.zeros((n, 6), dtype=np.float32)
    actions = np.array([4, 4, 4, 4, 2], dtype=np.int64)
    outcomes = np.array([0, 0, 0, 0, 1], dtype=np.int64)  # no Rebellious
    sv = _fake_sv(n)
    base = np.zeros(5, dtype=np.float64)
    meta = {"beta_model": "f.onnx", "beta_match_rate": 0.9, "n_decisions": n}
    path = R.generate_report(states, actions, outcomes, sv, base, meta, str(tmp_path))
    txt = open(path, encoding="utf-8").read()
    assert "Pola kegagalan (Rebellious)" in txt
    assert "insufficient data" in txt.lower() or "no Rebellious decisions" in txt