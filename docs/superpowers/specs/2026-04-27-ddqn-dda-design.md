# DDQN-Based Dynamic Difficulty Adjustment (DDA) Design

**Date:** 2026-04-27
**Status:** Approved
**Scope:** Unity 2D turn-based RPG integration with ML-Agents DDQN trainer

## Overview

Implement Double Deep Q-Network (DDQN) for Dynamic Difficulty Adjustment using Unity ML-Agents. Agent learns to adjust enemy HP and damage multipliers to achieve flow state: ~60% win rate with player HP 40-60%.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Unity Game                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────┐  │
│  │ BattleSystem│───►│ DDAAgent   │───►│ DifficultyApplier│ │
│  │ (FSM)       │    │ (Agent)    │    │ (ScriptableObject)│ │
│  └─────────────┘    └─────┬──────┘    └─────────────────┘  │
│                           │                                 │
│  ┌─────────────┐    ┌─────▼──────┐                         │
│  │ BattleLogger│◄───│DDABehavior │                         │
│  └─────────────┘    └────────────┘                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ (gRPC via ML-Agents)
┌─────────────────────────────────────────────────────────────┐
│                    Python Training                          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              DDQNTrainer (Plugin)                      │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────────┐ │  │
│  │  │ Q-Network   │  │Target Net   │  │ Replay Buffer │ │  │
│  │  │ (128x128)   │  │ (128x128)   │  │ (10000)       │ │  │
│  │  └─────────────┘  └─────────────┘  └───────────────┘ │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## State Representation

Agent observes 4 continuous values (normalized 0-1):

| State | Description | Calculation |
|-------|-------------|-------------|
| `hp_ratio` | Player survivability | `playerEndHP / playerStartHP` (after battle) |
| `damage_ratio` | Combat pressure | `damageDealt / playerStartHP` |
| `turn_normalized` | Efficiency | `turnCount / expectedTurnsPerBattle` |
| `difficulty_normalized` | Current difficulty | `currentLevel / maxLevel` |

**No discretization** — DDQN neural network handles continuous inputs directly.

## Action Space

3 discrete actions:

| Action | Effect |
|--------|--------|
| `0` (Maintain) | Keep current difficulty level |
| `1` (Increase) | Move to next difficulty level |
| `2` (Decrease) | Move to previous difficulty level |

Actions apply to both HP and damage multipliers simultaneously.

## Difficulty Levels

5 discrete levels:

| Level | Multiplier | Name |
|-------|------------|------|
| 0 | 0.75x | Very Easy |
| 1 | 0.875x | Easy |
| 2 | 1.0x | Normal |
| 3 | 1.125x | Hard |
| 4 | 1.25x | Very Hard |

Start at level 2 (Normal) by default.

## Reward Function

Shaped reward with multiple components:

### Win Condition
```python
if playerWon:
    reward = 1.0  # Base win reward
    
    # HP Target bonus (flow state: 40-60% HP)
    hpRatio = playerEndHP / playerStartHP
    
    if 0.4 <= hpRatio <= 0.6:
        # Maximum bonus at 50% HP
        distanceFromOptimal = abs(hpRatio - 0.5)
        hpBonus = 0.5 * (1.0 - distanceFromOptimal * 2)
        reward += hpBonus
    elif hpRatio > 0.6:
        # Too easy - penalty
        reward -= 0.1 * (hpRatio - 0.6)
    elif hpRatio < 0.4:
        # Too hard - penalty
        reward -= 0.1 * (0.4 - hpRatio)
else:
    reward = -1.0  # Loss penalty

# Efficiency penalty
if turnCount > expectedTurns:
    reward -= 0.01 * (turnCount - expectedTurns)

reward = clamp(reward, -2.0, 2.0)
```

### Reward Summary

| Condition | Reward |
|-----------|--------|
| Win | +1.0 |
| Win + HP 40-60% | +0.3 to +0.5 |
| Win + HP > 60% | -0.1 per 10% over |
| Win + HP < 40% | -0.1 per 10% under |
| Loss | -1.0 |
| Turns > expected | -0.01 per extra turn |

