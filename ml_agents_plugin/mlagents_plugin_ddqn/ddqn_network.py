"""
Q-Network architecture for DDQN.
"""
import torch
import torch.nn as nn
from typing import List


class QNetwork(nn.Module):
    """
    Q-Network for DDQN with configurable hidden layers.

    Architecture:
        Input: State observation (4 continuous values)
        Hidden: 2 layers of 128 units each with ReLU
        Output: Q-values for each action (3 actions)
    """

    def __init__(
        self,
        observation_size: int = 4,
        action_size: int = 3,
        hidden_units: int = 128,
        num_layers: int = 2,
    ):
        super().__init__()

        self.observation_size = observation_size
        self.action_size = action_size
        self.hidden_units = hidden_units

        # Build layers
        layers = []

        # Input layer
        layers.append(nn.Linear(observation_size, hidden_units))
        layers.append(nn.ReLU())

        # Hidden layers
        for _ in range(num_layers - 1):
            layers.append(nn.Linear(hidden_units, hidden_units))
            layers.append(nn.ReLU())

        # Output layer (Q-values for each action)
        layers.append(nn.Linear(hidden_units, action_size))

        self.network = nn.Sequential(*layers)

        # Initialize weights
        self._initialize_weights()

    def _initialize_weights(self):
        """Initialize network weights using Xavier initialization."""
        for module in self.modules():
            if isinstance(module, nn.Linear):
                nn.init.xavier_uniform_(module.weight)
                nn.init.zeros_(module.bias)

    def forward(self, state: torch.Tensor) -> torch.Tensor:
        """
        Forward pass through the network.

        Args:
            state: State observation tensor of shape (batch_size, observation_size)
                   or (observation_size,) for single state

        Returns:
            Q-values tensor of shape (batch_size, action_size)
        """
        # Ensure correct shape
        if state.dim() == 1:
            state = state.unsqueeze(0)

        return self.network(state)

    def get_q_values(self, state: torch.Tensor) -> torch.Tensor:
        """Get Q-values for a state."""
        with torch.no_grad():
            return self.forward(state)

    def get_action(self, state: torch.Tensor, epsilon: float = 0.0) -> int:
        """
        Get action using epsilon-greedy policy.

        Args:
            state: State observation
            epsilon: Exploration rate (0 = greedy, 1 = random)

        Returns:
            Action index
        """
        if torch.rand(1).item() < epsilon:
            return torch.randint(0, self.action_size, (1,)).item()

        with torch.no_grad():
            q_values = self.forward(state)
            return q_values.argmax(dim=-1).item()


class ReplayBuffer:
    """
    Experience replay buffer for DDQN.
    Stores transitions and samples batches for training.
    """

    def __init__(self, capacity: int = 10000):
        self.capacity = capacity
        self.buffer = []
        self.position = 0

    def push(
        self,
        state: torch.Tensor,
        action: int,
        reward: float,
        next_state: torch.Tensor,
        done: bool,
    ):
        """Add transition to buffer."""
        if len(self.buffer) < self.capacity:
            self.buffer.append(None)

        self.buffer[self.position] = (state, action, reward, next_state, done)
        self.position = (self.position + 1) % self.capacity

    def sample(self, batch_size: int) -> dict:
        """
        Sample a batch of transitions.

        Returns:
            dict with tensors: states, actions, rewards, next_states, dones
        """
        import random
        indices = random.sample(range(len(self.buffer)), batch_size)

        states, actions, rewards, next_states, dones = zip(
            *[self.buffer[i] for i in indices]
        )

        return {
            "states": torch.stack(states),
            "actions": torch.tensor(actions, dtype=torch.long),
            "rewards": torch.tensor(rewards, dtype=torch.float32),
            "next_states": torch.stack(next_states),
            "dones": torch.tensor(dones, dtype=torch.float32),
        }

    def __len__(self):
        return len(self.buffer)

    def is_ready(self, batch_size: int) -> bool:
        """Check if buffer has enough samples."""
        return len(self.buffer) >= batch_size