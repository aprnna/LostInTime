using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace DDA
{
    public class DDAAgent : Agent
    {
        [Header("References")] [SerializeField]
        private DifficultySettings _difficultySettings;

        [SerializeField] private DifficultyApplier _difficultyApplier;

        [Header("State Configuration")] [SerializeField]
        private bool _isTrainingMode = true;

        // --- Battle state (per-battle) ---
        private int _battleStartHP;
        private int _totalEnemyHP; // Total max HP of all enemies in battle
        private int _turnCount;
        private int _damageDealt;
        private bool _battleInProgress;

        // --- Observations ---
        private float _hpRatio = 1f;          // HP ratio after last battle
        private float _resourceDepletion = 0f; // Resource depletion ratio
        private float _areaProgressRatio = 0f; // Area progress ratio (areasCompleted / totalAreas)
        private float _currentDifficultyNorm = 0.5f; // Current difficulty normalized (index/4, starts at Normal=0.5)

        // --- Remaining Resources (NEW) ---
        private float _swordRemaining = 1f;   // Sword uses remaining (uses/max)
        private float _gunRemaining = 1f;     // Gun uses remaining (uses/max)
        private float _defendRemaining = 1f;  // Defend uses remaining (uses/max)

        // --- Area / Run state ---
        private int _playerLevel = 1;
        private int _totalAreas = 12;

        // --- Episode control ---
        private bool _decisionPending = false;
        private int _lastDifficultyLevel = -1;

        // --- Area HP tracking (for area-level reward) ---
        private int _areaStartHP;
        private int _areaEndHP;
        private bool _areaWon;

        // --- Run-level tracking ---
        private int _areasCompleted;
        private int _battlesWon;
        private int _battlesTotal;

        // --- Environment ID for multi-env logging ---
        private int _envId = 0;

        public event Action<int> OnDifficultyChanged;

        // ----------------------------------------------------------------
        // ML-Agents overrides
        // ----------------------------------------------------------------

        public override void Initialize()
        {
            if (_difficultySettings == null)
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
        }

        public override void OnEpisodeBegin()
        {
            // Full reset — 1 episode = 1 full run (12 areas)
            _battleStartHP = 0;
            _totalEnemyHP = 0;
            _turnCount = 0;
            _damageDealt = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _lastDifficultyLevel = -1;
            _hpRatio = 1f;
            _resourceDepletion = 0f;
            _areaProgressRatio = 0f;
            _currentDifficultyNorm = 0.5f; // Normal difficulty
            _swordRemaining = 1f;
            _gunRemaining = 1f;
            _defendRemaining = 1f;
            _playerLevel = 1;
            _totalAreas = 12;
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
            _areasCompleted = 0;
            _battlesWon = 0;
            _battlesTotal = 0;
        }

        /// <summary>
        /// 10 observations — all normalized to [0,1]:
        /// 1. HP Ratio
        /// 2. Turn Count
        /// 3. Player Level
        /// 4. Damage Ratio
        /// 5. Resource Depletion
        /// 6. Area Progress Ratio
        /// 7. Current Difficulty
        /// 8. Sword Remaining
        /// 9. Gun Remaining
        /// 10. Defend Remaining
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. HP Ratio (0=dead, 1=full)
            sensor.AddObservation(_hpRatio);

            // 2. Turn Count normalized (cap at 20)
            sensor.AddObservation(Mathf.Clamp01(_turnCount / 20f));

            // 3. Player Level normalized (cap at 10)
            sensor.AddObservation(Mathf.Clamp01(_playerLevel / 10f));

            // 4. Damage Ratio (damage dealt relative to total enemy HP)
            // Measures offensive effectiveness: how much damage player dealt vs total enemy HP
            float dmgRatio = _totalEnemyHP > 0
                ? Mathf.Clamp01(_damageDealt / (float)_totalEnemyHP)
                : 0f;
            sensor.AddObservation(dmgRatio);

            // 5. Resource Depletion (ratio of used actions)
            sensor.AddObservation(_resourceDepletion);

            // 6. Area Progress Ratio (how far into the run)
            sensor.AddObservation(_areaProgressRatio);

            // 7. Current Difficulty (normalized: index/4 for 5 levels)
            sensor.AddObservation(_currentDifficultyNorm);

            // 8-10. Remaining Resources (uses remaining / max uses)
            sensor.AddObservation(_swordRemaining);
            sensor.AddObservation(_gunRemaining);
            sensor.AddObservation(_defendRemaining);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!_decisionPending) return;
            _decisionPending = false;

            if (_difficultySettings == null) return;

            int prevLevel = _difficultySettings.CurrentLevelIndex;
            string prevDiffName = _difficultySettings.GetLevelName();
            int action = actions.DiscreteActions[0];

            switch (action)
            {
                case 0: /* Maintain */ break;
                case 1:
                    _difficultySettings.IncreaseDifficulty();
                    break;
                case 2:
                    _difficultySettings.DecreaseDifficulty();
                    break;
            }

            // Update difficulty observation after action
            _currentDifficultyNorm = _difficultySettings.CurrentLevelIndex / 4f;

            string actionName = action switch
            {
                0 => "Maintain",
                1 => "Increase",
                2 => "Decrease",
                _ => "Unknown"
            };

            TrainingLogger.LogAgentAction(action, actionName, prevLevel,
                _difficultySettings.CurrentLevelIndex, prevDiffName,
                _difficultySettings.GetLevelName(), _envId);

            if (_difficultySettings.CurrentLevelIndex != prevLevel)
            {
                _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
                OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var disc = actionsOut.DiscreteActions;
            disc[0] = 0;
            if (UnityEngine.Input.GetKey(KeyCode.I)) disc[0] = 1;
            else if (UnityEngine.Input.GetKey(KeyCode.D)) disc[0] = 2;
        }

        // ----------------------------------------------------------------
        // Run lifecycle hooks (1 episode = 1 full run)
        // ----------------------------------------------------------------

        public void OnRunStart()
        {
            // Reset run-level counters (OnEpisodeBegin handles full reset)
            _areasCompleted = 0;
            _battlesWon = 0;
            _battlesTotal = 0;

            // Reset difficulty to baseline at start of run
            _difficultySettings?.ResetToNormal();

            // Reset progress and difficulty observations
            _areaProgressRatio = 0f;
            _currentDifficultyNorm = 0.5f; // Normal difficulty (index 2 / 4)

            // Reset remaining resources to full
            _swordRemaining = 1f;
            _gunRemaining = 1f;
            _defendRemaining = 1f;

            TrainingLogger.LogMessage($"DDAAgent: Run started. Difficulty reset to baseline. " +
                $"Epsilon={(_isTrainingMode ? "training" : "inference")}", _envId);
        }

        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            if (_isTrainingMode)
            {
                // Run completion bonus/penalty
                float runBonus = runWon ? 0.5f : -0.1f;
                AddReward(runBonus);

                TrainingLogger.LogMessage($"DDAAgent: Run end. Won={runWon}, Areas={areasCompleted}/{totalAreas}, " +
                    $"Cumulative={GetCumulativeReward():F3}, RunBonus={runBonus:F2}", _envId);
                Debug.Log($"[DDAAgent] Run end. Won={runWon}, Areas={areasCompleted}/{totalAreas}, " +
                          $"Cumulative={GetCumulativeReward():F3}, " +
                          $"RunBonus={runBonus:F2}");
            }

            // EndEpisode — ONLY place this is called
            EndEpisode();
        }

        // ----------------------------------------------------------------
        // Area lifecycle hooks (1 episode spans all 12 areas)
        // ----------------------------------------------------------------

        /// <summary>
        /// Called when entering a new area.
        /// No EndEpisode — episode spans the entire run.
        /// </summary>
        public void OnAreaEnter(int areaIndex, MapType areaType, int totalAreas)
        {
            // Reset area-level tracking only
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
        }

        public void OnBattleStart(int playerStartHP, int totalEnemyHP)
        {
            // Track HP at start of first battle in area
            if (_areaStartHP == 0)
                _areaStartHP = playerStartHP;

            _battleStartHP = playerStartHP;
            _totalEnemyHP = totalEnemyHP;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = true;
        }

        public void OnTurnEnd(int damageDealtThisTurn)
        {
            _damageDealt += damageDealtThisTurn;
            _turnCount++;
        }

        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            _battleInProgress = false;
            _battlesTotal++;

            // Update HP ratio observation
            _hpRatio = _battleStartHP > 0
                ? Mathf.Clamp01((float)playerEndHP / _battleStartHP)
                : 1.0f;

            // Track area-level state (last battle in area)
            _areaEndHP = playerEndHP;
            if (playerWon) _battlesWon++;

            // No reward here — reward is given at OnAreaComplete

            // Note: _areasCompleted is NOT yet incremented here (that happens in OnAreaComplete)
            // But we still want to log for debugging. The caller (TrainingBattleSimulator) knows
            // the actual area index and logs it separately.
            // Debug.Log($"[DDAAgent] Battle end. Won={playerWon}, HP={playerEndHP}/{_battleStartHP}, " +
            //           $"HPRatio={_hpRatio:F2}, Difficulty={_difficultySettings?.GetLevelName()}, " +
            //           $"BattlesWon={_battlesWon}/{_battlesTotal}");
        }

        /// <summary>
        /// Called when area is complete.
        /// Reward attribution (MDP):
        /// - First area (baseline): skip reward (not caused by agent action)
        /// - Other areas: AddReward shifts 1 step — reward from area t
        ///   is accumulated between step t-1 and step t, attributing it to
        ///   action a_{t-1} which set difficulty for area t.
        /// Progressive weighting: later areas have higher weight.
        /// </summary>
        public void OnAreaComplete(bool areaWon)
        {
            _areasCompleted++;
            _areaWon = areaWon;

            // Update progress ratio observation
            _areaProgressRatio = (float)_areasCompleted / _totalAreas;

            // Calculate progressive weight with base 0.5 (ensures early areas still matter)
            // Range: 0.5 (area 1) to 1.0 (area 12)
            float progressWeight = 0.5f + 0.5f * ((float)_areasCompleted / _totalAreas);

            if (_isTrainingMode)
            {
                // Calculate area-level reward based on overall area outcome
                // Reward is attributed to the action that set difficulty for this area
                float baseReward = CalculateReward(areaWon, _areaEndHP, _areaStartHP);

                // Apply progressive weighting to both wins and losses
                float weightedReward = baseReward * progressWeight;
                AddReward(weightedReward);
            }

            // Request decision — agent observes current state and decides difficulty for next area
            _decisionPending = true;
            RequestDecision();
        }

        public void SetPlayerLevel(int level) => _playerLevel = level;
        public void SetTrainingMode(bool v) => _isTrainingMode = v;
        public void SetEnvId(int envId) => _envId = envId;

        /// <summary>
        /// Sets the shared DifficultySettings instance (called by the training simulator).
        /// CRITICAL for convergence: the agent and the TrainingBattleSimulator MUST share one
        /// DifficultySettings instance, otherwise the agent mutates a separate object and its
        /// Increase/Decrease actions never reach the battles the simulator runs. That disconnect
        /// was the root cause of Q-values collapsing to ~0 (no action had any effect on reward).
        /// </summary>
        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }

        public float GetCumulativeRewardValue => GetCumulativeReward();

        /// <summary>
        /// Encode MapType enum to float [0,1]:
        /// Enemy=0, Boss=0.33, Rest=0.67, Shop=1.0
        /// </summary>
        public static float EncodeMapType(MapType type)
        {
            return type switch
            {
                MapType.Enemy => 0.0f,
                MapType.Boss => 0.33f,
                MapType.Rest => 0.67f,
                MapType.Shop => 1.0f,
                _ => 0.0f
            };
        }

        /// <summary>
        /// Update battle phase observations from simulator.
        /// Call each turn to provide real-time battle state.
        /// </summary>
        public void UpdateBattlePhase(float hpRatio, float resourceDepletion,
            float swordRemaining, float gunRemaining, float defendRemaining)
        {
            _hpRatio = hpRatio;
            _resourceDepletion = resourceDepletion;
            _swordRemaining = swordRemaining;
            _gunRemaining = gunRemaining;
            _defendRemaining = defendRemaining;
        }

        // ----------------------------------------------------------------
        // Reward — parabolic sweet spot at HP 50%, linear decay outside
        // ----------------------------------------------------------------
        public static float CalculateReward(bool won, int endHP, int startHP)
        {
            // Loss: softened from -1.0 to -0.5 so early-training deaths do not dominate the
            // mean reward and obscure learning progress.
            if (!won) return -0.5f;

            // Clamp to [0,1]: endHP can exceed startHP (level-up heal mid-area), which would
            // otherwise push the too-easy branch negative and reward a win below zero.
            float hpRatio = startHP > 0 ? Mathf.Clamp01((float)endHP / startHP) : 0f;

            // Sweet spot 35-65% (broadened from 40-60%): parabolic peak at 50%.
            // Reward ranges ~0.44 (edges) to 1.0 (peak) — more reachable +1.0 signal.
            if (hpRatio >= 0.35f && hpRatio <= 0.65f)
                return 1.0f - 25.0f * (hpRatio - 0.5f) * (hpRatio - 0.5f);

            // Too easy (HP > 65%): small positive, encourages harder difficulty.
            if (hpRatio > 0.65f)
                return (1.0f - hpRatio) * 0.5f;

            // Too hard (HP < 35%): small positive, encourages easier difficulty.
            return hpRatio * 0.3f;
        }

#if UNITY_EDITOR
        public string GetDebugState()
        {
            float progressWeight = 0.5f + 0.5f * ((float)_areasCompleted / _totalAreas);
            return $"HPRatio={_hpRatio:F2} | " +
                   $"Turns={_turnCount} | " +
                   $"Level={_playerLevel} | " +
                   $"DmgRatio={(_totalEnemyHP > 0 ? (_damageDealt / (float)_totalEnemyHP).ToString("F2") : "N/A")} | " +
                   $"ResDepl={_resourceDepletion:F2} | " +
                   $"Diff={_difficultySettings?.GetLevelName() ?? "N/A"} | " +
                   $"Prog={_areaProgressRatio:F2} | " +
                   $"DiffNorm={_currentDifficultyNorm:F2} | " +
                   $"Sword={_swordRemaining:F2} | " +
                   $"Gun={_gunRemaining:F2} | " +
                   $"Defend={_defendRemaining:F2} | " +
                   $"ProgWeight={progressWeight:F2} | " +
                   $"AreasCompleted={_areasCompleted} | " +
                   $"WinRate={(_battlesTotal > 0 ? ((float)_battlesWon / _battlesTotal).ToString("F2") : "N/A")}";
        }
#endif
    }
}