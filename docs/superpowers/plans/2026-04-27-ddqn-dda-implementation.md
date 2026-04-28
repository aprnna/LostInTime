# DDQN-Based Dynamic Difficulty Adjustment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Double Deep Q-Network (DDQN) for Dynamic Difficulty Adjustment using Unity ML-Agents to achieve flow state gameplay (60% win rate, player HP 40-60%).

**Architecture:** Unity Agent collects battle state observations, Python DDQN trainer learns via experience replay with target network, inference applies difficulty multipliers to enemy stats before battle.

**Tech Stack:** Unity ML-Agents 4.0.3, PyTorch, C# Agent API, ScriptableObject for settings

---

## File Structure

```
Assets/Scripts/DDA/
├── DifficultySettings.cs    # ScriptableObject - stores difficulty levels
├── DifficultyApplier.cs     # Applies multipliers to EnemyStats
├── DDAAgent.cs              # ML-Agents Agent implementation
└── DDAIntegration.cs       # Coordinates with BattleSystem

ml_agents_plugin/
├── setup.py
└── mlagents_plugin_ddqn/
    ├── __init__.py
    ├── ddqn_network.py      # Q-Network architecture
    ├── ddqn_policy.py       # Policy wrapper for ML-Agents
    └── ddqn_trainer.py      # DDQNTrainer implementation

config/
└── ddqn.yaml                # Training configuration (update existing)
```

---

## Phase 1: Unity Agent

### Task 1: DifficultySettings ScriptableObject

**Files:**
- Create: `Assets/Scripts/DDA/DifficultySettings.cs`
- Create: `Assets/Scripts/DDA/DDA.asmdef` (assembly definition)

- [ ] **Step 1: Create DDA assembly definition**

Create `Assets/Scripts/DDA/DDA.asmdef`:

```json
{
    "name": "DDA",
    "rootNamespace": "DDA",
    "references": [
        "Unity.ML-Agents"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create DifficultySettings ScriptableObject**

Create `Assets/Scripts/DDA/DifficultySettings.cs`:

```csharp
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// ScriptableObject storing difficulty multipliers for DDA system.
    /// Supports 5 discrete levels: Very Easy (0.75x) to Very Hard (1.25x).
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "DDA/DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        [Header("Difficulty Levels")]
        [Tooltip("Multiplier values for each difficulty level")]
        [SerializeField] private float[] _difficultyLevels = { 0.75f, 0.875f, 1.0f, 1.125f, 1.25f };
        
        [Header("Current State")]
        [Tooltip("Current difficulty level index (0-4)")]
        [SerializeField] private int _currentLevelIndex = 2; // Start at Normal
        
        /// <summary>Current HP multiplier based on difficulty level.</summary>
        public float HPMultiplier => _difficultyLevels[_currentLevelIndex];
        
        /// <summary>Current damage multiplier based on difficulty level.</summary>
        public float DamageMultiplier => _difficultyLevels[_currentLevelIndex];
        
        /// <summary>Current difficulty level index (0-4).</summary>
        public int CurrentLevelIndex => _currentLevelIndex;
        
        /// <summary>Total number of difficulty levels.</summary>
        public int LevelCount => _difficultyLevels.Length;
        
        /// <summary>Increases difficulty by one level (max: Very Hard).</summary>
        public void IncreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Min(_currentLevelIndex + 1, _difficultyLevels.Length - 1);
        }
        
        /// <summary>Decreases difficulty by one level (min: Very Easy).</summary>
        public void DecreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Max(_currentLevelIndex - 1, 0);
        }
        
        /// <summary>Sets difficulty to specific level index.</summary>
        public void SetLevel(int levelIndex)
        {
            _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _difficultyLevels.Length - 1);
        }
        
        /// <summary>Resets difficulty to Normal (level 2).</summary>
        public void ResetToNormal()
        {
            _currentLevelIndex = 2;
        }
        
        /// <summary>Returns difficulty level normalized to 0-1 range.</summary>
        public float GetNormalizedDifficulty()
        {
            return (float)_currentLevelIndex / (_difficultyLevels.Length - 1);
        }
        
        /// <summary>Gets the difficulty level name for display.</summary>
        public string GetLevelName()
        {
            string[] names = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };
            return names[_currentLevelIndex];
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Reset to Normal")]
        private void ContextResetToNormal() => ResetToNormal();
        #endif
    }
}
```

- [ ] **Step 3: Create DifficultySettings asset**

In Unity Editor:
1. Right-click in `Assets/Resources/DDA/` folder
2. Create → DDA → DifficultySettings
3. Name it `DefaultDifficultySettings`

- [ ] **Step 4: Commit DifficultySettings**

```bash
git add Assets/Scripts/DDA/DifficultySettings.cs Assets/Scripts/DDA/DDA.asmdef
git commit -m "$(cat <<'EOF'
feat(dda): add DifficultySettings ScriptableObject

5 discrete difficulty levels (0.75x to 1.25x multipliers).
Methods: IncreaseDifficulty, DecreaseDifficulty, SetLevel, ResetToNormal.
Exposes HPMultiplier and DamageMultiplier for enemy scaling.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: DifficultyApplier

**Files:**
- Modify: `Assets/Scripts/Player/EnemyStats.cs`
- Create: `Assets/Scripts/DDA/DifficultyApplier.cs`

- [ ] **Step 1: Read current EnemyStats implementation**

Current `EnemyStats.cs` uses `EnemySO` data directly. We need to add difficulty multipliers.

- [ ] **Step 2: Create DifficultyApplier class**

Create `Assets/Scripts/DDA/DifficultyApplier.cs`:

```csharp
using Player;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Applies difficulty multipliers to enemy stats.
    /// Called during battle initialization before enemies spawn.
    /// </summary>
    public class DifficultyApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DifficultySettings _difficultySettings;
        
        public static DifficultyApplier Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Call this before enemy is spawned in battle.
        /// </summary>
        /// <param name="enemyStats">The enemy stats to modify.</param>
        public void ApplyDifficulty(EnemyStats enemyStats)
        {
            if (_difficultySettings == null)
            {
                Debug.LogWarning("[DifficultyApplier] DifficultySettings not assigned, using default multipliers.");
                return;
            }
            
            enemyStats.ApplyDifficultyMultiplier(
                _difficultySettings.HPMultiplier,
                _difficultySettings.DamageMultiplier
            );
        }
        
        /// <summary>
        /// Gets current difficulty settings for read-only access.
        /// </summary>
        public DifficultySettings GetDifficultySettings()
        {
            return _difficultySettings;
        }
        
        /// <summary>
        /// Sets difficulty settings reference (useful for runtime injection).
        /// </summary>
        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }
    }
}
```

- [ ] **Step 3: Modify EnemyStats to support difficulty multipliers**

Modify `Assets/Scripts/Player/EnemyStats.cs` to add:

