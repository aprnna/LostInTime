"""
Custom network modules using ReLU activation instead of Swish.

ML-Agents hardcodes Swish (x * sigmoid(x)) in LinearEncoder and ConditionalEncoder.
This module provides drop-in replacements that use nn.ReLU, so the DDQN plugin
doesn't need to patch the installed mlagents package.
"""
from typing import List, Optional, Tuple, Dict

from mlagents.torch_utils import torch, nn
from mlagents_envs.base_env import ActionSpec, ObservationSpec, ObservationType
from mlagents.trainers.settings import (
    NetworkSettings,
    EncoderType,
    ConditioningType,
)
from mlagents.trainers.torch_entities.layers import (
    linear_layer,
    Initialization,
    LSTM,
    LayerNorm,
)
from mlagents.trainers.torch_entities.encoders import VectorInput
from mlagents.trainers.torch_entities.decoders import ValueHeads
from mlagents.trainers.torch_entities.utils import ModelUtils
from mlagents.trainers.torch_entities.attention import (
    EntityEmbedding,
    ResidualSelfAttention,
    get_zero_entities_mask,
)
from mlagents.trainers.torch_entities.networks import ObservationEncoder
from mlagents.trainers.buffer import AgentBuffer
from mlagents.trainers.trajectory import ObsUtil


# ---------------------------------------------------------------------------
# ReLU-based LinearEncoder (replaces Swish with ReLU)
# ---------------------------------------------------------------------------

class ReLULinearEncoder(torch.nn.Module):
    """Same as mlagents LinearEncoder but uses nn.ReLU instead of Swish."""

    def __init__(
        self,
        input_size: int,
        num_layers: int,
        hidden_size: int,
        kernel_init: Initialization = Initialization.KaimingHeNormal,
        kernel_gain: float = 1.0,
    ):
        super().__init__()
        self.layers = [
            linear_layer(
                input_size,
                hidden_size,
                kernel_init=kernel_init,
                kernel_gain=kernel_gain,
            )
        ]
        self.layers.append(nn.ReLU())
        for _ in range(num_layers - 1):
            self.layers.append(
                linear_layer(
                    hidden_size,
                    hidden_size,
                    kernel_init=kernel_init,
                    kernel_gain=kernel_gain,
                )
            )
            self.layers.append(nn.ReLU())
        self.seq_layers = torch.nn.Sequential(*self.layers)

    def forward(self, input_tensor: torch.Tensor) -> torch.Tensor:
        return self.seq_layers(input_tensor)


# ---------------------------------------------------------------------------
# ReLU-based ConditionalEncoder (replaces Swish with ReLU in HyperNetwork too)
# ---------------------------------------------------------------------------

class _ReLUConditionalEncoder(torch.nn.Module):
    """Same as mlagents ConditionalEncoder but uses nn.ReLU instead of Swish."""

    def __init__(
        self,
        input_size: int,
        goal_size: int,
        hidden_size: int,
        num_layers: int,
        num_conditional_layers: int,
        kernel_init: Initialization = Initialization.KaimingHeNormal,
        kernel_gain: float = 1.0,
    ):
        super().__init__()
        layers = []
        prev_size = input_size
        for i in range(num_layers):
            if num_layers - i <= num_conditional_layers:
                layers.append(
                    _ReLUHyperNetwork(
                        prev_size, hidden_size, goal_size, hidden_size, 2
                    )
                )
            else:
                layers.append(
                    linear_layer(
                        prev_size,
                        hidden_size,
                        kernel_init=kernel_init,
                        kernel_gain=kernel_gain,
                    )
                )
            layers.append(nn.ReLU())
            prev_size = hidden_size
        self.layers = torch.nn.ModuleList(layers)

    def forward(
        self, input_tensor: torch.Tensor, goal_tensor: torch.Tensor
    ) -> torch.Tensor:
        activation = input_tensor
        for layer in self.layers:
            if isinstance(layer, _ReLUHyperNetwork):
                activation = layer(activation, goal_tensor)
            else:
                activation = layer(activation)
        return activation


class _ReLUHyperNetwork(torch.nn.Module):
    """Same as mlagents HyperNetwork but uses nn.ReLU instead of Swish."""

    def __init__(
        self,
        input_size,
        output_size,
        hyper_input_size,
        layer_size,
        num_layers,
    ):
        super().__init__()
        import math

        self.input_size = input_size
        self.output_size = output_size

        layer_in_size = hyper_input_size
        layers = []
        for _ in range(num_layers):
            layers.append(
                linear_layer(
                    layer_in_size,
                    layer_size,
                    kernel_init=Initialization.KaimingHeNormal,
                    kernel_gain=1.0,
                    bias_init=Initialization.Zero,
                )
            )
            layers.append(nn.ReLU())
            layer_in_size = layer_size
        flat_output = linear_layer(
            layer_size,
            input_size * output_size,
            kernel_init=Initialization.KaimingHeNormal,
            kernel_gain=0.1,
            bias_init=Initialization.Zero,
        )

        bound = math.sqrt(1 / (layer_size * self.input_size))
        flat_output.weight.data.uniform_(-bound, bound)

        self.hypernet = torch.nn.Sequential(*layers, LayerNorm(), flat_output)
        self.bias = torch.nn.Parameter(torch.zeros(output_size))

    def forward(self, input_activation, hyper_input):
        output_weights = self.hypernet(hyper_input)
        output_weights = output_weights.view(-1, self.input_size, self.output_size)
        result = (
            torch.bmm(input_activation.unsqueeze(1), output_weights).squeeze(1)
            + self.bias
        )
        return result


