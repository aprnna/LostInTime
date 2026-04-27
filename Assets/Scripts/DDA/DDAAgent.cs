using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Player;
using Playfab;
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
            _currentArea = 0;
            _areasWon = 0;

            // Reset difficulty to normal for new episode
            if (_difficultySettings != null)
            {
                _difficultySettings.ResetToNormal();
            }
        }

        /// <summary>
        /// Called when entering new area.
        /// Does NOT request decision - difficulty was already set for this area.
        /// </summary>
        public void OnAreaEnter(int areaIndex, int totalAreas)
        {
            _currentArea = areaIndex;
            _totalAreas = totalAreas;

            // DO NOT request decision here - difficulty was set at end of previous area
            // First area uses default difficulty

            if (_difficultySettings != null &&
                _difficultySettings.CurrentLevelIndex != _lastDifficultyLevel)
            {
                OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
                Debug.Log($"[DDAAgent] Area {areaIndex}: Difficulty = {_difficultySettings.GetLevelName()}");
                _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
            }
        }

        /// <summary>
        /// Called when area completed (won or lost).
        /// This ends the episode (1 episode = 1 area).
        /// Requests difficulty decision for NEXT area.
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
            RequestDecision();

            // End episode (1 episode = 1 area)
            EndEpisode();
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
            _lastDifficultyLevel = -1;
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
        /// Called when battle ends. Accumulates reward.
        /// Does NOT end episode - episode ends at OnAreaComplete.
        /// </summary>
        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            _battleInProgress = false;

            if (!_isTrainingMode)
            {
                // Inference mode: no reward calculation
                return;
            }

            // Calculate reward for this battle and accumulate
            float reward = CalculateReward(playerWon, playerEndHP, _battleStartHP, _turnCount);
            AddReward(reward);

            Debug.Log($"[DDAAgent] Battle end. Won: {playerWon}, HP: {playerEndHP}/{_battleStartHP}, " +
                      $"Turns: {_turnCount}, Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");
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

        /// <summary>
        /// Calculates reward based on area outcome.
        /// </summary>
        private float CalculateRewardForArea(bool won)
        {
            float reward = 0f;

            if (won)
            {
                // Base win reward
                reward += 1.0f;

                // Win streak bonus
                reward += 0.1f * Mathf.Min(_areasWon, 5);

                // HP ratio consideration (from last battle)
                float hpRatio = GetLastBattleHPRatio();
                if (hpRatio >= 0.4f && hpRatio <= 0.6f)
                {
                    // Flow state bonus
                    reward += 0.3f;
                }
                else if (hpRatio > 0.6f)
                {
                    // Too easy - small penalty
                    reward -= 0.05f * (hpRatio - 0.6f);
                }
                else if (hpRatio < 0.4f)
                {
                    // Too hard - small penalty
                    reward -= 0.05f * (0.4f - hpRatio);
                }
            }
            else
            {
                // Loss penalty
                reward -= 1.0f;
            }

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