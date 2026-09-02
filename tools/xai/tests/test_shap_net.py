# tools/xai/tests/test_shap_net.py
import numpy as np, torch
from xai.shap_net import ShapQNet


def test_shapqnet_forward_shape_and_values():
    # known weights: identity-ish normalization, deterministic linears
    mean = np.zeros(6, dtype=np.float32)
    var = np.ones(6, dtype=np.float32)
    rng = np.random.default_rng(0)
    w1 = rng.standard_normal((6, 128)).astype(np.float32)
    b1 = np.zeros(128, dtype=np.float32)
    w2 = rng.standard_normal((128, 128)).astype(np.float32)
    b2 = np.zeros(128, dtype=np.float32)
    w3 = rng.standard_normal((128, 5)).astype(np.float32)
    b3 = np.zeros(5, dtype=np.float32)
    net = ShapQNet(mean, var, eps=1e-5, w1=w1, b1=b1, w2=w2, b2=b2, w3=w3, b3=b3)
    x = torch.zeros((4, 6))
    q = net(x)  # zeros in -> zeros through ReLU at first layer? w1@0=0, ReLU(0)=0
    assert q.shape == (4, 5)
    # with zero input and zero biases, after norm (0-0)/1=0, ReLU(0)=0, ... final = b3=0
    assert torch.allclose(q, torch.zeros((4, 5)), atol=1e-6)