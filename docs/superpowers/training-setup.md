# DDQN Training Setup

## Unity Scene Configuration

### 1. Create Training Scene

1. Duplicate `Assets/Scenes/DDA.unity` as `Assets/Scenes/DDA Training.unity`
2. Open the new scene

### 2. Add DDA Components

1. Create empty GameObject named `DDA System`
2. Add components:
   - `DDAAgent` (Agent from ML-Agents)
   - `DDAIntegration`
   - `DifficultyApplier`

3. Configure `DDAAgent`:
   - Behavior Parameters:
     - Behavior Name: `ddqn_dda`
     - Vector Observation: Size 4
     - Discrete Actions: Branch 0 Size 3
   - Decision Requester: Decision Period 10, Take Actions Between Decisions: true

4. Assign references:
   - Difficulty Settings: `Assets/Resources/DDA/DefaultDifficultySettings`
   - Training Mode: true

### 3. Run Training

```bash
# Install plugin
cd ml_agents_plugin
pip install -e .

# Start training
mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train

# Or with Unity Editor
# 1. Open DDA Training scene
# 2. Set Time Scale to 20 in Project Settings
# 3. Press Play
```

### 4. Monitor Training

```bash
# TensorBoard
tensorboard --logdir results/ddqn_dda_v1

# Metrics to watch:
# - Policy/ddqn_dda/Epsilon (should decay)
# - Policy/ddqn_dda/BufferSize (should fill up)
# - Reward (should increase over time)
```

### 5. Inference

After training:

1. Copy `results/ddqn_dda_v1/ddqn_dda.onnx` to `Assets/ML-Agents/Models/`
2. In Unity Editor:
   - Select DDAAgent
   - Behavior Parameters > Model: ddqn_dda
   - Set Training Mode: false
3. Play game normally - agent adjusts difficulty automatically