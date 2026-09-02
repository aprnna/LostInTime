"""Minimal PyTorch replica of QNetworkDDQN + ONNX weight extraction. Spec §6.

Architecture: Norm(running mean / sqrt(running_var+eps)) -> Clip[-5,5]
-> Linear(6,128) -> ReLU -> Linear(128,128) -> ReLU -> Linear(128,5).
Weights extracted from the deployed beta .onnx.

Adaptations vs. the brief (verified on ddqn_dda_sidang1.onnx, Step 5 reality
check):
  * The ML-Agents ONNX export does NOT store ``running_var`` as an initializer.
    It stores the precomputed divisor ``sqrt(running_var + eps)`` (the second
    input of the normalization ``Div`` node, named like ``onnx::Div_<N>``).
    ``load_from_onnx`` recovers ``var = divisor**2`` and sets ``eps=0`` so
    ``sqrt(var+eps) == divisor`` exactly.
  * The normalizer is followed by an ONNX ``Clip`` node (range [-5, 5] for the
    beta model). ``ShapQNet.forward`` mirrors this with ``torch.clamp``; the
    clip range is detected from the ONNX Clip node by ``load_from_onnx``.
  * The ONNX graph exposes only argmax action outputs (shape [N,1]), not raw
    Q-values. ``onnx_inference`` appends the value-head Gemm output to the
    graph outputs and returns that [N,5] tensor.
"""

import numpy as np
import onnx
import onnxruntime as ort
import torch
import torch.nn as nn
from onnx import TensorProto, helper


class ShapQNet(nn.Module):
    """PyTorch replica of the DDQN Q-network for SHAP attribution.

    forward(x: [B,6]) -> [B,5]. Normalization is (x - mean) / sqrt(var + eps)
    followed by a clamp to [clip_min, clip_max] (ML-Agents normalizer clip).
    """

    def __init__(self, norm_mean, norm_var, eps, w1, b1, w2, b2, w3, b3,
                 clip_min=-5.0, clip_max=5.0):
        super().__init__()
        self.register_buffer("norm_mean", torch.as_tensor(norm_mean, dtype=torch.float32))
        self.register_buffer("norm_var", torch.as_tensor(norm_var, dtype=torch.float32))
        self.eps = float(eps)
        # None disables the clip (matches a non-clipping normalizer).
        self.clip_min = clip_min
        self.clip_max = clip_max
        # PyTorch Linear weight is [out, in]; accept either [in,out] or [out,in]
        # by orienting against the bias length (bias has shape [out]).
        def _orient(w, b):
            w = np.asarray(w, dtype=np.float32)
            b = np.asarray(b, dtype=np.float32)
            out = b.shape[0]
            if w.shape[0] == out:
                return w, w.shape[1], out  # already [out, in]
            return w.T, w.shape[0], out    # was [in, out] -> [out, in]
        w1p, in1, out1 = _orient(w1, b1)
        w2p, in2, out2 = _orient(w2, b2)
        w3p, in3, out3 = _orient(w3, b3)
        self.l1 = nn.Linear(in1, out1)
        self.l2 = nn.Linear(in2, out2)
        self.l3 = nn.Linear(in3, out3)
        with torch.no_grad():
            self.l1.weight.copy_(torch.as_tensor(w1p, dtype=torch.float32))
            self.l1.bias.copy_(torch.as_tensor(b1, dtype=torch.float32))
            self.l2.weight.copy_(torch.as_tensor(w2p, dtype=torch.float32))
            self.l2.bias.copy_(torch.as_tensor(b2, dtype=torch.float32))
            self.l3.weight.copy_(torch.as_tensor(w3p, dtype=torch.float32))
            self.l3.bias.copy_(torch.as_tensor(b3, dtype=torch.float32))

    def forward(self, x):
        # Clamp norm_var >= 0 to guard against floating-point underflow before
        # sqrt; eps is baked into the ONNX divisor so eps=0.0 is intentional,
        # but a tiny negative var (e.g. -1e-14 from f32 round-trip) would NaN.
        safe_var = torch.clamp(self.norm_var, min=0.0)
        x = (x - self.norm_mean) / torch.sqrt(safe_var + self.eps)
        if self.clip_min is not None and self.clip_max is not None:
            x = torch.clamp(x, self.clip_min, self.clip_max)
        x = torch.relu(self.l1(x))
        x = torch.relu(self.l2(x))
        return self.l3(x)  # [B,5]


# ---------------------------------------------------------------------------
# ONNX weight extraction helpers
# ---------------------------------------------------------------------------

def _np_dtype(onnx_dtype):
    return {
        TensorProto.FLOAT: np.float32, TensorProto.DOUBLE: np.float64,
        TensorProto.INT32: np.int32, TensorProto.INT64: np.int64,
    }.get(onnx_dtype, np.float32)


