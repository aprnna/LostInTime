"""
DDQN Plugin for ML-Agents.
Provides Double Deep Q-Network trainer for DDA systems.

Based on the official DQN implementation with Double Q-Learning extension:
- Uses target network for value estimation
- Reduces overestimation bias
- Follows ML-Agents standard patterns (OffPolicyTrainer, TorchPolicy)
"""
from .ddqn_trainer import DDQNTrainer, get_type_and_setting
from .ddqn_optimizer import DDQNSettings, DDQNOptimizer, QNetworkDDQN

__all__ = [
    "DDQNTrainer",
    "DDQNOptimizer",
    "QNetworkDDQN",
    "DDQNSettings",
    "get_type_and_setting",
]

__version__ = "0.2.0"