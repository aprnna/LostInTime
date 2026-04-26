# DDA Training Setup Guide

## Quick Setup (Unity Editor)

1. Open scene `Assets/Scenes/DDA ML Agents.unity`
2. Go to menu: **DDA > Setup Training Scene**
3. Go to menu: **DDA > Create Training UI Prefab** (optional, for visual display)
4. Go to menu: **DDA > Validate Setup** to confirm everything is ready

## Manual Setup

### 1. Create DifficultySettings Asset
- Right-click in Project window: `Assets/Resources/DDA/`
- Create > DDA > DifficultySettings
- Name it `DefaultDifficultySettings`

### 2. Scene Components

**TrainingBattleSimulator** GameObject:
- `TrainingBattleSimulator.cs`
- `DifficultySettings` reference
- `DDAAgent` reference (from scene)

**DDAAgent** GameObject:
- `DDAAgent.cs`
- **Behavior Parameters** (ML-Agents component)
  - Behavior Name: `ddqn_dda`
  - Space Type: Discrete
  - Discrete Actions: 3 (Maintain, Increase, Decrease)
- **Decision Requester** (ML-Agents component)
  - Decision Period: 1
  - Take Actions Between Decisions: false

**DifficultyApplier** GameObject:
- `DifficultyApplier.cs`
- `DifficultySettings` reference

**TrainingUI** (optional):
- Canvas with `TrainingUIDisplay.cs`
- UI Text elements for Episode, Difficulty, Win Rate, Reward
- HP Sliders for Player/Enemy
- Stats text area

## Training

### Start Training Simulation
1. Press Play in Unity Editor
2. Battles run automatically
3. Watch the TrainingUIDisplay for:
   - Episode count increasing
   - Win rate approaching 60%
   - Difficulty adjusting based on performance
   - Reward values

### Start ML-Agents Training (Python)
```bash
# Terminal
mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1

# Unity: Press Play
```

### Monitor Learning
- Episode count
- Win rate (target: ~60%)
- Difficulty level changes
- Average reward (should increase over time)
- Flow state indicator (target: "🎯 FLOW STATE")

## Parameters

### TrainingBattleSimulator
- `playerBaseHP`: 100 (adjust for balance)
- `playerBaseDamage`: 20
- `enemyBaseHP`: 80
- `enemyBaseDamage`: 15
- `playerAccuracy`: 0.85
- `enemyAccuracy`: 0.80
- `battleDelay`: 0.1s (speed of battles)
- `turnDelay`: 0.05s (speed of turns)
- `playerSkill`: 0.0-1.0 (random → optimal)
- `useSmartPlayer`: enable skill-based damage

### DDAAgent
- `expectedTurnsPerBattle`: 10
- `isTrainingMode`: true (for training)

## Troubleshooting

**"No DifficultySettings assigned"**
- Run: DDA > Create DifficultySettings Asset
- Assign to TrainingBattleSimulator and DifficultyApplier

**"Trainer type ddqn not found"**
- Run: `cd ml_agents_plugin && pip install -e .`
- Verify: `mlagents-learn --help` should show ddqn as option

**Agent not making decisions**
- Check Behavior Parameters: Behavior Name matches config
- Check Decision Requester is attached
- Check `isTrainingMode` = true on DDAAgent