**Goal:** Agent learns to tune difficulty so players win with moderate HP (flow state).

## Integration Points

### DDAAgent.cs
- Extends `Agent` from ML-Agents
- `CollectObservations()` — Gather state from `BattleLogger`, `PlayerStats`
- `OnActionReceived()` — Apply difficulty via `DifficultySettings`
- `OnBattleEnd()` — Calculate reward, end episode

### DifficultySettings.cs
- ScriptableObject asset
- Stores current difficulty level (0-4)
- Exposes `HPMultiplier`, `DamageMultiplier` properties
- `IncreaseDifficulty()`, `DecreaseDifficulty()` methods

### DifficultyApplier.cs
- Applies multipliers to `EnemyStats` on spawn
- Hooks into `BattleSystem.SpawnEnemies()`

### DDAIntegration.cs
- Coordinates agent decision before battle
- Receives battle end event for reward calculation

## Python DDQN Plugin

### File Structure
```
ml_agents_plugin/
├── setup.py
├── mlagents_plugin_ddqn/
│   ├── __init__.py          # Plugin registration
│   ├── ddqn_trainer.py      # DDQNTrainer class
│   ├── ddqn_policy.py       # Policy wrapper
│   └── ddqn_network.py      # Q-Network architecture
```

### DDQNTrainer Key Features
- **Experience Replay Buffer:** 10,000 capacity
- **Target Network:** Soft updates (τ=0.005)
- **Double Q-Learning:** Online network selects action, target evaluates
- **Loss:** MSE for Q-value regression
- **Learning Rate:** 0.0003 (linear schedule)
- **Batch Size:** 64
- **Discount (γ):** 0.99

## Training Workflow

### Phase 1: Offline Training
1. Open `DDA ML Agents.unity` scene
2. Run: `mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train`
3. Training runs 500k steps
4. Checkpoints saved every 10k steps
5. Final model: `results/ddqn_v1/ddqn_dda.onnx`

### Phase 2: Inference
1. Load trained ONNX model into `DDAAgent`
2. `RequestDecision()` called before each battle
3. Agent outputs difficulty action
4. `DifficultyApplier` applies multipliers to enemies
5. Battle runs normally

### Phase 3: Online Update (Optional)
1. Collect battle logs from players
2. Fine-tune model offline
3. Deploy updated model via content update

## Files to Create

```
Assets/Scripts/DDA/
├── DDAAgent.cs              # ML-Agents Agent
├── DifficultySettings.cs    # ScriptableObject
├── DifficultyApplier.cs     # Apply to EnemyStats
└── DDAIntegration.cs        # BattleSystem hooks

ml_agents_plugin/
├── setup.py
├── mlagents_plugin_ddqn/
│   ├── __init__.py
│   ├── ddqn_trainer.py
│   ├── ddqn_policy.py
│   └── ddqn_network.py

config/
└── ddqn.yaml                # Update existing

Assets/Scenes/
└── DDA ML Agents.unity      # Training scene
```

## Implementation Order

1. **Unity Agent (Phase 1)**
   - `DifficultySettings.cs`
   - `DifficultyApplier.cs`
   - `DDAAgent.cs`
   - `DDAIntegration.cs`

2. **Python Plugin (Phase 2)**
   - `ddqn_network.py`
   - `ddqn_policy.py`
   - `ddqn_trainer.py`
   - `setup.py`

3. **Training Setup (Phase 3)**
   - Update `config/ddqn.yaml`
   - Create training scene
   - Add fast battle simulator

4. **Testing (Phase 4)**
   - Heuristic mode testing
   - Training run
   - Inference testing

## Success Criteria

- Agent achieves ~60% win rate for players
- Player HP after win falls in 40-60% range consistently
- Difficulty adjustments feel natural (no wild swings)
- Model converges within 500k training steps