```csharp
using UnityEngine;

namespace Player
{
    public class EnemyStats : MonoBehaviour
    {
        [SerializeField] private EnemySO _enemyData;
        private EnemyModel _enemyModel;
        
        // Track scaled values for DDA
        private int _scaledMaxHealth;
        private int _scaledBaseDamage;
        private bool _isDifficultyApplied = false;
        
        public void Start()
        {
            _enemyModel = new EnemyModel(_enemyData);
            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
        }

        public string EnemyName
        {
            get => _enemyModel.EnemyName;
            private set => _enemyModel.EnemyName = value;
        }
        
        public EnemyType EnemyType => _enemyData.EnemyType;
            
        public int Health
        {
            get => _enemyModel.Health;
            private set => _enemyModel.Health = value;
        }
        
        public int MaxHealth => _scaledMaxHealth;
        
        public int BaseDamage => _scaledBaseDamage;
        
        public int MinDamage()
        {
            return BaseDamage - _enemyModel.IntervalDamage;
        }
        
        public int MaxDamage()
        {
            return BaseDamage + _enemyModel.IntervalDamage;
        }
        
        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Called by DifficultyApplier before battle.
        /// </summary>
        public void ApplyDifficultyMultiplier(float hpMultiplier, float damageMultiplier)
        {
            _scaledMaxHealth = Mathf.RoundToInt(_enemyModel.MaxHealth * hpMultiplier);
            _scaledBaseDamage = Mathf.RoundToInt(_enemyModel.BaseDamage * damageMultiplier);
            _isDifficultyApplied = true;
            
            // Scale current health proportionally if already initialized
            if (_enemyModel.Health > 0)
            {
                float healthRatio = (float)_enemyModel.Health / _enemyModel.MaxHealth;
                _enemyModel.Health = Mathf.RoundToInt(_scaledMaxHealth * healthRatio);
            }
            else
            {
                _enemyModel.Health = _scaledMaxHealth;
            }
            
            Debug.Log($"[EnemyStats] Applied difficulty: HP={hpMultiplier:F2}x, DMG={damageMultiplier:F2}x. " +
                      $"Result: HP={_scaledMaxHealth}, DMG={_scaledBaseDamage}");
        }
        
        /// <summary>
        /// Resets difficulty to base values.
        /// </summary>
        public void ResetDifficulty()
        {
            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
            _isDifficultyApplied = false;
        }
        
        public void GetHit(int value)
        {
            if (Health - value > 0)
            {
                Health -= value;
            }
            else
            {
                Health = 0;
                Debug.Log("You Died");
                gameObject.SetActive(false);
            }
        }

        public Sprite GetPortrait()
        {
            return _enemyData.EnemyPortrait;
        }
        
        public bool IsAlive()
        {
            return Health > 0;
        }
    }
}
```

- [ ] **Step 4: Commit DifficultyApplier**

```bash
git add Assets/Scripts/DDA/DifficultyApplier.cs Assets/Scripts/Player/EnemyStats.cs
git commit -m "$(cat <<'EOF'
feat(dda): add DifficultyApplier for enemy stat scaling

DifficultyApplier applies HP/damage multipliers from DifficultySettings.
EnemyStats now tracks scaled values separately from base values.
ApplyDifficultyMultiplier scales HP and damage before battle.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: DDAAgent (ML-Agents Agent)

**Files:**
- Create: `Assets/Scripts/DDA/DDAAgent.cs`

- [ ] **Step 1: Create DDAAgent class**

Create `Assets/Scripts/DDA/DDAAgent.cs`:

```csharp
using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Manager;
using Player;
using Playfab;

namespace DDA
{
    /// <summary>
    /// ML-Agents Agent for Dynamic Difficulty Adjustment.
    /// Observes battle state and adjusts difficulty to achieve flow state.
    /// </summary>
    public class DDAAgent : Agent
    {
        [Header("References")]
        [SerializeField] private DifficultySettings _difficultySettings;
        [SerializeField] private DifficultyApplier _difficultyApplier;
        
        [Header("State Configuration")]
        [Tooltip("Expected turns per battle for normalization")]
        [SerializeField] private int _expectedTurnsPerBattle = 10;
        
        [Header("Training Configuration")]
        [Tooltip("Enable training mode (rewards calculated)")]
        [SerializeField] private bool _isTrainingMode = true;
        
        // State tracking
        private int _battleStartHP;
        private int _damageDealt;
        private int _turnCount;
        private bool _battleInProgress;
        
        // Components
        private BattleLogger _battleLogger;
        private PlayerStats _playerStats;
        
        // Events
        public event Action<int> OnDifficultyChanged;
        
        /// <summary>Current episode's starting player HP.</summary>
        public int BattleStartHP => _battleStartHP;
        
        /// <summary>Current episode's total damage dealt by player.</summary>
        public int DamageDealt => _damageDealt;
        
        /// <summary>Current episode's turn count.</summary>
        public int TurnCount => _turnCount;
        
        public override void Initialize()
        {
            _playerStats = PlayerStats.Instance;
            _battleLogger = BattleLogger.Instance;
            
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }
        }
        
        public override void OnEpisodeBegin()
        {
            // Reset state for new episode
            _battleStartHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            
            // Reset difficulty to normal for new episode
            if (_difficultySettings != null)
            {
                _difficultySettings.ResetToNormal();
            }
        }
        
        public override void CollectObservations(VectorSensor sensor)
        {
            // State 1: HP Ratio (0-1)
            // Use last battle's end HP ratio, or 1.0 if first battle
            float hpRatio = GetLastBattleHPRatio();
            sensor.AddObservation(Mathf.Clamp01(hpRatio));
            
            // State 2: Damage Ratio (0-2 typically)
            // Damage dealt / HP at battle start
            float damageRatio = GetDamageRatio();
            sensor.AddObservation(Mathf.Clamp(damageRatio, 0f, 2f));
            
            // State 3: Turn count normalized (0-1)
            // Turns taken / expected turns
            float turnNormalized = _turnCount / (float)_expectedTurnsPerBattle;
            sensor.AddObservation(Mathf.Clamp01(turnNormalized));
            
            // State 4: Current difficulty normalized (0-1)
            float difficultyNormalized = _difficultySettings != null 
                ? _difficultySettings.GetNormalizedDifficulty() 
                : 0.5f;
            sensor.AddObservation(difficultyNormalized);
        }
        
        public override void OnActionReceived(ActionBuffers actions)
        {
            int action = actions.DiscreteActions[0];
            
            if (_difficultySettings == null)
            {
                Debug.LogWarning("[DDAAgent] No DifficultySettings assigned.");
                return;
            }
            
            int previousLevel = _difficultySettings.CurrentLevelIndex;
            
            switch (action)
            {
                case 0: // Maintain
                    // No change
                    break;
                case 1: // Increase difficulty
                    _difficultySettings.IncreaseDifficulty();
                    break;
                case 2: // Decrease difficulty
                    _difficultySettings.DecreaseDifficulty();
                    break;
            }
            
            if (_difficultySettings.CurrentLevelIndex != previousLevel)
            {
                OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
                Debug.Log($"[DDAAgent] Difficulty changed: {_difficultySettings.GetLevelName()}");
            }
        }
        
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Manual control for testing
            var discreteActions = actionsOut.DiscreteActions;
            