def numpy_from_onnx(tensor):
    """Convert an ONNX TensorProto (initializer or Constant value) to numpy.

    ONNX stores tensor data in one of two ways:
    - ``raw_data`` (bytes) — large tensors and most initializers.
    - Typed fields (``float_data``, ``int32_data``, ``int64_data``,
      ``double_data``) — scalar/small Constant nodes (e.g. Clip min/max
      in opset >= 12).  ``raw_data`` is empty in this case.
    """
    if tensor.raw_data:
        arr = np.frombuffer(tensor.raw_data, dtype=_np_dtype(tensor.data_type)).copy()
    elif tensor.float_data:
        arr = np.array(list(tensor.float_data), dtype=np.float32)
    elif tensor.double_data:
        arr = np.array(list(tensor.double_data), dtype=np.float64)
    elif tensor.int32_data:
        arr = np.array(list(tensor.int32_data), dtype=np.int32)
    elif tensor.int64_data:
        arr = np.array(list(tensor.int64_data), dtype=np.int64)
    else:
        arr = np.array([], dtype=_np_dtype(tensor.data_type))
    arr = arr.reshape(tensor.dims) if tensor.dims else arr.reshape(())
    return arr.astype(np.float32)


def _initializers(onnx_path):
    m = onnx.load(onnx_path)
    inits = {init.name: numpy_from_onnx(init) for init in m.graph.initializer}
    return inits, m


def _find_norm_divisor(m, mean_name):
    """Locate the normalization divisor initializer.

    The ML-Agents normalizer emits: Sub(obs, running_mean) -> Div(_, divisor)
    -> Clip. Returns the divisor array (== sqrt(running_var + eps)) and the
    Clip (min, max) tuple, or (None, (None, None)) if not found.
    """
    # Find the Sub node that consumes running_mean.
    sub_out = None
    for n in m.graph.node:
        if n.op_type == "Sub" and mean_name in n.input:
            sub_out = n.output[0]
            break
    if sub_out is None:
        return None, (None, None)
    # Find the Div node that consumes sub_out; its other input is the divisor.
    divisor = None
    div_out = None
    for n in m.graph.node:
        if n.op_type == "Div" and sub_out in n.input:
            for inp in n.input:
                if inp != sub_out:
                    for init in m.graph.initializer:
                        if init.name == inp:
                            divisor = numpy_from_onnx(init)
            div_out = n.output[0]
            break
    # Find the Clip node that consumes div_out.
    clip = (None, None)
    if div_out is not None:
        for n in m.graph.node:
            if n.op_type == "Clip" and div_out in n.input:
                cmin, cmax = None, None
                # Clip min/max can be attributes (older opset) or inputs.
                for a in n.attribute:
                    if a.name == "min":
                        cmin = float(a.f)
                    elif a.name == "max":
                        cmax = float(a.f)
                # opset >= 12 passes min/max as inputs (Constant nodes).
                if cmin is None or cmax is None:
                    for inp in n.input[1:]:
                        for node in m.graph.node:
                            if node.op_type == "Constant" and inp in node.output:
                                for a in node.attribute:
                                    if a.name == "value":
                                        v = numpy_from_onnx(a.t)
                                        if cmin is None:
                                            cmin = float(v.flat[0])
                                        elif cmax is None:
                                            cmax = float(v.flat[0])
                clip = (cmin, cmax)
                break
    return divisor, clip


