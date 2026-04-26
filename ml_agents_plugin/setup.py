from setuptools import setup, find_packages

setup(
    name="mlagents-plugin-ddqn",
    version="0.1.0",
    description="DDQN (Double Deep Q-Network) trainer plugin for ML-Agents",
    author="SpaceJam DDA Team",
    packages=find_packages(),
    install_requires=[
        "mlagents>=2.0.0",
        "torch>=2.0.0",
        "numpy>=1.24.0",
    ],
    python_requires=">=3.8",
    entry_points={
        "mlagents.trainers": [
            "ddqn = mlagents_plugin_ddqn.ddqn_trainer:register_ddqn_trainer"
        ]
    },
)