            // Default: Maintain
            discreteActions[0] = 0;
            
            // Increase with I key
            if (Input.GetKey(KeyCode.I))
            {
                discreteActions[0] = 1;
            }
            // Decrease with D key
            else if (Input.GetKey(KeyCode.D))
            {
                discreteActions[0] = 2;
            }
        }
        
        /// <summary>
        /// Called when battle starts. Records initial state.
        /// </summary>
        public void OnBattleStart(int playerStartHP)
        {
            _battleStartHP = playerStartHP;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = true;
            
            // Request decision from agent
            RequestDecision();
        }
        
        /// <summary>
        /// Called after each turn. Updates state.
        /// </summary>
        public void OnTurnEnd(int damageDealtThisTurn)
        {
            _damageDealt += damageDealtThisTurn;
            _turnCount++;
        }
        
        /// <summary>
        /// Called when battle ends. Calculates and applies reward.
        /// </summary>
        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            _battleInProgress = false;
            
            if (!_isTrainingMode)
            {
                // Inference mode: no reward, just end episode
                EndEpisode();
                return;
            }
            
            float reward = CalculateReward(playerWon, playerEndHP, _battleStartHP, _turnCount);
            AddReward(reward);
            
            Debug.Log($"[DDAAgent] Battle end. Won: {playerWon}, HP: {playerEndHP}/{_battleStartHP}, " +
                      $"Turns: {_turnCount}, Reward: {reward:F3}");
            
            EndEpisode();
        }
        
        /// <summary>
        /// Calculates reward based on battle outcome.
        /// Shaped reward: Win +1, Loss -1, HP target bonus, efficiency penalty.
        /// </summary>
        private float CalculateReward(bool playerWon, int playerEndHP, int playerStartHP, int turns)
        {
            float reward = 0f;
            
            if (playerWon)
            {
                // Base win reward
                reward += 1.0f;
                
                // Calculate HP ratio
                float hpRatio = playerStartHP > 0 ? (float)playerEndHP / playerStartHP : 0f;
                
                // HP Target bonus (flow state: 40-60% HP)
                if (hpRatio >= 0.4f && hpRatio <= 0.6f)
                {
                    // Maximum bonus at 50% HP
                    float distanceFromOptimal = Mathf.Abs(hpRatio - 0.5f);
                    float hpBonus = 0.5f * (1.0f - distanceFromOptimal * 2f);
                    reward += hpBonus;
                }
                else if (hpRatio > 0.6f)
                {
                    // Too easy - penalty
                    float excess = hpRatio - 0.6f;
                    reward -= 0.1f * excess;
                }
                else if (hpRatio < 0.4f)
                {
                    // Too hard - penalty
                    float deficit = 0.4f - hpRatio;
                    reward -= 0.1f * deficit;
                }
            }
            else
            {
                // Loss penalty
                reward -= 1.0f;
            }
            
            // Efficiency penalty (turns over expected)
            if (turns > _expectedTurnsPerBattle)
            {
                float efficiencyPenalty = 0.01f * (turns - _expectedTurnsPerBattle);
                reward -= efficiencyPenalty;
            }
            
            // Clamp reward to reasonable range
            return Mathf.Clamp(reward, -2f, 2f);
        }
        
        private float GetLastBattleHPRatio()
        {
            if (_battleLogger == null || _battleLogger.GetCurrentLog() == null)
            {
                return 1.0f; // First battle: assume full HP
            }
            
            var lastRecord = _battleLogger.GetCurrentLog();
            if (lastRecord.Battle_Record.Count == 0)
            {
                return 1.0f;
            }
            
            var lastBattle = lastRecord.Battle_Record[lastRecord.Battle_Record.Count - 1];
            float hpRatio = lastBattle.player_performance.player_hp_end / (float)lastBattle.player_performance.player_hp_start;
            return hpRatio;
        }
        
        private float GetDamageRatio()
        {
            if (_battleStartHP <= 0) return 0f;
            return _damageDealt / (float)_battleStartHP;
        }
        
        /// <summary>
        /// Sets training mode at runtime.
        /// </summary>
        public void SetTrainingMode(bool isTraining)
        {
            _isTrainingMode = isTraining;
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Get current state for debugging.
        /// </summary>
        public string GetDebugState()
        {
            return $"HP Ratio: {GetLastBattleHPRatio():F2}, " +
                   $"Damage Ratio: {GetDamageRatio():F2}, " +
                   $"Turns: {_turnCount}, " +
                   $"Difficulty: {(_difficultySettings != null ? _difficultySettings.GetLevelName() : "None")}";
        }
        #endif
    }
}
```

- [ ] **Step 2: Commit DDAAgent**

```bash
git add Assets/Scripts/DDA/DDAAgent.cs
git commit -m "$(cat <<'EOF'
feat(dda): add DDAAgent ML-Agents implementation

Collects 4 observations: HP ratio, damage ratio, turn normalized, difficulty.
Actions: Maintain (0), Increase (1), Decrease (2) difficulty.
Shaped reward: Win +1, Loss -1, HP target bonus (40-60%), efficiency penalty.
Heuristic mode for manual testing (I/D keys).

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: DDAIntegration (BattleSystem Hooks)

**Files:**
- Create: `Assets/Scripts/DDA/DDAIntegration.cs`
- Modify: `Assets/Scripts/Manager/BattleSystem.cs`

- [ ] **Step 1: Create DDAIntegration class**

Create `Assets/Scripts/DDA/DDAIntegration.cs`:

