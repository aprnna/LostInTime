"""
DDQN Plugin for ML-Agents.
Provides Double Deep Q-Network trainer for DDA systems.
"""
import attr
from typing import Dict, Any

from mlagents.trainers.settings import HyperparamSettings, ScheduleType


@attr.s(auto_attribs=True)
class DDQNSettings(HyperparamSettings):
    """
    Hyperparameters for DDQN trainer.

    Inherits from HyperparamSettings and adds DDQN-specific parameters.
    """
    # DDQN-specific hyperparameters
    gamma: float = 0.99  # Discount factor
    tau: float = 0.005  # Soft update rate for target network
    target_update_interval: int = 1000  # Hard update interval (if tau=1.0)

    # Exploration settings
    exploration_initial_eps: float = 1.0
    exploration_final_eps: float = 0.01
    exploration_decay_steps: int = 50000

    # Override defaults from parent
    batch_size: int = 64
    buffer_size: int = 10000
    learning_rate: float = 3.0e-4


def register_plugin() -> tuple:
    """
    Register DDQN trainer with ML-Agents.

    This function is called via the mlagents.trainer_type entry point.
    Returns a tuple of (trainer_types_dict, trainer_settings_dict).
    """
    from mlagents_plugin_ddqn.ddqn_trainer import DDQNTrainer

    trainer_types = {
        "ddqn": DDQNTrainer,
    }

    trainer_settings = {
        "ddqn": DDQNSettings,
    }

    return trainer_types, trainer_settings


# Also export trainer and components for direct use
from .ddqn_trainer import DDQNTrainer
from .ddqn_network import QNetwork
from .ddqn_policy import DDQNPolicy

__all__ = [
    "DDQNTrainer",
    "DDQNPolicy",
    "QNetwork",
    "DDQNSettings",
    "register_plugin",
]

__version__ = "0.1.0"