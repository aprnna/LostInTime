"""
DDQN Policy wrapper for ML-Agents.
"""
import torch
import numpy as np
from typing import Dict, Any, List
from mlagents.trainers.policy import Policy
from mlagents.trainers.processor import ProcessorManager
from mlagents_envs.base_env import DecisionSteps, TerminalSteps

from .ddqn_network import QNetwork


class DDQNPolicy(Policy):
    """
    DDQN Policy implementation for ML-Agents.
    Wraps QNetwork to provide action selection.
    """

    def __init__(
        self,
        seed: int,
        behavior_name: str,
        network_settings: Dict[str, Any],
        action_specs,
        trainer_settings: Dict[str, Any],
    ):
        super().__init__(seed, behavior_name, network_settings, trainer_settings)

        # Extract network parameters
        hidden_units = network_settings.get("hidden_units", 128)
        num_layers = network_settings.get("num_layers", 2)

        # Get observation and action sizes from specs
        obs_size = self._get_observation_size()
        action_size = self._get_action_size()

        # Create Q-networks (online and target)
        self.q_network = QNetwork(
            observation_size=obs_size,
            action_size=action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )

        self.target_network = QNetwork(
            observation_size=obs_size,
            action_size=action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )

        # Copy weights to target network
        self.target_network.load_state_dict(self.q_network.state_dict())

        # Epsilon for exploration
        self.epsilon = trainer_settings.get("exploration_initial_eps", 1.0)

        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.q_network.to(self.device)
        self.target_network.to(self.device)

    def _get_observation_size(self) -> int:
        """Extract total observation size from specs."""
        # Default DDA state size: 4
        return 4

    def _get_action_size(self) -> int:
        """Extract action size from specs."""
        # Default DDA actions: 3 (Maintain, Increase, Decrease)
        return 3

    def get_action(
        self,
        decision_requests: DecisionSteps,
        worker_id: int = 0,
        deterministic: bool = False,
    ) -> np.ndarray:
        """
        Get actions for the given observations.

        Args:
            decision_requests: DecisionSteps containing observations
            worker_id: Worker ID (unused in DDQN)
            deterministic: If True, always take greedy action

        Returns:
            np.ndarray of shape (num_agents, 1) containing actions
        """
        obs = decision_requests.obs[0]  # Shape: (num_agents, obs_size)
        obs_tensor = torch.tensor(obs, dtype=torch.float32, device=self.device)

        with torch.no_grad():
            if deterministic or np.random.random() > self.epsilon:
                q_values = self.q_network(obs_tensor)
                actions = q_values.argmax(dim=-1).cpu().numpy()
            else:
                # Random exploration
                actions = np.random.randint(0, self._get_action_size(), size=len(obs))

        return actions.reshape(-1, 1)

    def update_target_network(self, tau: float = 1.0):
        """
        Soft update target network using Polyak averaging.

        Args:
            tau: Soft update coefficient (1.0 = hard copy, 0.005 = soft)
        """
        for target_param, online_param in zip(
            self.target_network.parameters(),
            self.q_network.parameters(),
        ):
            target_param.data.copy_(
                tau * online_param.data + (1 - tau) * target_param.data
            )

    def save_checkpoint(self, checkpoint_path: str):
        """Save model checkpoint."""
        torch.save(
            {
                "q_network": self.q_network.state_dict(),
                "target_network": self.target_network.state_dict(),
            },
            checkpoint_path,
        )

    def load_checkpoint(self, checkpoint_path: str):
        """Load model checkpoint."""
        checkpoint = torch.load(checkpoint_path, map_location=self.device)
        self.q_network.load_state_dict(checkpoint["q_network"])
        self.target_network.load_state_dict(checkpoint["target_network"])

    def get_weights(self):
        """Get model weights for syncing."""
        return {k: v.cpu().numpy() for k, v in self.q_network.state_dict().items()}

    def set_weights(self, weights: Dict[str, np.ndarray]):
        """Set model weights from external source."""
        state_dict = {k: torch.tensor(v) for k, v in weights.items()}
        self.q_network.load_state_dict(state_dict)
        self.target_network.load_state_dict(state_dict)