```csharp
using System;
using UnityEngine;
using Manager;
using Player;
using DDA;

namespace DDA
{
    /// <summary>
    /// Coordinates DDA agent with battle system.
    /// Hooks into battle lifecycle events.
    /// </summary>
    public class DDAIntegration : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DDAAgent _ddaAgent;
        [SerializeField] private DifficultyApplier _difficultyApplier;
        [SerializeField] private DifficultySettings _difficultySettings;
        
        [Header("Configuration")]
        [SerializeField] private bool _enableDDA = true;
        [SerializeField] private bool _isTrainingMode = false;
        
        private BattleSystem _battleSystem;
        private BattleLogger _battleLogger;
        private int _playerStartHP;
        
        public static DDAIntegration Instance { get; private set; }
        
        /// <summary>Is DDA currently enabled?</summary>
        public bool IsEnabled => _enableDDA;
        
        /// <summary>Is training mode active?</summary>
        public bool IsTrainingMode => _isTrainingMode;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            _battleLogger = BattleLogger.Instance;
            
            if (_ddaAgent == null)
            {
                _ddaAgent = FindObjectOfType<DDAAgent>();
            }
            
            if (_difficultyApplier == null)
            {
                _difficultyApplier = DifficultyApplier.Instance;
            }
            
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }
            
            // Set training mode
            if (_ddaAgent != null)
            {
                _ddaAgent.SetTrainingMode(_isTrainingMode);
            }
        }
        
        /// <summary>
        /// Called before battle starts. Requests difficulty decision from agent.
        /// </summary>
        public void OnBattlePreStart(int playerStartHP)
        {
            if (!_enableDDA || _ddaAgent == null)
            {
                return;
            }
            
            _playerStartHP = playerStartHP;
            _ddaAgent.OnBattleStart(playerStartHP);
            
            Debug.Log($"[DDAIntegration] Battle pre-start. Player HP: {playerStartHP}, " +
                      $"Difficulty: {_difficultySettings.GetLevelName()}");
        }
        
        /// <summary>
        /// Called after each turn. Updates DDA state.
        /// </summary>
        public void OnTurnEnd(int damageDealtThisTurn)
        {
            if (!_enableDDA || _ddaAgent == null)
            {
                return;
            }
            
            _ddaAgent.OnTurnEnd(damageDealtThisTurn);
        }
        
        /// <summary>
        /// Called when battle ends. Calculates reward and triggers learning.
        /// </summary>
        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            if (!_enableDDA || _ddaAgent == null)
            {
                return;
            }
            
            _ddaAgent.OnBattleEnd(playerWon, playerEndHP);
            
            Debug.Log($"[DDAIntegration] Battle end. Won: {playerWon}, " +
                      $"End HP: {playerEndHP}/{_playerStartHP}");
        }
        
        /// <summary>
        /// Applies difficulty to enemy stats before spawn.
        /// </summary>
        public void ApplyDifficultyToEnemy(EnemyStats enemyStats)
        {
            if (!_enableDDA || _difficultyApplier == null)
            {
                return;
            }
            
            _difficultyApplier.ApplyDifficulty(enemyStats);
        }
        
        /// <summary>
        /// Enables or disables DDA at runtime.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enableDDA = enabled;
        }
        
        /// <summary>
        /// Sets training mode at runtime.
        /// </summary>
        public void SetTrainingMode(bool isTraining)
        {
            _isTrainingMode = isTraining;
            if (_ddaAgent != null)
            {
                _ddaAgent.SetTrainingMode(isTraining);
            }
        }
        
        /// <summary>
        /// Gets current difficulty level name for UI.
        /// </summary>
        public string GetCurrentDifficultyName()
        {
            if (_difficultySettings == null)
            {
                return "Normal";
            }
            return _difficultySettings.GetLevelName();
        }
        
        /// <summary>
        /// Gets current difficulty multipliers for UI.
        /// </summary>
        public (float hp, float damage) GetCurrentMultipliers()
        {
            if (_difficultySettings == null)
            {
                return (1.0f, 1.0f);
            }
            return (_difficultySettings.HPMultiplier, _difficultySettings.DamageMultiplier);
        }
    }
}
```

- [ ] **Step 2: Modify BattleSystem to integrate DDA**

Modify `Assets/Scripts/Manager/BattleSystem.cs` to add DDA hooks:

```csharp
// Add at top with other usings
using DDA;

// Add field in class
public class BattleSystem : MonoBehaviour
{
    // ... existing fields ...
    
    [Header("DDA Integration")]
    [SerializeField] private DDAIntegration _ddaIntegration;
    
    // Add in PrepareBattle method after SpawnEnemies
    public async UniTask PrepareBattle()
    {
        await Initialize();
        await InitializeFSM();
        await SpawnEnemies();
        await StartBattleLogging();
        
        // DDA: Notify battle start
        if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
        {
            _ddaIntegration.OnBattlePreStart(PlayerStats.Health);
        }
    }
    
    // Add difficulty application in SpawnEnemies
    public async UniTask SpawnEnemies()
    {
        AudioManager.Instance.PlaySound(SoundType.SFX_SpawnEnemy);
        var enemies = MapSystem.GetEnemies();
        Transform[] enemiesPos = GameManager.GetEnemiesPosition();
        Enemies.Clear();
        
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = Instantiate(enemies[i].Prefab, enemiesPos[i]);
            var enemyStats = enemy.GetComponent<EnemyStats>();
            
            // DDA: Apply difficulty multipliers
            if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
            {
                _ddaIntegration.ApplyDifficultyToEnemy(enemyStats);
            }
            
            Enemies.Add(enemy.GetComponent<EnemyController>());
        }
        await UniTask.Yield();
    }
    
    // Add DDA notification in OnBattleEnd
    public void OnBattleEnd()
    {
        bool playerWon = BattleResult == BattleResult.PlayerWin;
        
        int totalEnemyHP = 0;
        foreach (var enemy in Enemies)
        {
            totalEnemyHP += enemy.EnemyStats.Health;
        }
        
        if (BattleLogger != null)
        {
            BattleLogger.EndBattle(PlayerStats.Health, totalEnemyHP, playerWon);
            SessionManager.Instance.EndSessionAndSend();
        }
        
        // DDA: Notify battle end
        if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
        {
            _ddaIntegration.OnBattleEnd(playerWon, PlayerStats.Health);
        }
    }
}
```

- [ ] **Step 3: Commit DDAIntegration**

```bash
git add Assets/Scripts/DDA/DDAIntegration.cs Assets/Scripts/Manager/BattleSystem.cs
git commit -m "$(cat <<'EOF'
feat(dda): add DDAIntegration for BattleSystem hooks

DDAIntegration coordinates agent with battle lifecycle:
- OnBattlePreStart: Request difficulty decision
- OnTurnEnd: Update state tracking
- OnBattleEnd: Calculate reward

BattleSystem applies difficulty to enemies on spawn.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2: Python DDQN Plugin

### Task 5: DDQN Network Architecture

**Files:**
- Create: `ml_agents_plugin/setup.py`
- Create: `ml_agents_plugin/mlagents_plugin_ddqn/__init__.py`
- Create: `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_network.py`

- [ ] **Step 1: Create plugin directory structure**

```bash
mkdir -p ml_agents_plugin/mlagents_plugin_ddqn
```

- [ ] **Step 2: Create setup.py**

Create `ml_agents_plugin/setup.py`:

```python
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
```

- [ ] **Step 3: Create __init__.py**

Create `ml_agents_plugin/mlagents_plugin_ddqn/__init__.py`:

```python
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
```

- [ ] **Step 4: Create Q-Network architecture**

Create `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_network.py`:

```python
"""
Q-Network architecture for DDQN.
"""
import torch
import torch.nn as nn
from typing import List


