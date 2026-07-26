"""
Re-export ONNX from existing checkpoint with the deterministic output fix.

Bug fixed: in QNetworkDDQN.forward(), disc_action_out and deterministic_disc_action_out
were swapped. Unity InferenceOnly reads the deterministic slot, which was previously
filled with get_random_action() (torch.randint), causing the agent to always output
random actions regardless of what the model learned.

Usage:
    conda run -n mlagents python export_onnx_fixed.py
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ml_agents_plugin'))

import torch
from mlagents.torch_utils import default_device
from mlagents_plugin_ddqn.networks import QNetworkDDQN
from mlagents.trainers.settings import NetworkSettings
from mlagents_envs.base_env import ActionSpec, ObservationSpec, ObservationType, DimensionProperty

CHECKPOINT_PATH = "results/ddqn_dda5/ddqn_dda/checkpoint.pt"
OUTPUT_ONNX     = "Assets/Resources/DDA/Models/ddqn_dda5.onnx"
RUN_ID          = "ddqn_dda5"

print("=" * 60)
print(f"Re-exporting ONNX from: {CHECKPOINT_PATH}")
print(f"Output:                  {OUTPUT_ONNX}")
print("=" * 60)

# --- Build network (must match training config exactly) ---
obs_specs = [ObservationSpec(
    shape=(6,),
    dimension_property=(DimensionProperty.NONE,),
    observation_type=ObservationType.DEFAULT,
    name='VectorSensor_size6'
)]
net_settings = NetworkSettings(
    normalize=True,
    hidden_units=128,
    num_layers=2,
)
action_spec = ActionSpec.create_discrete((5,))

net = QNetworkDDQN(
    stream_names=['extrinsic'],
    observation_specs=obs_specs,
    network_settings=net_settings,
    action_spec=action_spec,
)
net.to(default_device())

# --- Load weights from checkpoint ---
ckpt = torch.load(CHECKPOINT_PATH, map_location=default_device())
policy_state = ckpt['Policy']
net.load_state_dict(policy_state)
net.eval()

print(f"Loaded checkpoint: global_step={ckpt.get('global_step', '?')}")

# --- Verify the fix: run greedy forward on a sample observation ---
sample_obs = [torch.zeros(1, 6, device=default_device())]
with torch.no_grad():
    out = net.forward(sample_obs)
    # out = (version, memory_size, disc_action_out, discrete_act_size, deterministic_disc)
    disc_stochastic   = out[2].item()   # random (stochastic slot)
    disc_deterministic = out[4].item()  # greedy argmax (deterministic slot) — the one Unity uses
print(f"Sample zero-obs → stochastic={disc_stochastic}, deterministic={disc_deterministic}")
print(f"  (Unity InferenceOnly reads the DETERMINISTIC slot: action={disc_deterministic})")

# --- Export to ONNX ---
os.makedirs(os.path.dirname(OUTPUT_ONNX), exist_ok=True)
with torch.no_grad():
    torch.onnx.export(
        net,
        (sample_obs,),          # example input
        OUTPUT_ONNX,
        opset_version=9,
        input_names=['obs_0'],
        output_names=['version_number', 'memory_size',
                      'discrete_actions', 'discrete_action_output_shape',
                      'deterministic_discrete_actions'],
        dynamic_axes={
            'obs_0': {0: 'batch'},
            'discrete_actions': {0: 'batch'},
            'deterministic_discrete_actions': {0: 'batch'},
        },
        verbose=False,
    )

print(f"\n✅ ONNX exported to: {OUTPUT_ONNX}")
print("   Import into Unity: right-click asset → Reimport")
print("   The deterministic output now contains the GREEDY argmax action.")
print("   Unity InferenceOnly will correctly use the trained policy.")