# ---------------------------------------------------------------------------
# ReLU-based NetworkBody
# ---------------------------------------------------------------------------

class ReLUNetworkBody(nn.Module):
    """Same as mlagents NetworkBody but uses ReLULinearEncoder / _ReLUConditionalEncoder."""

    def __init__(
        self,
        observation_specs: List[ObservationSpec],
        network_settings: NetworkSettings,
        encoded_act_size: int = 0,
    ):
        super().__init__()
        self.normalize = network_settings.normalize
        self.use_lstm = network_settings.memory is not None
        self.h_size = network_settings.hidden_units
        self.m_size = (
            network_settings.memory.memory_size
            if network_settings.memory is not None
            else 0
        )

        # Reuse the standard ObservationEncoder (visual / vector preprocessing)
        self.observation_encoder = ObservationEncoder(
            observation_specs,
            self.h_size,
            network_settings.vis_encode_type,
            self.normalize,
        )
        self.processors = self.observation_encoder.processors
        total_enc_size = self.observation_encoder.total_enc_size
        total_enc_size += encoded_act_size

        if (
            self.observation_encoder.total_goal_enc_size > 0
            and network_settings.goal_conditioning_type == ConditioningType.HYPER
        ):
            self._body_encoder = _ReLUConditionalEncoder(
                total_enc_size,
                self.observation_encoder.total_goal_enc_size,
                self.h_size,
                network_settings.num_layers,
                1,
            )
        else:
            self._body_encoder = ReLULinearEncoder(
                total_enc_size, network_settings.num_layers, self.h_size
            )

        if self.use_lstm:
            self.lstm = LSTM(self.h_size, self.m_size)
        else:
            self.lstm = None  # type: ignore

    def update_normalization(self, buffer: AgentBuffer) -> None:
        self.observation_encoder.update_normalization(buffer)

    def copy_normalization(self, other_network: "ReLUNetworkBody") -> None:
        self.observation_encoder.copy_normalization(
            other_network.observation_encoder
        )

    @property
    def memory_size(self) -> int:
        return self.lstm.memory_size if self.use_lstm else 0

    def forward(
        self,
        inputs: List[torch.Tensor],
        actions: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        encoded_self = self.observation_encoder(inputs)
        if actions is not None:
            encoded_self = torch.cat([encoded_self, actions], dim=1)
        if isinstance(self._body_encoder, _ReLUConditionalEncoder):
            goal = self.observation_encoder.get_goal_encoding(inputs)
            encoding = self._body_encoder(encoded_self, goal)
        else:
            encoding = self._body_encoder(encoded_self)

        if self.use_lstm:
            encoding = encoding.reshape([-1, sequence_length, self.h_size])
            encoding, memories = self.lstm(encoding, memories)
            encoding = encoding.reshape([-1, self.m_size // 2])
        return encoding, memories


# ---------------------------------------------------------------------------
# ReLU-based ValueNetwork
# ---------------------------------------------------------------------------

class ReLUValueNetwork(nn.Module):
    """Same as mlagents ValueNetwork but backed by ReLUNetworkBody (ReLU activation)."""

    def __init__(
        self,
        stream_names: List[str],
        observation_specs: List[ObservationSpec],
        network_settings: NetworkSettings,
        encoded_act_size: int = 0,
        outputs_per_stream: int = 1,
    ):
        nn.Module.__init__(self)
        self.network_body = ReLUNetworkBody(
            observation_specs,
            network_settings,
            encoded_act_size=encoded_act_size,
        )
        if network_settings.memory is not None:
            encoding_size = network_settings.memory.memory_size // 2
        else:
            encoding_size = network_settings.hidden_units
        self.value_heads = ValueHeads(stream_names, encoding_size, outputs_per_stream)

    def update_normalization(self, buffer: AgentBuffer) -> None:
        self.network_body.update_normalization(buffer)

    @property
    def memory_size(self) -> int:
        return self.network_body.memory_size

    def critic_pass(
        self,
        inputs: List[torch.Tensor],
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        value_outputs, critic_mem_out = self.forward(
            inputs, memories=memories, sequence_length=sequence_length
        )
        return value_outputs, critic_mem_out

    def forward(
        self,
        inputs: List[torch.Tensor],
        actions: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        encoding, memories = self.network_body(
            inputs, actions, memories, sequence_length
        )
        output = self.value_heads(encoding)
        return output, memories