class QNetwork(nn.Module):
    """
    Q-Network for DDQN with configurable hidden layers.
    
    Architecture:
        Input: State observation (4 continuous values)
        Hidden: 2 layers of 128 units each with ReLU
        Output: Q-values for each action (3 actions)
    """
    
    def __init__(
        self,
        observation_size: int = 4,
        action_size: int = 3,
        hidden_units: int = 128,
        num_layers: int = 2,
    ):
        super().__init__()
        
        self.observation_size = observation_size
        self.action_size = action_size
        self.hidden_units = hidden_units
        
        # Build layers
        layers = []
        
        # Input layer
        layers.append(nn.Linear(observation_size, hidden_units))
        layers.append(nn.ReLU())
        
        # Hidden layers
        for _ in range(num_layers - 1):
            layers.append(nn.Linear(hidden_units, hidden_units))
            layers.append(nn.ReLU())
        
        # Output layer (Q-values for each action)
        layers.append(nn.Linear(hidden_units, action_size))
        
        self.network = nn.Sequential(*layers)
        
        # Initialize weights
        self._initialize_weights()
    
    def _initialize_weights(self):
        """Initialize network weights using Xavier initialization."""
        for module in self.modules():
            if isinstance(module, nn.Linear):
                nn.init.xavier_uniform_(module.weight)
                nn.init.zeros_(module.bias)
    
    def forward(self, state: torch.Tensor) -> torch.Tensor:
        """
        Forward pass through the network.
        
        Args:
            state: State observation tensor of shape (batch_size, observation_size)
                   or (observation_size,) for single state
        
        Returns:
            Q-values tensor of shape (batch_size, action_size)
        """
        # Ensure correct shape
        if state.dim() == 1:
            state = state.unsqueeze(0)
        
        return self.network(state)
    
    def get_q_values(self, state: torch.Tensor) -> torch.Tensor:
        """Get Q-values for a state."""
        with torch.no_grad():
            return self.forward(state)
    
    def get_action(self, state: torch.Tensor, epsilon: float = 0.0) -> int:
        """
        Get action using epsilon-greedy policy.
        
        Args:
            state: State observation
            epsilon: Exploration rate (0 = greedy, 1 = random)
        
        Returns:
            Action index
        """
        if torch.rand(1).item() < epsilon:
            return torch.randint(0, self.action_size, (1,)).item()
        
        with torch.no_grad():
            q_values = self.forward(state)
            return q_values.argmax(dim=-1).item()


class ReplayBuffer:
    """
    Experience replay buffer for DDQN.
    Stores transitions and samples batches for training.
    """
    
    def __init__(self, capacity: int = 10000):
        self.capacity = capacity
        self.buffer = []
        self.position = 0
    
    def push(
        self,
        state: torch.Tensor,
        action: int,
        reward: float,
        next_state: torch.Tensor,
        done: bool,
    ):
        """Add transition to buffer."""
        if len(self.buffer) < self.capacity:
            self.buffer.append(None)
        
        self.buffer[self.position] = (state, action, reward, next_state, done)
        self.position = (self.position + 1) % self.capacity
    
    def sample(self, batch_size: int) -> dict:
        """
        Sample a batch of transitions.
        
        Returns:
            dict with tensors: states, actions, rewards, next_states, dones
        """
        import random
        indices = random.sample(range(len(self.buffer)), batch_size)
        
        states, actions, rewards, next_states, dones = zip(
            *[self.buffer[i] for i in indices]
        )
        
        return {
            "states": torch.stack(states),
            "actions": torch.tensor(actions, dtype=torch.long),
            "rewards": torch.tensor(rewards, dtype=torch.float32),
            "next_states": torch.stack(next_states),
            "dones": torch.tensor(dones, dtype=torch.float32),
        }
    
    def __len__(self):
        return len(self.buffer)
    
    def is_ready(self, batch_size: int) -> bool:
        """Check if buffer has enough samples."""
        return len(self.buffer) >= batch_size
```

- [ ] **Step 5: Commit DDQN network**

```bash
git add ml_agents_plugin/
git commit -m "$(cat <<'EOF'
feat(ddqn-plugin): add Q-Network and ReplayBuffer

QNetwork: 2-layer MLP (128 units) with ReLU, outputs Q-values.
ReplayBuffer: 10k capacity, stores transitions for experience replay.
Xavier initialization for stable training.
Epsilon-greedy action selection.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: DDQN Policy and Trainer

**Files:**
- Create: `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_policy.py`
- Create: `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_trainer.py`

- [ ] **Step 1: Create DDQN Policy**

Create `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_policy.py`:

```python
"""
DDQN Policy wrapper for ML-Agents.
"""
import torch
import numpy as np
from typing import Dict, Any, List
from mlagents.trainers.policy import Policy
from mlagents.trainers.processor import ProcessorManager
from mlagents_envs.base_env import DecisionSteps, TerminalSteps

from .ddqn_network import QNetwork


class DDQNPolicy(Policy):
    """
    DDQN Policy implementation for ML-Agents.
    Wraps QNetwork to provide action selection.
    """
    
    def __init__(
        self,
        seed: int,
        behavior_name: str,
        network_settings: Dict[str, Any],
        action_specs,
        trainer_settings: Dict[str, Any],
    ):
        super().__init__(seed, behavior_name, network_settings, trainer_settings)
        
        # Extract network parameters
        hidden_units = network_settings.get("hidden_units", 128)
        num_layers = network_settings.get("num_layers", 2)
        
        # Get observation and action sizes from specs
        obs_size = self._get_observation_size()
        action_size = self._get_action_size()
        
        # Create Q-networks (online and target)
        self.q_network = QNetwork(
            observation_size=obs_size,
            action_size=action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )
        
        self.target_network = QNetwork(
            observation_size=obs_size,
            action_size=action_size,
            hidden_units=hidden_units,
            num_layers=num_layers,
        )
        
        # Copy weights to target network
        self.target_network.load_state_dict(self.q_network.state_dict())
        
        # Epsilon for exploration
        self.epsilon = trainer_settings.get("exploration_initial_eps", 1.0)
        
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.q_network.to(self.device)
        self.target_network.to(self.device)
    
    def _get_observation_size(self) -> int:
        """Extract total observation size from specs."""
        # Default DDA state size: 4
        return 4
    
    def _get_action_size(self) -> int:
        """Extract action size from specs."""
        # Default DDA actions: 3 (Maintain, Increase, Decrease)
        return 3
    
    def get_action(
        self,
        decision_requests: DecisionSteps,
        worker_id: int = 0,
        deterministic: bool = False,
    ) -> np.ndarray:
        """
        Get actions for the given observations.
        
        Args:
            decision_requests: DecisionSteps containing observations
            worker_id: Worker ID (unused in DDQN)
            deterministic: If True, always take greedy action
        
        Returns:
            np.ndarray of shape (num_agents, 1) containing actions
        """
        obs = decision_requests.obs[0]  # Shape: (num_agents, obs_size)
        obs_tensor = torch.tensor(obs, dtype=torch.float32, device=self.device)
        
        with torch.no_grad():
            if deterministic or np.random.random() > self.epsilon:
                q_values = self.q_network(obs_tensor)
                actions = q_values.argmax(dim=-1).cpu().numpy()
            else:
                # Random exploration
                actions = np.random.randint(0, self._get_action_size(), size=len(obs))
        
        return actions.reshape(-1, 1)
    
    def update_target_network(self, tau: float = 1.0):
        """
        Soft update target network using Polyak averaging.
        
        Args:
            tau: Soft update coefficient (1.0 = hard copy, 0.005 = soft)
        """
        for target_param, online_param in zip(
            self.target_network.parameters(),
            self.q_network.parameters(),
        ):
            target_param.data.copy_(
                tau * online_param.data + (1 - tau) * target_param.data
            )
    
    def save_checkpoint(self, checkpoint_path: str):
        """Save model checkpoint."""
        torch.save(
            {
                "q_network": self.q_network.state_dict(),
                "target_network": self.target_network.state_dict(),
            },
            checkpoint_path,
        )
    
    def load_checkpoint(self, checkpoint_path: str):
        """Load model checkpoint."""
        checkpoint = torch.load(checkpoint_path, map_location=self.device)
        self.q_network.load_state_dict(checkpoint["q_network"])
        self.target_network.load_state_dict(checkpoint["target_network"])
    
    def get_weights(self):
        """Get model weights for syncing."""
        return {k: v.cpu().numpy() for k, v in self.q_network.state_dict().items()}
    
    def set_weights(self, weights: Dict[str, np.ndarray]):
        """Set model weights from external source."""
        state_dict = {k: torch.tensor(v) for k, v in weights.items()}
        self.q_network.load_state_dict(state_dict)
        self.target_network.load_state_dict(state_dict)
```

