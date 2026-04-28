using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Player;
using UnityEngine.InputSystem;

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

        // Area progression tracking
        private int _currentArea;
        private int _totalAreas;
        private int _areasWon;
        private int _lastDifficultyLevel = -1;
        private bool _decisionPending; // Only true when we explicitly want a decision

        // HP tracking for reward calculation (works in training without BattleLogger)
        private int _lastBattleEndHP;
        private int _lastBattleStartHP;

        // Player power tracking
        private int _playerLevel = 1;
        private MapType _nextAreaType = MapType.Enemy;

        // Components
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

            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }
        }

        public override void OnEpisodeBegin()
        {
            // Reset PER-EPISODE state only (battle-specific)
            // Do NOT reset: _currentArea, _areasWon, difficulty (those are RUN-level)
            _battleStartHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            _decisionPending = false; // Reset decision flag for new episode

            // Difficulty persists across episodes within a run
            // It gets reset at run start (OnRunStart) and set by agent decisions
        }

        /// <summary>
        /// Called when area completed (won or lost).
        /// Requests difficulty decision for NEXT area.
        /// Episode will end when next area starts.
        /// </summary>
        public void OnAreaComplete(bool won)
        {
            if (won)
            {
                _areasWon++;
            }
            else
            {
                _areasWon = 0; // Reset streak on loss
            }

            if (!_isTrainingMode)
            {
                return;
            }

            // Calculate reward for this area
            float reward = CalculateRewardForArea(won);
            AddReward(reward);

            Debug.Log($"[DDAAgent] Area complete. Won: {won}, Areas won streak: {_areasWon}, " +
                      $"Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");

            // Request difficulty decision for NEXT area
            // Set flag to allow action processing (prevents DecisionRequester interference)
            _decisionPending = true;
            RequestDecision();

            // DO NOT call EndEpisode() here - let it happen on next area enter
            // This ensures the action is processed before episode reset
        }

        /// <summary>
        /// Called when entering new area.
        /// Ends previous episode and starts new one.
        /// </summary>
        public void OnAreaEnter(int areaIndex, int totalAreas, MapType areaType = MapType.Enemy)
        {
            // End previous episode if this isn't the first area
            if (areaIndex > 0 && _isTrainingMode)
            {
                EndEpisode();
            }

            _currentArea = areaIndex;
            _totalAreas = totalAreas;
            _nextAreaType = areaType;

            if (_difficultySettings != null &&
                _difficultySettings.CurrentLevelIndex != _lastDifficultyLevel)
            {
                OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
                Debug.Log($"[DDAAgent] Area {areaIndex}: Difficulty = {_difficultySettings.GetLevelName()}");
                _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
            }
        }

        /// <summary>
        /// Called when training run starts (beginning of episode).
        /// </summary>
        public void OnRunStart()
        {
            _currentArea = 0;
            _areasWon = 0;
            _battleStartHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _lastDifficultyLevel = -1;
            _lastBattleStartHP = 0;
            _lastBattleEndHP = 0;
            _playerLevel = 1;
            _nextAreaType = MapType.Enemy;
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
            int expectedTurns = Mathf.Max(1, _expectedTurnsPerBattle);
            float turnNormalized = _turnCount / (float)expectedTurns;
            sensor.AddObservation(Mathf.Clamp01(turnNormalized));

            // State 4: Current difficulty normalized (0-1)
            float difficultyNormalized = _difficultySettings != null
                ? _difficultySettings.GetNormalizedDifficulty()
                : 0.5f;
            sensor.AddObservation(difficultyNormalized);

            // State 5: Area progress normalized (0-1)
            float areaProgress = _totalAreas > 0 ? (float)_currentArea / _totalAreas : 0f;
            sensor.AddObservation(Mathf.Clamp01(areaProgress));

            // State 6: Win streak normalized (-1 to 1)
            float winStreak = Mathf.Clamp((_areasWon - 1) / 5f, -1f, 1f);
            sensor.AddObservation(winStreak);

            // State 7: Player level normalized (0-1)
            // Max expected level around 10
            float playerLevelNormalized = Mathf.Clamp01(_playerLevel / 10f);
            sensor.AddObservation(playerLevelNormalized);

            // State 8: Next area type (one-hot encoded would be better, but using single value)
            // Rest=0, Enemy=0.33, Shop=0.67, Boss=1.0
            float areaTypeNormalized = _nextAreaType switch
            {
                MapType.Rest => 0.0f,
                MapType.Enemy => 0.33f,
                MapType.Shop => 0.67f,
                MapType.Boss => 1.0f,
                _ => 0.33f
            };
            sensor.AddObservation(areaTypeNormalized);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // SAFETY: Only process ONE action per area completion
            // This prevents DecisionRequester component from triggering multiple decisions
            if (!_decisionPending)
            {
                // Silently ignore - not our requested decision
                return;
            }

            // Reset flag - we're processing the decision now
            _decisionPending = false;

            // Also check battle state (shouldn't happen, but extra safety)
            if (_battleInProgress)
            {
                Debug.LogWarning("[DDAAgent] Ignoring action during battle - decisions only allowed between areas");
                return;
            }

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
            if (UnityEngine.Input.GetKey(KeyCode.I))
            {
                discreteActions[0] = 1;
            }
            // Decrease with D key
            else if (UnityEngine.Input.GetKey(KeyCode.D))
            {
                discreteActions[0] = 2;
            }
        }

        /// <summary>
        /// Called when battle starts. Records initial state.
        /// Does NOT request decision - difficulty was set at area enter.
        /// </summary>
        public void OnBattleStart(int playerStartHP)
        {
            _battleStartHP = playerStartHP;
            _lastBattleStartHP = playerStartHP;  // Track for area reward
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = true;

            // DO NOT request decision here - difficulty is set once per area
            // Decision was requested in previous OnAreaComplete()
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
        /// Called when battle ends. Stores state for area reward calculation.
        /// Does NOT add reward - reward only at area completion.
        /// </summary>
        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            _battleInProgress = false;

            // Store last battle HP for area reward calculation
            _lastBattleEndHP = playerEndHP;

            if (!_isTrainingMode)
            {
                // Inference mode: no reward calculation
                return;
            }

            // No battle-level reward - only area reward at OnAreaComplete
            Debug.Log($"[DDAAgent] Battle end. Won: {playerWon}, HP: {playerEndHP}/{_battleStartHP}, " +
                      $"Turns: {_turnCount}, Cumulative: {GetCumulativeReward():F3}");
        }

        /// <summary>
        /// Called when training run ends (all areas or player died).
        /// Note: Episodes are already ended per-area in OnAreaComplete.
        /// This is for logging only.
        /// </summary>
        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            // Episodes are already ended per-area
            // This method is for run-level logging only
            Debug.Log($"[DDAAgent] Run end. Won: {runWon}, Areas: {areasCompleted}/{totalAreas}");
        }

        // GetCumulativeReward() is inherited from Agent base class

        /// <summary>
        /// Calculates reward based on battle outcome.
        /// Shaped reward: Win +1, Loss -1, HP target bonus, efficiency penalty.
        /// </summary>
        private float CalculateReward(bool playerWon, int playerEndHP, int playerStartHP, int turns)
        {
            float reward = 0f;

            if (playerWon)
            {
                // Calculate HP ratio
                float hpRatio = playerStartHP > 0 ? (float)playerEndHP / playerStartHP : 0f;

                // Reward based on HP ratio - flow state is optimal
                if (hpRatio >= 0.4f && hpRatio <= 0.6f)
                {
                    // FLOW STATE - Maximum reward
                    // Peak at 50% HP with Gaussian-like curve
                    float distanceFromOptimal = Mathf.Abs(hpRatio - 0.5f);
                    float flowBonus = 1.0f * (1.0f - distanceFromOptimal * 2f);
                    reward += 1.0f + flowBonus; // Base + flow bonus = 1.0 to 2.0
                }
                else if (hpRatio > 0.6f)
                {
                    // TOO EASY - Strong penalty proportional to excess
                    // 60-70%: small penalty, 70-80%: medium, 80-100%: heavy
                    float excess = hpRatio - 0.6f;
                    float penalty = 1.5f * excess; // Much stronger penalty
                    reward += 0.5f - penalty; // Diminishing returns for easy wins
                    // 60% HP: +0.5, 80% HP: +0.2, 100% HP: -0.1
                }
                else if (hpRatio < 0.4f)
                {
                    // TOO HARD - Won but barely survived
                    float deficit = 0.4f - hpRatio;
                    float penalty = 0.5f * deficit;
                    reward += 0.5f - penalty;
                    // 30% HP: +0.35, 20% HP: +0.2, 10% HP: +0.05
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

        /// <summary>
        /// Calculates reward based on area outcome.
        /// No streak bonus - loss terminates run (reset from beginning).
        /// </summary>
        private float CalculateRewardForArea(bool won)
        {
            float reward = 0f;

            if (won)
            {
                // HP ratio is PRIMARY reward factor
                float hpRatio = GetLastBattleHPRatio();

                if (hpRatio >= 0.4f && hpRatio <= 0.6f)
                {
                    // FLOW STATE - Best reward
                    float distanceFromOptimal = Mathf.Abs(hpRatio - 0.5f);
                    float flowBonus = 0.7f * (1.0f - distanceFromOptimal * 2f);
                    reward += 1.0f + flowBonus;
                }
                else if (hpRatio > 0.6f)
                {
                    // Too easy - diminishing returns
                    float excess = hpRatio - 0.6f;
                    float penalty = 1.5f * excess;
                    reward += 0.5f - penalty;
                }
                else if (hpRatio < 0.4f)
                {
                    // Too hard - survived but barely
                    float deficit = 0.4f - hpRatio;
                    float penalty = 0.5f * deficit;
                    reward += 0.5f - penalty;
                }
                else
                {
                    // Default small win reward
                    reward += 0.3f;
                }
            }
            else
            {
                // Loss penalty - run terminates
                reward -= 1.5f;
            }

            return Mathf.Clamp(reward, -2f, 2f);
        }

        /// <summary>
        /// Get HP ratio from last battle for reward calculation.
        /// Uses tracked HP values (works in training without BattleLogger).
        /// </summary>
        private float GetLastBattleHPRatio()
        {
            if (_lastBattleStartHP <= 0)
            {
                return 1.0f; // First battle: assume full HP
            }

            return Mathf.Clamp01((float)_lastBattleEndHP / _lastBattleStartHP);
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

        /// <summary>
        /// Sets the player level for observation normalization.
        /// Called by TrainingBattleSimulator after level-up.
        /// </summary>
        public void SetPlayerLevel(int level)
        {
            _playerLevel = level;
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