def load_from_onnx(onnx_path):
    """Extract weights from a beta .onnx into a ShapQNet.

    Heuristic mapping (verified on ddqn_dda_sidang1.onnx):
      * running_mean  : [6] initializer named ``*running_mean``.
      * divisor       : [6] initializer feeding the normalization Div node
                        (== sqrt(running_var + eps)). Recovered as var=div**2.
      * eps           : 0.0 (baked into the divisor; sqrt(var+0) == divisor).
      * clip range    : read from the normalization Clip node (default -5,5).
      * linears       : 2D initializers shaped (128,6), (128,128), (5,128)
                        in PyTorch [out,in] layout; transposes if [in,out].
      * biases        : 1D initializers shaped (128,), (128,), (5,).

    Raises KeyError if mapping fails. Caller should fall back to KernelExplainer.
    """
    inits, m = _initializers(onnx_path)

    # --- normalization running_mean + divisor ---
    mean = None
    mean_name = None
    for name, arr in inits.items():
        if arr.shape == (6,) and ("running_mean" in name or "mean" in name):
            mean = arr
            mean_name = name
            break
    if mean is None:
        sixes = [(n, a) for n, a in inits.items() if a.shape == (6,)]
        if sixes:
            mean, mean_name = sixes[0]
    if mean is None:
        raise KeyError("running_mean [6] initializer not found")

    divisor, clip = _find_norm_divisor(m, mean_name)
    if divisor is None:
        # Do NOT guess by squaring an arbitrary [6] initializer: a future model
        # that stores running_var directly (no precomputed Div divisor) would
        # be silently misinterpreted as a divisor -> var = running_var**2
        # (squaring variance, badly wrong). Fail loudly instead; the caller
        # falls back to Task 5's KernelExplainer-on-ONNX path.
        raise KeyError(
            "no norm Div divisor found in onnx graph and running_var is not "
            "stored directly; cannot recover normalization safely"
        )

    var = (divisor.astype(np.float32) ** 2)
    eps = 0.0  # eps is baked into the precomputed divisor.
    clip_min, clip_max = clip if clip[0] is not None and clip[1] is not None else (-5.0, 5.0)

    # --- linears: 6->128, 128->128, 128->5 ---
    two_d = [(n, a) for n, a in inits.items() if a.ndim == 2]
    w1 = w2 = w3 = None
    b1 = b2 = b3 = None
    # Bias candidates by shape.
    bias128 = [a for n, a in inits.items() if a.shape == (128,)]
    bias5 = [a for n, a in inits.items() if a.shape == (5,)]

    for n, a in two_d:
        s = a.shape
        if s == (128, 6):
            w1 = a
        elif s == (6, 128):
            w1 = a.T
        elif s == (128, 128):
            w2 = a
        elif s == (5, 128):
            w3 = a
        elif s == (128, 5):
            w3 = a.T
    if w1 is None or w2 is None or w3 is None:
        raise KeyError(f"linears not found; 2D shapes seen: {[a.shape for _, a in two_d]}")

    # Match biases by name when possible (fall back to position).
    def _pick_bias(candidates, keyword, fallback_default):
        for n, a in inits.items():
            if a.shape == (fallback_default,) and keyword in n:
                return a
        return candidates[0] if candidates else np.zeros(fallback_default, np.float32)

    b1 = _pick_bias(bias128, "seq_layers.0.bias", 128)
    b2 = _pick_bias(bias128, "seq_layers.2.bias", 128)
    b3 = _pick_bias(bias5, "extrinsic.bias", 5)

    net = ShapQNet(mean, var, eps, w1=w1, b1=b1, w2=w2, b2=b2, w3=w3, b3=b3,
                   clip_min=clip_min, clip_max=clip_max)
    return net


# ---------------------------------------------------------------------------
# ONNX inference + faithfulness
# ---------------------------------------------------------------------------

def _value_head_output_name(m):
    """Find the value-head Gemm output name (the [N,5] Q-value tensor)."""
    # Prefer the ML-Agents naming convention. The "value_heads" path component
    # lives in the node's *output tensor name* (e.g. /network_body/value_heads/
    # extrinsic/Gemm_output_0), NOT the node name field (often blank in torch
    # exports), so we inspect n.output[0].
    for n in m.graph.node:
        if n.op_type == "Gemm" and "value_heads" in (n.output[0] if n.output else ""):
            return n.output[0]
    # Fallback: the last Gemm feeding an Unsqueeze/ArgMax (action head).
    gemm_outs = [n.output[0] for n in m.graph.node if n.op_type == "Gemm"]
    for n in m.graph.node:
        if n.op_type in ("ArgMax", "Unsqueeze") and n.input:
            if n.input[0] in gemm_outs:
                return n.input[0]
    if gemm_outs:
        return gemm_outs[-1]
    raise KeyError("value-head Gemm output not found in onnx graph")


def onnx_inference(onnx_path, states_np):
    """Run the deployed .onnx for Q-values [N,5]. Spec §9.1.

    The ML-Agents export only exposes argmax action outputs ([N,1]); the raw
    Q-values are an intermediate tensor. We append the value-head Gemm output
    to the graph outputs and return it.
    """
    m = onnx.load(onnx_path)
    target = _value_head_output_name(m)
    # Add the intermediate as a graph output so onnxruntime will return it.
    out_names = [o.name for o in m.graph.output]
    if target not in out_names:
        vi = helper.make_tensor_value_info(target, TensorProto.FLOAT,
                                           ["batch", 5])
        m.graph.output.append(vi)
    sess = ort.InferenceSession(m.SerializeToString(),
                                providers=["CPUExecutionProvider"])
    in_name = sess.get_inputs()[0].name
    outs = sess.run([target], {in_name: states_np.astype(np.float32)})
    q = np.asarray(outs[0], dtype=np.float32)
    if q.ndim != 2 or q.shape[1] != 5:
        raise KeyError(f"value-head output not [N,5]; got shape {q.shape}")
    return q


def check_faithfulness(shap_net, onnx_path, states_np, tol=1e-4):
    """Compare ShapQNet outputs against the deployed .onnx on the same states."""
    q_torch = shap_net(torch.as_tensor(states_np, dtype=torch.float32)).detach().numpy()
    q_onnx = onnx_inference(onnx_path, states_np)
    q_torch = q_torch.reshape(q_onnx.shape)
    diff = float(np.max(np.abs(q_torch - q_onnx)))
    return {"max_diff": diff, "passed": diff < tol,
            "q_torch_shape": q_torch.shape, "q_onnx_shape": q_onnx.shape}