- [ ] **Step 2: Create DDQN Trainer**

Create `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_trainer.py`:

```python
"""
DDQN Trainer implementation for ML-Agents.
"""
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from typing import Dict, List, Any, Optional
from mlagents.trainers.trainer import Trainer
from mlagents.trainers.policy import Policy
from mlagents_envs.base_env import BehaviorSpec

from .ddqn_network import QNetwork, ReplayBuffer
from .ddqn_policy import DDQNPolicy


class DDQNTrainer(Trainer):
    """
    Double Deep Q-Network trainer for ML-Agents.
    
    Implements DDQN algorithm with:
    - Experience replay buffer
    - Target network with soft updates
    - Double Q-learning (online network for action selection, target for evaluation)
    """
    
    def __init__(
        self,
        behavior_name: str,
        reward_signal_specs,
        policy: DDQNPolicy,
        trainer_settings: Dict[str, Any],
        training: bool = True,
        artifact_path: str = "",
        init_path: Optional[str] = None,
        checkpoint_interval: int = 10000,
    ):
        super().__init__(
            behavior_name,
            reward_signal_specs,
            policy,
            trainer_settings,
            training,
            artifact_path,
            init_path,
        )
        
        # Training configuration
        hyperparams = trainer_settings.get("hyperparameters", {})
        
        self.learning_rate = hyperparams.get("learning_rate", 0.0003)
        self.batch_size = hyperparams.get("batch_size", 64)
        self.buffer_size = hyperparams.get("buffer_size", 10000)
        self.gamma = hyperparams.get("gamma", 0.99)
        self.tau = hyperparams.get("tau", 0.005)  # Soft update rate
        self.target_update_interval = hyperparams.get("target_update_interval", 10000)
        
        # Exploration schedule
        self.exploration_initial_eps = hyperparams.get("exploration_initial_eps", 1.0)
        self.exploration_final_eps = hyperparams.get("exploration_final_eps", 0.01)
        self.exploration_decay_steps = hyperparams.get("exploration_decay_steps", 50000)
        
        # Training state
        self.step = 0
        self.episode_rewards: List[float] = []
        self.last_reward = 0.0
        
        # Initialize replay buffer
        self.replay_buffer = ReplayBuffer(capacity=self.buffer_size)
        
        # Initialize optimizer
        self.optimizer = optim.Adam(
            self.policy.q_network.parameters(),
            lr=self.learning_rate,
        )
        
        # Loss function
        self.loss_fn = nn.MSELoss()
        
        self.checkpoint_interval = checkpoint_interval
        
        # Device
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    
    def _get_current_epsilon(self) -> float:
        """
        Calculate current epsilon for exploration.
        Linear decay from initial to final.
        """
        if self.step >= self.exploration_decay_steps:
            return self.exploration_final_eps
        
        progress = self.step / self.exploration_decay_steps
        return self.exploration_initial_eps - progress * (
            self.exploration_initial_eps - self.exploration_final_eps
        )
    
    def process_experiences(
        self,
        current_info: DecisionSteps,
        new_info: DecisionSteps,
        actions: np.ndarray,
        rewards: np.ndarray,
        dones: np.ndarray,
    ):
        """
        Process experiences from environment and store in replay buffer.
        
        Args:
            current_info: Current observations
            new_info: Next observations
            actions: Actions taken
            rewards: Rewards received
            dones: Episode termination flags
        """
        # Convert observations to tensors
        states = torch.tensor(
            current_info.obs[0], dtype=torch.float32, device=self.device
        )
        next_states = torch.tensor(
            new_info.obs[0], dtype=torch.float32, device=self.device
        )
        
        # Store transitions
        for i in range(len(rewards)):
            self.replay_buffer.push(
                state=states[i],
                action=int(actions[i]),
                reward=float(rewards[i]),
                next_state=next_states[i],
                done=bool(dones[i]),
            )
            
            # Track episode rewards
            if dones[i]:
                self.episode_rewards.append(float(rewards[i]))
    
    def update_policy(self):
        """
        Update Q-network using sampled batch from replay buffer.
        DDQN: Use online network for action selection, target for evaluation.
        """
        # Check if enough samples
        if not self.replay_buffer.is_ready(self.batch_size):
            return
        
        # Sample batch
        batch = self.replay_buffer.sample(self.batch_size)
        
        states = batch["states"].to(self.device)
        actions = batch["actions"].to(self.device)
        rewards = batch["rewards"].to(self.device)
        next_states = batch["next_states"].to(self.device)
        dones = batch["dones"].to(self.device)
        
        # Compute current Q values
        q_values = self.policy.q_network(states)
        current_q = q_values.gather(1, actions.unsqueeze(1)).squeeze(1)
        
        # DDQN: Use online network to select best action
        with torch.no_grad():
            next_q_online = self.policy.q_network(next_states)
            next_actions = next_q_online.argmax(dim=1)
            
            # Use target network to evaluate the action
            next_q_target = self.policy.target_network(next_states)
            next_q_values = next_q_target.gather(1, next_actions.unsqueeze(1)).squeeze(1)
            
            # Compute target Q values
            target_q = rewards + self.gamma * next_q_values * (1 - dones)
        
        # Compute loss
        loss = self.loss_fn(current_q, target_q)
        
        # Optimize
        self.optimizer.zero_grad()
        loss.backward()
        torch.nn.utils.clip_grad_norm_(self.policy.q_network.parameters(), 1.0)
        self.optimizer.step()
        
        # Soft update target network
        self.policy.update_target_network(tau=self.tau)
        
        self.step += 1
    
    def advance(self):
        """Advance training step."""
        # Update epsilon
        self.policy.epsilon = self._get_current_epsilon()
    
    def add_policy_stats(
        self,
        stats: Dict[str, Any],
        policy_name: str,
    ) -> Dict[str, Any]:
        """Add policy statistics for logging."""
        stats[f"Policy/{policy_name}/Epsilon"] = self.policy.epsilon
        stats[f"Policy/{policy_name}/BufferSize"] = len(self.replay_buffer)
        stats[f"Policy/{policy_name}/LearningRate"] = self.learning_rate
        return stats
    
    def get_module_names(self) -> List[str]:
        """Get list of module names for checkpointing."""
        return ["q_network", "target_network"]
    
    def save_model_checkpoint(self, checkpoint_path: str):
        """Save model checkpoint."""
        self.policy.save_checkpoint(checkpoint_path)
    
    def load_model_checkpoint(self, checkpoint_path: str):
        """Load model checkpoint."""
        self.policy.load_checkpoint(checkpoint_path)


def register_ddqn_trainer():
    """
    Register DDQN trainer with ML-Agents.
    Called via entry point in setup.py.
    """
    from mlagents.trainers import register_trainer
    
    register_trainer("ddqn", DDQNTrainer)
    
    # Also register the policy
    from mlagents.trainers import register_policy
    
    register_policy("ddqn", DDQNPolicy)
```

