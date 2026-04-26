from setuptools import setup, find_packages

setup(
    name="mlagents-plugin-ddqn",
    version="0.1.0",
    description="DDQN (Double Deep Q-Network) trainer plugin for ML-Agents",
    author="SpaceJam DDA Team",
    packages=find_packages(),
    install_requires=[
        "mlagents>=1.1.0",
        "torch>=2.2.0",
        "numpy>=1.23.0",
    ],
    python_requires=">=3.8",
    entry_points={
        "mlagents.trainer_type": [
            "ddqn = mlagents_plugin_ddqn:register_plugin"
        ]
    },
)