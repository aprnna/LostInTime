# tools/xai/tests/test_identify_model.py
import numpy as np
from xai import identify_model as I

def test_match_rate_with_mock_infer():
    states = np.zeros((5, 6), dtype=np.float32)
    actions = np.array([2, 4, 1, 3, 1], dtype=np.int64)  # ground truth (no 0 so infer_b never matches)
    # mock inference: argmax matches actions exactly for model_a, never for model_b
    def infer_a(path, s):
        q = np.zeros((s.shape[0], 5), dtype=np.float32)
        for i, a in enumerate(actions):
            q[i, a] = 1.0
        return q
    def infer_b(path, s):
        q = np.zeros((s.shape[0], 5), dtype=np.float32)
        q[:, 0] = 1.0  # always argmax 0
        return q
    # infer_fn is called per candidate; route by path
    def infer(path, s):
        return infer_a(path, s) if "a" in path else infer_b(path, s)
    res = I.identify_model(["model_a.onnx", "model_b.onnx"], states, actions, infer_fn=infer)
    assert res["best_path"] == "model_a.onnx"
    assert res["rankings"][0][1] == 1.0      # model_a match 100%
    assert res["rankings"][1][1] == 0.0      # model_b match 0%
    assert res["best_match_rate"] == 1.0