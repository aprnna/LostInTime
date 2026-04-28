"""
DDQN Trainer implementation for ML-Agents.
Based on DQN trainer with Double Q-Learning extension.
"""
from typing import cast

import numpy as np
from mlagents_envs.logging_util import get_logger
from mlagents.trainers.buffer import BufferKey
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.trainer.off_policy_trainer import OffPolicyTrainer
from mlagents.trainers.optimizer.torch_optimizer import TorchOptimizer
from mlagents.trainers.trajectory import Trajectory, ObsUtil
from mlagents.trainers.behavior_id_utils import BehaviorIdentifiers
from mlagents_envs.base_env import BehaviorSpec
from mlagents.trainers.settings import TrainerSettings
from .ddqn_optimizer import DDQNOptimizer, DDQNSettings, QNetworkDDQN

logger = get_logger(__name__)
TRAINER_NAME = "ddqn"


class DDQNTrainer(OffPolicyTrainer):
    """
    Double Deep Q-Network trainer for ML-Agents.

    Implements DDQN algorithm:
    - Uses online network for action selection
    - Uses target network for value estimation
    - Reduces overestimation bias compared to standard DQN
    """

    def __init__(
        self,
        behavior_name: str,
        reward_buff_cap: int,
        trainer_settings: TrainerSettings,
        training: bool,
        load: bool,
        seed: int,
        artifact_path: str,
    ):
        """
        Initialize DDQN trainer.

        :param behavior_name: The name of the behavior associated with trainer config
        :param reward_buff_cap: Max reward history to track
        :param trainer_settings: The parameters for the trainer
        :param training: Whether the trainer is set for training
        :param load: Whether the model should be loaded
        :param seed: The seed the model will be initialized with
        :param artifact_path: The directory for storing artifacts
        """
        super().__init__(
            behavior_name,
            reward_buff_cap,
            trainer_settings,
            training,
            load,
            seed,
            artifact_path,
        )
        self.policy: TorchPolicy = None  # type: ignore
        self.optimizer: DDQNOptimizer = None  # type: ignore

    def _process_trajectory(self, trajectory: Trajectory) -> None:
        """
        Takes a trajectory and processes it, putting it into the replay buffer.
        """
        super()._process_trajectory(trajectory)
        last_step = trajectory.steps[-1]
        agent_id = trajectory.agent_id

        agent_buffer_trajectory = trajectory.to_agentbuffer()
        self._warn_if_group_reward(agent_buffer_trajectory)

        # Update normalization
        if self.is_training:
            self.policy.actor.update_normalization(agent_buffer_trajectory)
            self.optimizer.critic.update_normalization(agent_buffer_trajectory)

        # Evaluate all reward functions for reporting
        self.collected_rewards["environment"][agent_id] += np.sum(
            agent_buffer_trajectory[BufferKey.ENVIRONMENT_REWARDS]
        )
        for name, reward_signal in self.optimizer.reward_signals.items():
            evaluate_result = (
                reward_signal.evaluate(agent_buffer_trajectory) * reward_signal.strength
            )
            self.collected_rewards[name][agent_id] += np.sum(evaluate_result)

        # Get value estimates for reporting
        (
            value_estimates,
            _,
            value_memories,
        ) = self.optimizer.get_trajectory_value_estimates(
            agent_buffer_trajectory, trajectory.next_obs, trajectory.done_reached
        )
        if value_memories is not None:
            agent_buffer_trajectory[BufferKey.CRITIC_MEMORY].set(value_memories)

        for name, v in value_estimates.items():
            self._stats_reporter.add_stat(
                f"Policy/{self.optimizer.reward_signals[name].name.capitalize()} Value",
                np.mean(v),
            )

        # Handle interrupted trajectories
        if last_step.interrupted:
            last_step_obs = last_step.obs
            for i, obs in enumerate(last_step_obs):
                agent_buffer_trajectory[ObsUtil.get_name_at_next(i)][-1] = obs
            agent_buffer_trajectory[BufferKey.DONE][-1] = False

        self._append_to_update_buffer(agent_buffer_trajectory)

        if trajectory.done_reached:
            self._update_end_episode_stats(agent_id, self.optimizer)

    def create_optimizer(self) -> TorchOptimizer:
        """Creates the DDQN optimizer."""
        return DDQNOptimizer(
            cast(TorchPolicy, self.policy),
            self.trainer_settings
        )

    def create_policy(
        self, parsed_behavior_id: BehaviorIdentifiers, behavior_spec: BehaviorSpec
    ) -> TorchPolicy:
        """
        Creates a policy with PyTorch backend and DDQN hyperparameters.

        :param parsed_behavior_id: Parsed behavior identifier
        :param behavior_spec: Specifications for policy construction
        :return: TorchPolicy instance
        """
        exploration_initial_eps = cast(
            DDQNSettings, self.trainer_settings.hyperparameters
        ).exploration_initial_eps
        actor_kwargs = {
            "exploration_initial_eps": exploration_initial_eps,
            "stream_names": [
                signal.value for signal in self.trainer_settings.reward_signals.keys()
            ],
        }
        policy = TorchPolicy(
            self.seed,
            behavior_spec,
            self.trainer_settings.network_settings,
            actor_cls=QNetworkDDQN,
            actor_kwargs=actor_kwargs,
        )
        self.maybe_load_replay_buffer()
        return policy

    @staticmethod
    def get_settings_type():
        return DDQNSettings

    @staticmethod
    def get_trainer_name() -> str:
        return TRAINER_NAME


def get_type_and_setting():
    """Entry point for ML-Agents trainer registration."""
    return (
        {DDQNTrainer.get_trainer_name(): DDQNTrainer},
        {DDQNTrainer.get_trainer_name(): DDQNTrainer.get_settings_type()}
    )