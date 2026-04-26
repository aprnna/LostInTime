"""
DDQN Policy wrapper for ML-Agents.
"""
import torch
import numpy as np
from typing import Dict, Any, List, Optional

from mlagents_envs.base_env import DecisionSteps, BehaviorSpec, ActionTuple
from mlagents.trainers.policy import Policy
from mlagents.trainers.settings import NetworkSettings, TrainerSettings
from mlagents.trainers.action_info import ActionInfo
from mlagents.trainers.behavior_id_utils import get_global_agent_id

from .ddqn_network import QNetwork


class DDQNPolicy(Policy):
    """
    DDQN Policy implementation for ML-Agents.
    Wraps QNetwork to provide action selection for discrete actions.
    """

    def __init__(
        self,
        seed: int,
        behavior_spec: BehaviorSpec,
        network_settings: NetworkSettings,
        trainer_settings: TrainerSettings,
    ):
        """
        Initialize DDQN Policy.

        :param seed: Random seed
        :param behavior_spec: Behavior specifications (obs/action sizes)
        :param network_settings: Network configuration
        :param trainer_settings: Trainer hyperparameters
        """
        super().__init__(seed, behavior_spec, network_settings)

        # Extract network parameters from settings
        hidden_units = network_settings.hidden_units
        num_layers = network_settings.num_layers

        # Get observation size from behavior spec
        obs_specs = behavior_spec.observation_specs
        self._obs_size = sum(spec.shape[0] for spec in obs_specs)

        # Get action size from behavior spec (discrete)
        action_spec = behavior_spec.action_spec
        if action_spec.discrete_size > 0:
            self._action_size = action_spec.discrete_branches[0]
        else:
            # Fallback to 3 actions for DDA
            self._action_size = 3

        # Create Q-networks (online and target)
        self.q_network = QNetwork(
            observation_size=self._obs_size,
            action_size=self._action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )

        self.target_network = QNetwork(
            observation_size=self._obs_size,
            action_size=self._action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )

        # Copy weights to target network
        self.target_network.load_state_dict(self.q_network.state_dict())

        # Epsilon for exploration (will be updated by trainer)
        self.epsilon = 1.0

        # Device
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.q_network.to(self.device)
        self.target_network.to(self.device)

        # Set random seed
        torch.manual_seed(seed)
        np.random.seed(seed)

    def get_action(
        self,
        decision_requests: DecisionSteps,
        worker_id: int = 0,
    ) -> ActionInfo:
        """
        Get actions for the given observations.

        :param decision_requests: DecisionSteps containing observations
        :param worker_id: Worker ID
        :return: ActionInfo containing the selected action
        """
        if len(decision_requests) == 0:
            return ActionInfo.empty()

        # Get observations
        obs = decision_requests.obs[0]  # Shape: (num_agents, obs_size)
        num_agents = obs.shape[0]

        obs_tensor = torch.tensor(obs, dtype=torch.float32, device=self.device)

        # Select actions using epsilon-greedy
        with torch.no_grad():
            if np.random.random() > self.epsilon:
                q_values = self.q_network(obs_tensor)
                actions = q_values.argmax(dim=-1).cpu().numpy()
            else:
                # Random exploration
                actions = np.random.randint(0, self._action_size, size=num_agents)

        # Create ActionTuple for discrete action
        action_tuple = ActionTuple()
        action_tuple.add_discrete(actions.reshape(-1, 1))

        # Get agent IDs
        global_agent_ids = [
            get_global_agent_id(worker_id, int(agent_id))
            for agent_id in decision_requests.agent_id
        ]

        return ActionInfo(
            action=action_tuple,
            env_action=action_tuple,
            outputs={"q_values": q_values.cpu().numpy() if self.epsilon < 1.0 else None},
            agent_ids=list(decision_requests.agent_id),
        )

    def update_target_network(self, tau: float = 1.0) -> None:
        """
        Soft update target network using Polyak averaging.

        :param tau: Soft update coefficient (1.0 = hard copy, 0.005 = soft)
        """
        for target_param, online_param in zip(
            self.target_network.parameters(),
            self.q_network.parameters(),
        ):
            target_param.data.copy_(
                tau * online_param.data + (1 - tau) * target_param.data
            )

    def save_checkpoint(self, checkpoint_path: str) -> None:
        """
        Save model checkpoint.

        :param checkpoint_path: Path to save checkpoint
        """
        import os
        os.makedirs(os.path.dirname(checkpoint_path) if os.path.dirname(checkpoint_path) else ".", exist_ok=True)
        full_path = os.path.join(checkpoint_path, "ddqn_model.pt") if os.path.isdir(checkpoint_path) else checkpoint_path
        torch.save(
            {
                "q_network": self.q_network.state_dict(),
                "target_network": self.target_network.state_dict(),
                "epsilon": self.epsilon,
            },
            full_path,
        )

    def load_checkpoint(self, checkpoint_path: str) -> None:
        """
        Load model checkpoint.

        :param checkpoint_path: Path to load checkpoint
        """
        import os
        full_path = os.path.join(checkpoint_path, "ddqn_model.pt") if os.path.isdir(checkpoint_path) else checkpoint_path

        checkpoint = torch.load(full_path, map_location=self.device)
        self.q_network.load_state_dict(checkpoint["q_network"])
        self.target_network.load_state_dict(checkpoint["target_network"])
        if "epsilon" in checkpoint:
            self.epsilon = checkpoint["epsilon"]

    def get_weights(self) -> Dict[str, np.ndarray]:
        """Get model weights for syncing."""
        return {k: v.cpu().numpy() for k, v in self.q_network.state_dict().items()}

    def set_weights(self, weights: Dict[str, np.ndarray]) -> None:
        """Set model weights from external source."""
        state_dict = {k: torch.tensor(v) for k, v in weights.items()}
        self.q_network.load_state_dict(state_dict)
        self.target_network.load_state_dict(state_dict)