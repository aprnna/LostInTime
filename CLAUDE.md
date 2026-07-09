# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 2D turn-based battle game ("Lost In Time") with ML-Agents DDQN-based Dynamic Difficulty Adjustment (DDA). Built for research/thesis (skripsi) comparing ML-based DDA against baseline approaches.

## Tech Stack

- **Engine**: Unity 2022+ (URP rendering)
- **Async**: UniTask for async/await patterns
- **Backend**: PlayFab (Client API) for session/battle logging
- **ML**: Unity ML-Agents + custom DDQN trainer plugin (`mlagents-plugin-ddqn`)
- **UI**: Unity UI (ugui)

## Commands

### ML-Agents Training
```bash
# Install dependencies
pip install -r requirements.txt

# Install custom DDQN plugin
cd ml_agents_plugin && pip install -e .

# Train DDA agent (run Unity Editor DDA ML Agents scene + Play)
mlagents-learn config/ddqn.yaml --run-id=<run-name> --train

# Train with parallel environments (faster)
mlagents-learn config/ddqn.yaml --run-id=<run-name> --train --num-envs=4
```

Training results saved to `results/<run-name>/`. Models: `.onnx` for inference, `.pt` for checkpoint.

### Unity Editor
- Tests: Window → General → Test Runner (EditMode/PlayMode)
- CLI build: `Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode`

### Training Logs
Training event logs written to file at:
```
C:\Users\<User>\AppData\LocalLow\SaltStudio\Lost In Time\DDA_Training\training_YYYYMMDD_HHMMSS.log
```
Managed by `TrainingLogger` (static, file-buffered, flushes every 10 entries).

## Core Architecture

### Manager Layer (Singletons)
- `GameManager` - Global game state, session management (PersistentSingleton, persists across scenes)
- `BattleSystem` - Battle FSM controller, orchestrates all battle states (scene-level singleton)
- `MapSystem` - Node-based map navigation (singleton)
- `RouletteSystem` - Damage/value randomization
- `MinigameManager` - TapZone minigame flow
- `AudioManager` - Sound playback via SoundSO ScriptableObject
- `InputManager` - Action maps (Player/World/Minigames modes)

### Battle System (Finite State Machine)
States in `Assets/Scripts/Manager/State/`:
```
PlayerTurnState → SelectActionState → SelectEnemyState → DamageRouletteState → CriticalAttackState → EnemyTurnState → ResultBattleState
```
FSM pattern: `GameState` abstract base (`OnEnter()`, `OnUpdate()`, `OnExit()`) + `FiniteStateMachine<T>` generic from `Assets/Scripts/State/`.

### DDA System (ML-Agents + DDQN)

**Episode structure**: 1 episode = 1 full run (12 areas). `EndEpisode()` only in `OnRunEnd()`.

**Decision flow** (critical for credit assignment):
```
Area 0 (Battle): ApplyDifficulty(default=Normal) → Battle → OnAreaComplete → AddReward(0) [baseline skip] → RequestDecision
                                                                                                          ↓ Academy processes
Area 1 (Rest):   ProcessRest → UpdateBattlePhase → no decision
Area 2 (Battle): ApplyDifficulty(agent's action from Area 0) → Battle → OnAreaComplete → AddReward → RequestDecision
```
- First area: reward SKIPPED (baseline, no agent action caused it)
- Area N reward attributes to action chosen after Area N-1
- `RequestDecision()` only after battle areas (not Rest/Shop)

**Agent** (`DDAAgent.cs`):
- 8 observations: HP Ratio, Turn Count, Player Level, Damage Dealt Ratio, QTE Accuracy, Resource Depletion, Area Progress, Current Difficulty
- 5 discrete actions: Very Easy (0), Easy (1), Normal (2), Hard (3), Very Hard (4)
- Reward: parabolic sweet spot at HP 50% (1.0 max), linear decay outside 40-60%, loss = -1.0, run bonus = +0.5/-0.1, death penalty = -0.5
- Terminal steps: area 12 completed (run won) or player dies (HP = 0)

