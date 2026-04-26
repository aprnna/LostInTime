"""
DDQN Trainer implementation for ML-Agents.
"""
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from typing import Dict, List, Any, Optional
from mlagents.trainers.trainer import Trainer
from mlagents.trainers.policy import Policy
from mlagents_envs.base_env import BehaviorSpec

from .ddqn_network import QNetwork, ReplayBuffer
from .ddqn_policy import DDQNPolicy


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
        reward_signal_specs,
        policy: DDQNPolicy,
        trainer_settings: Dict[str, Any],
        training: bool = True,
        artifact_path: str = "",
        init_path: Optional[str] = None,
        checkpoint_interval: int = 10000,
    ):
        super().__init__(
            behavior_name,
            reward_signal_specs,
            policy,
            trainer_settings,
            training,
            artifact_path,
            init_path,
        )

        # Training configuration
        hyperparams = trainer_settings.get("hyperparameters", {})

        self.learning_rate = hyperparams.get("learning_rate", 0.0003)
        self.batch_size = hyperparams.get("batch_size", 64)
        self.buffer_size = hyperparams.get("buffer_size", 10000)
        self.gamma = hyperparams.get("gamma", 0.99)
        self.tau = hyperparams.get("tau", 0.005)  # Soft update rate
        self.target_update_interval = hyperparams.get("target_update_interval", 10000)

        # Exploration schedule
        self.exploration_initial_eps = hyperparams.get("exploration_initial_eps", 1.0)
        self.exploration_final_eps = hyperparams.get("exploration_final_eps", 0.01)
        self.exploration_decay_steps = hyperparams.get("exploration_decay_steps", 50000)

        # Training state
        self.step = 0
        self.episode_rewards: List[float] = []
        self.last_reward = 0.0

        # Initialize replay buffer
        self.replay_buffer = ReplayBuffer(capacity=self.buffer_size)

        # Initialize optimizer
        self.optimizer = optim.Adam(
            self.policy.q_network.parameters(),
            lr=self.learning_rate,
        )

        # Loss function
        self.loss_fn = nn.MSELoss()

        self.checkpoint_interval = checkpoint_interval

        # Device
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    def _get_current_epsilon(self) -> float:
        """
        Calculate current epsilon for exploration.
        Linear decay from initial to final.
        """
        if self.step >= self.exploration_decay_steps:
            return self.exploration_final_eps

        progress = self.step / self.exploration_decay_steps
        return self.exploration_initial_eps - progress * (
            self.exploration_initial_eps - self.exploration_final_eps
        )

    def process_experiences(
        self,
        current_info,
        new_info,
        actions: np.ndarray,
        rewards: np.ndarray,
        dones: np.ndarray,
    ):
        """
        Process experiences from environment and store in replay buffer.

        Args:
            current_info: Current observations
            new_info: Next observations
            actions: Actions taken
            rewards: Rewards received
            dones: Episode termination flags
        """
        # Convert observations to tensors
        states = torch.tensor(
            current_info.obs[0], dtype=torch.float32, device=self.device
        )
        next_states = torch.tensor(
            new_info.obs[0], dtype=torch.float32, device=self.device
        )

        # Store transitions
        for i in range(len(rewards)):
            self.replay_buffer.push(
                state=states[i],
                action=int(actions[i]),
                reward=float(rewards[i]),
                next_state=next_states[i],
                done=bool(dones[i]),
            )

            # Track episode rewards
            if dones[i]:
                self.episode_rewards.append(float(rewards[i]))

    def update_policy(self):
        """
        Update Q-network using sampled batch from replay buffer.
        DDQN: Use online network for action selection, target for evaluation.
        """
        # Check if enough samples
        if not self.replay_buffer.is_ready(self.batch_size):
            return

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

        self.step += 1

    def advance(self):
        """Advance training step."""
        # Update epsilon
        self.policy.epsilon = self._get_current_epsilon()

    def add_policy_stats(
        self,
        stats: Dict[str, Any],
        policy_name: str,
    ) -> Dict[str, Any]:
        """Add policy statistics for logging."""
        stats[f"Policy/{policy_name}/Epsilon"] = self.policy.epsilon
        stats[f"Policy/{policy_name}/BufferSize"] = len(self.replay_buffer)
        stats[f"Policy/{policy_name}/LearningRate"] = self.learning_rate
        return stats

    def get_module_names(self) -> List[str]:
        """Get list of module names for checkpointing."""
        return ["q_network", "target_network"]

    def save_model_checkpoint(self, checkpoint_path: str):
        """Save model checkpoint."""
        self.policy.save_checkpoint(checkpoint_path)

    def load_model_checkpoint(self, checkpoint_path: str):
        """Load model checkpoint."""
        self.policy.load_checkpoint(checkpoint_path)


def register_ddqn_trainer():
    """
    Register DDQN trainer with ML-Agents.
    Called via entry point in setup.py.
    """
    from mlagents.trainers import register_trainer

    register_trainer("ddqn", DDQNTrainer)

    # Also register the policy
    from mlagents.trainers import register_policy

    register_policy("ddqn", DDQNPolicy)