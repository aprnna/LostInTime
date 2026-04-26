"""
DDQN Trainer implementation for ML-Agents.
"""
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from typing import Dict, List, Any, Optional, cast

from mlagents_envs.base_env import BehaviorSpec
from mlagents_envs.logging_util import get_logger
from mlagents.trainers.trainer.trainer import Trainer
from mlagents.trainers.policy import Policy
from mlagents.trainers.trajectory import Trajectory
from mlagents.trainers.settings import TrainerSettings
from mlagents.trainers.behavior_id_utils import BehaviorIdentifiers

from .ddqn_network import QNetwork, ReplayBuffer
from .ddqn_policy import DDQNPolicy
from . import DDQNSettings

logger = get_logger(__name__)

TRAINER_NAME = "ddqn"


class DDQNTrainer(Trainer):
    """
    Double Deep Q-Network trainer for ML-Agents.

    Implements DDQN algorithm with:
    - Experience replay buffer
    - Target network with soft updates
    - Double Q-learning (online network for action selection, target for evaluation)
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
            trainer_settings,
            training,
            load,
            artifact_path,
            reward_buff_cap,
        )

        self.hyperparameters: DDQNSettings = cast(
            DDQNSettings, self.trainer_settings.hyperparameters
        )

        self.seed = seed
        self.policy: DDQNPolicy = None  # type: ignore

        # Training configuration from hyperparameters
        self.learning_rate = self.hyperparameters.learning_rate
        self.batch_size = self.hyperparameters.batch_size
        self.buffer_size = self.hyperparameters.buffer_size
        self.gamma = self.hyperparameters.gamma
        self.tau = self.hyperparameters.tau

        # Exploration schedule
        self.exploration_initial_eps = self.hyperparameters.exploration_initial_eps
        self.exploration_final_eps = self.hyperparameters.exploration_final_eps
        self.exploration_decay_steps = self.hyperparameters.exploration_decay_steps

        # Training state
        self._step = 0
        self._episode_rewards: List[float] = []

        # Initialize replay buffer
        self.replay_buffer = ReplayBuffer(capacity=self.buffer_size)

        # Loss function
        self.loss_fn = nn.MSELoss()

        # Device
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    def _get_current_epsilon(self) -> float:
        """
        Calculate current epsilon for exploration.
        Linear decay from initial to final.
        """
        if self._step >= self.exploration_decay_steps:
            return self.exploration_final_eps

        progress = self._step / self.exploration_decay_steps
        return self.exploration_initial_eps - progress * (
            self.exploration_initial_eps - self.exploration_final_eps
        )

    def create_policy(
        self,
        parsed_behavior_id: BehaviorIdentifiers,
        behavior_spec: BehaviorSpec,
    ) -> DDQNPolicy:
        """
        Creates a DDQN policy for this trainer.

        :param parsed_behavior_id: Parsed behavior identifier
        :param behavior_spec: Behavior specifications
        :return: DDQNPolicy instance
        """
        policy = DDQNPolicy(
            seed=self.seed,
            behavior_spec=behavior_spec,
            network_settings=self.trainer_settings.network_settings,
            trainer_settings=self.trainer_settings,
        )
        return policy

    def add_policy(
        self, parsed_behavior_id: BehaviorIdentifiers, policy: Policy
    ) -> None:
        """
        Adds policy to trainer.

        :param parsed_behavior_id: Parsed behavior identifier
        :param policy: Policy to add
        """
        self.policy = policy
        self.policies[parsed_behavior_id.behavior_id] = policy

        # Initialize optimizer now that policy exists
        self.optimizer = optim.Adam(
            self.policy.q_network.parameters(),
            lr=self.learning_rate,
        )

    def save_model(self) -> None:
        """Saves model file(s) for the policy associated with this trainer."""
        if self.policy is not None:
            self.policy.save_checkpoint(self.artifact_path)

    def end_episode(self) -> None:
        """Signal that episode has ended. Reset episode-specific state."""
        pass

    def save_model_checkpoint(self, checkpoint_path: str) -> None:
        """Save model checkpoint."""
        if self.policy is not None:
            self.policy.save_checkpoint(checkpoint_path)

    def load_model_checkpoint(self, checkpoint_path: str) -> None:
        """Load model checkpoint."""
        if self.policy is not None:
            self.policy.load_checkpoint(checkpoint_path)

    def _process_trajectory(self, trajectory: Trajectory) -> None:
        """
        Process a trajectory and store experiences in replay buffer.

        :param trajectory: The trajectory to process
        """
        # Extract steps from trajectory
        agent_buffer = trajectory.to_agentbuffer()

        # Get observations, actions, rewards
        n_obs = len(self.policy.behavior_spec.observation_specs)
        obs_list = [agent_buffer[f"obs_{i}"] for i in range(n_obs)]

        # Convert to tensors
        states = torch.tensor(obs_list[0], dtype=torch.float32, device=self.device)

        actions = np.array(agent_buffer["actions"])
        rewards = np.array(agent_buffer["rewards"])

        # Get next observations
        next_obs_list = [trajectory.next_obs[i] for i in range(n_obs)]
        next_states = torch.tensor(
            np.array(next_obs_list[0]), dtype=torch.float32, device=self.device
        )

        done = trajectory.done_reached

        # Store transitions
        for i in range(len(rewards)):
            state_i = states[i] if states.dim() > 1 else states
            next_state_i = next_states[i] if next_states.dim() > 1 else next_states

            self.replay_buffer.push(
                state=state_i,
                action=int(actions[i] if isinstance(actions[i], (int, np.integer)) else actions[i][0]),
                reward=float(rewards[i]),
                next_state=next_state_i,
                done=done,
            )

        # Track episode rewards
        if done:
            total_reward = np.sum(rewards)
            self._episode_rewards.append(total_reward)
            self._reward_buffer.append(total_reward)

    def update_policy(self) -> Dict[str, float]:
        """
        Update Q-network using sampled batch from replay buffer.
        DDQN: Use online network for action selection, target for evaluation.

        :return: Dictionary of loss values for logging
        """
        # Check if enough samples
        if not self.replay_buffer.is_ready(self.batch_size):
            return {}

        # Sample batch
        batch = self.replay_buffer.sample(self.batch_size)

        states = batch["states"].to(self.device)
        actions = batch["actions"].to(self.device)
        rewards = batch["rewards"].to(self.device)
        next_states = batch["next_states"].to(self.device)
        dones = batch["dones"].to(self.device)

        # Compute current Q values
        q_values = self.policy.q_network(states)
        current_q = q_values.gather(1, actions.unsqueeze(1)).squeeze(1)

        # DDQN: Use online network to select best action
        with torch.no_grad():
            next_q_online = self.policy.q_network(next_states)
            next_actions = next_q_online.argmax(dim=1)

            # Use target network to evaluate the action
            next_q_target = self.policy.target_network(next_states)
            next_q_values = next_q_target.gather(1, next_actions.unsqueeze(1)).squeeze(1)

            # Compute target Q values
            target_q = rewards + self.gamma * next_q_values * (1 - dones)

        # Compute loss
        loss = self.loss_fn(current_q, target_q)

        # Optimize
        self.optimizer.zero_grad()
        loss.backward()
        torch.nn.utils.clip_grad_norm_(self.policy.q_network.parameters(), 1.0)
        self.optimizer.step()

        # Soft update target network
        self.policy.update_target_network(tau=self.tau)

        self._step += 1

        return {"Losses/DDQN Loss": loss.item()}

    def advance(self) -> None:
        """Advance training step and update epsilon."""
        if self.policy is not None:
            self.policy.epsilon = self._get_current_epsilon()

    def get_step(self) -> int:
        """Get current training step."""
        return self._step

    @property
    def get_max_steps(self) -> int:
        """Get maximum training steps."""
        return self.trainer_settings.max_steps

    @staticmethod
    def get_trainer_name() -> str:
        """Get trainer name for registration."""
        return TRAINER_NAME