**Difficulty** (`DifficultySettings.cs`):
- 5 levels: Very Easy (0.75x), Easy (0.875x), Normal (1.0x), Hard (1.125f), Very Hard (1.25x)
- Multipliers apply to both HP and Damage
- Agent changes difficulty by ±1 level per action

**Training simulator** (`TrainingBattleSimulator.cs`):
- `_instantMode = true`: entire run completes in ~2-3 frames (yields only before battle areas for Academy processing)
- `_instantMode = false`: coroutine-based with visual delays
- Smart AI for simulated player actions (target lowest HP enemy, weighted action selection)

**DDQN Plugin** (`ml_agents_plugin/`):
- `mlagents_plugin_ddqn/`: Custom trainer registered as `trainer_type: ddqn`
- Uses ReLU instead of Swish activation in `networks.py`
- Double Q-Learning: online network selects actions, target network evaluates Q-values
- Soft target update via tau=0.005
- Entry point: `get_type_and_setting()` in `__init__.py`

### Player/Enemy System
- `PlayerStats` - Player singleton (HP, shield, damage, XP, coins, level)
- `EnemyController` / `EnemyStats` - Enemy behavior and stats
- `BaseAction` - ScriptableObject base for player actions (attack, defend, heal)
- Actions loaded from `Resources/Player/Actions/` at initialization

### Map System
- `MapSystem` - Singleton managing node-based branching map
- `MapNode` - Node data (enemies, connections, drop items, map type)
- `MapType` enum: Enemy, Boss, Rest, Shop
- Branching: `BuildRandomPath()` traces random path through graph (~12 nodes per run from 22 total)

## Key Patterns

### PersistentSingleton
```csharp
public class GameManager : PersistentSingleton<GameManager>
```
Auto-instantiates from Resources or creates new GameObject. Persists across scenes.

### Standard Singleton
```csharp
public static BattleSystem Instance { get; private set; }
```
Scene-specific managers. Optional `DontDestroyOnLoad`.

### UniTask Async
```csharp
private async UniTask PrepareGame() {
    await Initialize();
    await InitializeActions();
}
```

### State Machine
```csharp
public class FiniteStateMachine<T> where T : IState {
    public void ChangeState(T newState) {
        _previousState.OnExit();
        _currentState = newState;
        _currentState.OnEnter();
    }
}
```

## File Conventions

| Directory | Purpose |
|-----------|---------|
| `Assets/Scripts/` | Core game logic by feature |
| `Assets/Scripts/DDA/` | DDA system (agent, simulator, logger, difficulty) |
| `Assets/Scripts/Manager/State/` | Battle FSM states |
| `Assets/Scripts/State/` | Generic FSM implementation |
| `Assets/Scenes/` | DDA ML Agents scene for training, Battle, MainMenu |
| `Assets/Resources/` | ScriptableObjects (GameData, Player/Actions, DDA/) |
| `config/` | ML-Agents training config (ddqn.yaml) |
| `results/` | Training output per run-id |
| `ml_agents_plugin/` | Custom DDQN trainer plugin |

## DDA Config (`config/ddqn.yaml`) — Key Settings

| Setting | Current | Notes |
|---------|---------|-------|
| `trainer_type` | `ddqn` | Custom plugin |
| `learning_rate` | 0.0001 | |
| `batch_size` | 128 | |
| `buffer_size` | 50000 | |
| `gamma` | 0.90 | |
| `exploration_decay_steps` | 350000 | |
| `max_steps` | 500000 | |
| `num_envs` | 1 | Bottleneck — increase to 4-8 for speed |
| `time_scale` | 100 | Unity time scale during training |
| `no_graphics` | true | Headless training |

## Git/Branch

- Main branch: `main`
- Recent work: DDQN training optimization, TrainingLogger integration, instant mode for TrainingBattleSimulator