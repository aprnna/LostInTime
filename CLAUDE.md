# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 2D turn-based battle game with ML-Agents based Dynamic Difficulty Adjustment (DDA). Built for research/thesis (skripsi) comparing ML-based DDA against baseline approaches.

## Tech Stack

- **Engine**: Unity 2022+ (URP rendering)
- **Async**: UniTask for async/await patterns
- **Backend**: PlayFab (Client API) for session/battle logging
- **ML**: Unity ML-Agents 4.0.3 + custom DDQN trainer (via `mlagents-ddqn-plugin`)
- **UI**: Unity UI (ugui)

## Core Architecture

### Manager Layer (Singletons)
- `GameManager` - Global game state, session management (PersistentSingleton)
- `BattleSystem` - Battle FSM controller, orchestrates all battle states
- `MapSystem` - Node-based map navigation
- `RouletteSystem` - Damage/value randomization
- `MinigameManager` - TapZone and other minigame flow
- `AudioManager` - Sound playback via SoundSO ScriptableObject
- `InputManager` - Action maps (Player/World/Minigames modes)

### Battle System (Finite State Machine)
States in `Assets/Scripts/Manager/State/`:
```
PlayerTurnState → SelectActionState → SelectEnemyState → DamageRouletteState → CriticalAttackState → EnemyTurnState → ResultBattleState
```

FSM pattern: `GameState` abstract base with `OnEnter()`, `OnUpdate()`, `OnExit()`. Uses `FiniteStateMachine<T>` generic implementation from `Assets/Scripts/State/`.

### DDA System (ML-Agents)
Uses Unity ML-Agents with custom DDQN trainer for difficulty adjustment. Training config in `config/ddqn.yaml`.

**Key components**:
- DDQN agent behavior configured via ML-Agents
- Training results stored in `results/` directory
- Battle logging via `BattleLogger` with DDA event hooks (`LogDDAEvent()`)

### Player/Enemy System
- `PlayerStats` - Player singleton (HP, shield, damage, XP, coins, level)
- `EnemyController` / `EnemyStats` - Enemy behavior and stats
- `BaseAction` - ScriptableObject base for player actions (attack, defend, heal)
- Actions loaded from `Resources/Player/Actions/` at initialization

### Map System
- `MapSystem` - Singleton managing node-based map
- `MapNode` - Node data (enemies, connections, drop items, map type)
- `MapType` enum: Battle, Rest, Shop, Boss, etc.
- Biome transitions via `BiomeController`

### Minigame System
- `MinigameManager` - Singleton managing minigame flow
- `TapZone` - Tap timing minigame (critical hits during `DamageRouletteState`)

### Data/Logging
- `BattleLogger` - Singleton tracking battle events, player actions, DDA events
- `SessionManager` - Session lifecycle management
- `PlayfabManager` - PlayFab API wrapper (login, data upload)

## Key Patterns

### PersistentSingleton
```csharp
public class GameManager : PersistentSingleton<GameManager>
```
Auto-instantiates from Resources or creates new GameObject. Used for managers that persist across scenes.

### Standard Singleton
```csharp
public static BattleSystem Instance { get; private set; }
```
Scene-specific managers use standard singleton pattern with `DontDestroyOnLoad` optional.

### UniTask Async
All initialization uses `async UniTask` with `await UniTask.Yield()`:
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

## Commands

### Unity Editor
- Open in Unity Hub → run Editor
- Tests: Window → General → Test Runner (EditMode/PlayMode)

### ML-Agents Training
```bash
# Install dependencies
pip install -r requirements.txt

# Install custom DDQN plugin (if using)
cd ml_agents_plugin && pip install -e .

# Train DDA agent
mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train
```

Training results saved to `results/<run-id>/`.

### Unity CLI Build
```bash
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform PlayMode
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode
```

## File Conventions

| Directory | Purpose |
|-----------|---------|
| `Assets/Scripts/` | Core game logic organized by feature |
| `Assets/Scenes/` | Unity scenes (MainMenu, Battle, DDA ML Agents, etc.) |
| `Assets/Resources/` | ScriptableObjects (GameData, Player/Actions) |
| `Assets/PlayFabSDK/` | PlayFab SDK integration |
| `Assets/Plugins/UniTask/` | Async library |
| `Assets/ML-Agents/` | ML-Agents timers |
| `config/` | ML-Agents training configs (ddqn.yaml) |
| `results/` | Training output (ddqn_v1/, ppo/) |

## Common Workflows

### Adding New Player Action
1. Create script inheriting `BaseAction` in `Assets/Scripts/Player/Item/`
2. Implement `Execute()`, `PlayVfx()`
3. Create ScriptableObject asset in `Assets/Resources/Player/Actions/`
4. Auto-loaded via `GameManager.InitializeActions()`

### Adding New Battle State
1. Create class inheriting `GameState` in `Assets/Scripts/Manager/State/`
2. Implement `OnEnter()`, `OnUpdate()`, `OnExit()`
3. Register in `BattleSystem.InitializeFSM()` and wire transitions via `StateMachine.ChangeState()`

### Adding New Map Node Type
1. Add type to `MapType` enum in `Assets/Scripts/MapSystem/MapType.cs`
2. Create corresponding scene (add to Build Settings)
3. Configure node in `MapData` ScriptableObject
4. Handle in `MapSystem.MovePlayerToNode()`

### Modifying Battle Logging
Edit `BattleLogger` in `Assets/Scripts/Playfab/BattleDataLogger.cs`:
- `OnPlayerTurn()` - Log player actions
- `OnEnemyTurn()` - Log enemy attacks
- `LogDDAEvent()` - Log DDA decisions for training analysis

## Git/Branch

- Main branch: `main`
- Recent work: Migration from Q-Learning to ML-Agents DDQN, BattleLogger DDA hooks integration
