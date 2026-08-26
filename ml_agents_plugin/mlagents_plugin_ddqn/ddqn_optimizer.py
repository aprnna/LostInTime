"""
DDQN Optimizer for ML-Agents.
Based on DQN optimizer with Double Q-Learning extension.
"""
from typing import cast, Dict, List, Tuple, Optional
from mlagents.torch_utils import torch, nn, default_device
from mlagents.trainers.optimizer.torch_optimizer import TorchOptimizer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.buffer import AgentBuffer, BufferKey, RewardSignalUtil
from mlagents_envs.timers import timed
from .networks import QNetworkDDQN  # single source of truth
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.utils import ModelUtils
from mlagents.trainers.trajectory import ObsUtil
from mlagents.trainers.settings import TrainerSettings, OffPolicyHyperparamSettings
from mlagents.trainers.settings import ScheduleType
import attr


@attr.s(auto_attribs=True)
class DDQNSettings(OffPolicyHyperparamSettings):
    """DDQN hyperparameters."""
    gamma: float = 0.99
    exploration_schedule: ScheduleType = ScheduleType.LINEAR
    exploration_initial_eps: float = 1.0
    exploration_final_eps: float = 0.01
    exploration_decay_steps: int = 20000  # Steps to decay epsilon
    target_update_interval: int = 10000
    tau: float = 0.005  # Soft update coefficient
    steps_per_update: float = 1
    save_replay_buffer: bool = False
    reward_signal_steps_per_update: float = attr.ib()

    @reward_signal_steps_per_update.default
    def _reward_signal_steps_per_update_default(self):
        return self.steps_per_update


class DDQNOptimizer(TorchOptimizer):
    """
    Double DQN Optimizer.
    Uses target network for value estimation to reduce overestimation.
    """

    def __init__(self, policy: TorchPolicy, trainer_settings: TrainerSettings):
        super().__init__(policy, trainer_settings)

        params = list(self.policy.actor.parameters())
        self.optimizer = torch.optim.Adam(
            params, lr=self.trainer_settings.hyperparameters.learning_rate
        )
        self.stream_names = list(self.reward_signals.keys())
        self.gammas = [_val.gamma for _val in trainer_settings.reward_signals.values()]
        self.use_dones_in_backup = {
            name: int(not self.reward_signals[name].ignore_done)
            for name in self.stream_names
        }

        self.hyperparameters: DDQNSettings = cast(
            DDQNSettings, trainer_settings.hyperparameters
        )
        self.tau = self.hyperparameters.tau
        self.decay_learning_rate = ModelUtils.DecayedValue(
            self.hyperparameters.learning_rate_schedule,
            self.hyperparameters.learning_rate,
            1e-10,
            self.trainer_settings.max_steps,
        )

        self.decay_exploration_rate = ModelUtils.DecayedValue(
            self.hyperparameters.exploration_schedule,
            self.hyperparameters.exploration_initial_eps,
            self.hyperparameters.exploration_final_eps,
            self.hyperparameters.exploration_decay_steps,
        )

        # Initialize Target Q-network for DDQN
        self.q_net_target = QNetworkDDQN(
            stream_names=self.reward_signals.keys(),
            observation_specs=policy.behavior_spec.observation_specs,
            network_settings=policy.network_settings,
            action_spec=policy.behavior_spec.action_spec,
        )
        # Move target network to same device as policy actor
        self.q_net_target.to(default_device())
        # Copy weights from online network
        ModelUtils.soft_update(self.policy.actor, self.q_net_target, 1.0)

    @property
    def critic(self):
        return self.q_net_target

    @timed
    def update(self, batch: AgentBuffer, num_sequences: int) -> Dict[str, float]:
        """
        Performs DDQN update on model.
        DDQN formula: y = r + γ * Q_target(s', argmax_a Q_online(s', a))
        - Online network selects action from NEXT state
        - Target network evaluates Q-value for that action
        """
        decay_lr = self.decay_learning_rate.get_value(self.policy.get_current_step())
        exp_rate = self.decay_exploration_rate.get_value(self.policy.get_current_step())
        self.policy.actor.exploration_rate = exp_rate

        rewards = {}
        for name in self.reward_signals:
            rewards[name] = ModelUtils.list_to_tensor(
                batch[RewardSignalUtil.rewards_key(name)]
            )

        n_obs = len(self.policy.behavior_spec.observation_specs)
        current_obs = ObsUtil.from_buffer(batch, n_obs)
        current_obs = [ModelUtils.list_to_tensor(obs) for obs in current_obs]

        next_obs = ObsUtil.from_buffer_next(batch, n_obs)
        next_obs = [ModelUtils.list_to_tensor(obs) for obs in next_obs]

        actions = AgentAction.from_buffer(batch)
        dones = ModelUtils.list_to_tensor(batch[BufferKey.DONE])

        # Get current Q-values from online network for current state
        current_q_values, _ = self.policy.actor.critic_pass(
            current_obs, sequence_length=self.policy.sequence_length
        )

        qloss = []
        with torch.no_grad():
            # DDQN Key: Use ONLINE network to select actions from NEXT state
            next_q_online, _ = self.policy.actor.critic_pass(
                next_obs, sequence_length=self.policy.sequence_length
            )
            # Get greedy actions from online network (next state)
            greedy_actions = self.policy.actor.get_greedy_action(next_q_online)

            # Use TARGET network to evaluate Q-values for next state
            next_q_target, _ = self.q_net_target.critic_pass(
                next_obs, sequence_length=self.policy.sequence_length
            )

        for name_i, name in enumerate(rewards.keys()):
            with torch.no_grad():
                # DDQN: Target Q = r + γ * Q_target(s', argmax_a Q_online(s', a))
                # Use target network to evaluate the action selected by online network
                next_q_values = torch.gather(
                    next_q_target[name], dim=1, index=greedy_actions
                ).squeeze()
                target_q_values = rewards[name] + (
                    (1.0 - self.use_dones_in_backup[name] * dones)
                    * self.gammas[name_i]
                    * next_q_values
                )
                target_q_values = target_q_values.reshape(-1, 1)

            # Current Q from online network
            curr_q = torch.gather(
                current_q_values[name], dim=1, index=actions.discrete_tensor
            )
            qloss.append(torch.nn.functional.smooth_l1_loss(curr_q, target_q_values))

        loss = torch.mean(torch.stack(qloss))
        ModelUtils.update_learning_rate(self.optimizer, decay_lr)
        self.optimizer.zero_grad()
        loss.backward()
        self.optimizer.step()

        # Soft update target network: θ_target = τ * θ_online + (1-τ) * θ_target
        ModelUtils.soft_update(self.policy.actor, self.q_net_target, self.tau)

        update_stats = {
            "Losses/Value Loss": loss.item(),
            "Policy/Learning Rate": decay_lr,
            "Policy/epsilon": exp_rate,
        }

        for reward_provider in self.reward_signals.values():
            update_stats.update(reward_provider.update(batch))
        return update_stats

    def get_modules(self):
        modules = {
            "Optimizer:value_optimizer": self.optimizer,
            "Optimizer:critic": self.critic,
        }
        for reward_provider in self.reward_signals.values():
            modules.update(reward_provider.get_modules())
        return modules