- [ ] **Step 3: Commit DDQN trainer**

```bash
git add ml_agents_plugin/mlagents_plugin_ddqn/ddqn_policy.py ml_agents_plugin/mlagents_plugin_ddqn/ddqn_trainer.py
git commit -m "$(cat <<'EOF'
feat(ddqn-plugin): add DDQNPolicy and DDQNTrainer

DDQNPolicy: Wraps QNetwork, epsilon-greedy action selection, soft target updates.
DDQNTrainer: Experience replay (10k), target network (τ=0.005), DDQN loss.
Linear exploration decay from 1.0 to 0.01 over 50k steps.
Registers 'ddqn' trainer type via ML-Agents entry point.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3: Training Configuration

### Task 7: Update Training Config and Create Training Scene

**Files:**
- Modify: `config/ddqn.yaml`
- Create: Training scene setup instructions

- [ ] **Step 1: Update ddqn.yaml configuration**

Replace `config/ddqn.yaml` with DDQN-specific config:

```yaml
# DDQN Training Configuration for Unity ML-Agents
# Uses custom ddqn trainer plugin (mlagents-ddqn-plugin)
# 
# Installation:
#   cd ml_agents_plugin
#   pip install -e .
#
# Usage:
#   mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train

behaviors:
  ddqn_dda:
    trainer_type: ddqn  # Custom DDQN trainer from plugin
    
    hyperparameters:
      # Learning
      learning_rate: 0.0003
      learning_rate_schedule: linear
      
      # Experience Replay
      batch_size: 64
      buffer_size: 10000
      
      # DDQN - Target Network Update
      tau: 0.005              # Soft update rate
      target_update_interval: 10000  # Hard update interval (steps)
      
      # Exploration (Epsilon-greedy)
      exploration_schedule: linear
      exploration_initial_eps: 1.0
      exploration_final_eps: 0.01
      exploration_decay_steps: 50000
      
      # Discount factor
      gamma: 0.99
    
    network_settings:
      normalize: true
      hidden_units: 128
      num_layers: 2
      vis_encode_type: simple
    
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    
    # Training settings
    keep_checkpoints: 5
    max_steps: 500000
    time_horizon: 64
    summary_freq: 10000
    checkpoint_interval: 10000

# Environment settings
default_settings:
  trainer_type: ddqn
  
env_settings:
  env_path: null
  base_port: 5005
  num_envs: 1
  num_areas: 1
  timeout_wait: 60
  seed: -1

# Engine settings for training
engine_settings:
  width: 84
  height: 84
  quality_level: 5
  time_scale: 20
  target_frame_rate: -1
  capture_frame_rate: 60
  no_graphics: false

# Checkpoint settings
checkpoint_settings:
  run_id: ddqn_dda_v1
  initialize_from: null
  load_model: false
  resume: false
  force: false
  train_model: true
  inference: false
  results_dir: results

# Debug settings
debug: false
```

- [ ] **Step 2: Create training scene setup document**

Create `docs/superpowers/training-setup.md`:

```markdown
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

### 3. Configure BattleSimulator (Fast Training)

For fast training, create a simplified battle simulator that skips animations:

```csharp
// Assets/Scripts/DDA/BattleSimulator.cs
// Simulates battles instantly for training
```

### 4. Run Training

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

### 5. Monitor Training

```bash
# TensorBoard
tensorboard --logdir results/ddqn_dda_v1

# Metrics to watch:
# - Policy/ddqn_dda/Epsilon (should decay)
# - Policy/ddqn_dda/BufferSize (should fill up)
# - Reward (should increase over time)
```

### 6. Inference

After training:

1. Copy `results/ddqn_dda_v1/ddqn_dda.onnx` to `Assets/ML-Agents/Models/`
2. In Unity Editor:
   - Select DDAAgent
   - Behavior Parameters > Model: ddqn_dda
   - Set Training Mode: false
3. Play game normally - agent adjusts difficulty automatically
```

- [ ] **Step 3: Commit training config**

```bash
git add config/ddqn.yaml docs/superpowers/training-setup.md
git commit -m "$(cat <<'EOF'
feat(dda): update DDQN training configuration

DDQN-specific hyperparameters: tau=0.005, buffer=10000, batch=64.
Linear epsilon decay 1.0→0.01 over 50k steps.
Training setup documentation with scene configuration.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4: Testing

### Task 8: Integration Testing

**Files:**
- Create: `Assets/Scripts/DDA/Tests/DDAIntegrationTests.cs`

- [ ] **Step 1: Create test assembly definition**

Create `Assets/Scripts/DDA/Tests/DDA.Tests.asmdef`:

