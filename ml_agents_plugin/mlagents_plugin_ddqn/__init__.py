"""
DDQN (Double Deep Q-Network) plugin for Unity ML-Agents.

This plugin provides a custom trainer implementing DDQN algorithm
for Dynamic Difficulty Adjustment in games.
"""

from .ddqn_trainer import DDQNTrainer, register_ddqn_trainer
from .ddqn_network import QNetwork
from .ddqn_policy import DDQNPolicy

__all__ = [
    "DDQNTrainer",
    "DDQNPolicy",
    "QNetwork",
    "register_ddqn_trainer",
]

__version__ = "0.1.0"