```json
{
    "name": "DDA.Tests",
    "rootNamespace": "DDA.Tests",
    "references": [
        "DDA",
        "Unity.ML-Agents",
        "Unity.TestFramework"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create DifficultySettings tests**

Create `Assets/Scripts/DDA/Tests/DifficultySettingsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DDA.Tests
{
    [TestFixture]
    public class DifficultySettingsTests
    {
        private DifficultySettings _settings;
        
        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<DifficultySettings>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }
        
        [Test]
        public void InitialLevel_IsNormal()
        {
            // Default level should be 2 (Normal)
            Assert.AreEqual(2, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.0f, _settings.HPMultiplier);
            Assert.AreEqual(1.0f, _settings.DamageMultiplier);
        }
        
        [Test]
        public void IncreaseDifficulty_ClampsAtMaximum()
        {
            // Increase beyond maximum
            for (int i = 0; i < 10; i++)
            {
                _settings.IncreaseDifficulty();
            }
            
            // Should be at maximum level (4)
            Assert.AreEqual(4, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.25f, _settings.HPMultiplier);
        }
        
        [Test]
        public void DecreaseDifficulty_ClampsAtMinimum()
        {
            // Decrease beyond minimum
            for (int i = 0; i < 10; i++)
            {
                _settings.DecreaseDifficulty();
            }
            
            // Should be at minimum level (0)
            Assert.AreEqual(0, _settings.CurrentLevelIndex);
            Assert.AreEqual(0.75f, _settings.HPMultiplier);
        }
        
        [Test]
        public void SetLevel_ValidIndex_SetsCorrectly()
        {
            _settings.SetLevel(3);
            Assert.AreEqual(3, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.125f, _settings.HPMultiplier);
        }
        
        [Test]
        public void SetLevel_NegativeIndex_ClampsToZero()
        {
            _settings.SetLevel(-5);
            Assert.AreEqual(0, _settings.CurrentLevelIndex);
        }
        
        [Test]
        public void SetLevel_TooHighIndex_ClampsToMaximum()
        {
            _settings.SetLevel(100);
            Assert.AreEqual(4, _settings.CurrentLevelIndex);
        }
        
        [Test]
        public void ResetToNormal_SetsLevelTwo()
        {
            _settings.SetLevel(0);
            _settings.ResetToNormal();
            Assert.AreEqual(2, _settings.CurrentLevelIndex);
        }
        
        [Test]
        public void GetNormalizedDifficulty_ReturnsCorrectRange()
        {
            // Level 0 -> 0.0
            _settings.SetLevel(0);
            Assert.AreEqual(0.0f, _settings.GetNormalizedDifficulty());
            
            // Level 2 -> 0.5
            _settings.SetLevel(2);
            Assert.AreEqual(0.5f, _settings.GetNormalizedDifficulty());
            
            // Level 4 -> 1.0
            _settings.SetLevel(4);
            Assert.AreEqual(1.0f, _settings.GetNormalizedDifficulty());
        }
        
        [Test]
        public void GetLevelName_ReturnsCorrectNames()
        {
            string[] expectedNames = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };
            
            for (int i = 0; i < 5; i++)
            {
                _settings.SetLevel(i);
                Assert.AreEqual(expectedNames[i], _settings.GetLevelName());
            }
        }
    }
}
```

- [ ] **Step 3: Create DDAAgent reward tests**

Create `Assets/Scripts/DDA/Tests/DDAAgentTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DDA.Tests
{
    [TestFixture]
    public class DDAAgentRewardTests
    {
        private DDAAgent _agent;
        private DifficultySettings _settings;
        
        [SetUp]
        public void SetUp()
        {
            var go = new GameObject();
            _agent = go.AddComponent<DDAAgent>();
            _settings = ScriptableObject.CreateInstance<DifficultySettings>();
            
            // Use reflection to set private field
            var field = typeof(DDAAgent).GetField("_difficultySettings", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_agent, _settings);
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_agent.gameObject);
            Object.DestroyImmediate(_settings);
        }
        
        [Test]
        public void CalculateReward_WinWithOptimalHP_ReturnsMaximumReward()
        {
            // Win with 50% HP (optimal)
            float reward = InvokeCalculateReward(_agent, true, 50, 100, 10);
            
            // Expected: 1.0 (win) + 0.5 (optimal HP) = 1.5
            Assert.AreEqual(1.5f, reward, 0.01f);
        }
        
        [Test]
        public void CalculateReward_WinWithLowHP_ReturnsWinWithPenalty()
        {
            // Win with 20% HP (too hard)
            float reward = InvokeCalculateReward(_agent, true, 20, 100, 10);
            
            // Expected: 1.0 (win) - 0.2 (too hard penalty) = 0.8
            // Penalty: 0.1 * (0.4 - 0.2) = 0.02 per 10% = 0.02 * 20/10 = 0.02 * 2 = 0.04
            // Actually: penalty = 0.1 * (0.4 - hpRatio) = 0.1 * (0.4 - 0.2) = 0.02
            // Reward = 1.0 - 0.02 = 0.98
            Assert.AreEqual(0.98f, reward, 0.05f);
        }
        
        [Test]
        public void CalculateReward_Loss_ReturnsNegativeReward()
        {
            // Loss
            float reward = InvokeCalculateReward(_agent, false, 0, 100, 10);
            
            Assert.AreEqual(-1.0f, reward, 0.01f);
        }
        
        [Test]
        public void CalculateReward_WinWithSlowClear_AppliesEfficiencyPenalty()
        {
            // Win but took 20 turns (expected 10)
            float reward = InvokeCalculateReward(_agent, true, 50, 100, 20);
            
            // Expected: 1.5 (win + optimal HP) - 0.1 (10 extra turns * 0.01)
            Assert.AreEqual(1.4f, reward, 0.05f);
        }
        
        [Test]
        public void CalculateReward_WinWithTooEasy_AppliesPenalty()
        {
            // Win with 80% HP (too easy)
            float reward = InvokeCalculateReward(_agent, true, 80, 100, 10);
            
            // Expected: 1.0 (win) - 0.2 (0.1 * (0.8 - 0.6)) = 0.98
            // Penalty: 0.1 * (hpRatio - 0.6) = 0.1 * 0.2 = 0.02
            // Reward = 1.0 - 0.02 = 0.98
            Assert.AreEqual(0.98f, reward, 0.05f);
        }
        
        private float InvokeCalculateReward(DDAAgent agent, bool playerWon, int playerEndHP, 
                                            int playerStartHP, int turns)
        {
            var method = typeof(DDAAgent).GetMethod("CalculateReward", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (float)method.Invoke(agent, new object[] { playerWon, playerEndHP, playerStartHP, turns });
        }
    }
}
```

- [ ] **Step 4: Commit tests**

```bash
git add Assets/Scripts/DDA/Tests/
git commit -m "$(cat <<'EOF'
test(dda): add DifficultySettings and DDAAgent tests

DifficultySettingsTests: level clamping, multipliers, normalized values.
DDAAgentTests: reward calculation for win/loss, HP target, efficiency.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Summary

**Files Created:**
- `Assets/Scripts/DDA/DDA.asmdef`
- `Assets/Scripts/DDA/DifficultySettings.cs`
- `Assets/Scripts/DDA/DifficultyApplier.cs`
- `Assets/Scripts/DDA/DDAAgent.cs`
- `Assets/Scripts/DDA/DDAIntegration.cs`
- `ml_agents_plugin/setup.py`
- `ml_agents_plugin/mlagents_plugin_ddqn/__init__.py`
- `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_network.py`
- `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_policy.py`
- `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_trainer.py`
- `docs/superpowers/training-setup.md`
- `Assets/Scripts/DDA/Tests/DDA.Tests.asmdef`
- `Assets/Scripts/DDA/Tests/DifficultySettingsTests.cs`
- `Assets/Scripts/DDA/Tests/DDAAgentTests.cs`

**Files Modified:**
- `Assets/Scripts/Player/EnemyStats.cs`
- `Assets/Scripts/Manager/BattleSystem.cs`
- `config/ddqn.yaml`

**Implementation Order:**
1. Phase 1: Unity Agent (Tasks 1-4)
2. Phase 2: Python Plugin (Tasks 5-6)
3. Phase 3: Training Setup (Task 7)
4. Phase 4: Testing (Task 8)

---

Plan complete and saved to `docs/superpowers/plans/2026-04-27-ddqn-dda-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?