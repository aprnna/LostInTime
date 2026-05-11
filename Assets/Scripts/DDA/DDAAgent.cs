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
        private int _turnCount;
        private int _damageDealt;
        private bool _battleInProgress;

        // --- Observations ---
        private float _hpRatio = 1f;          // HP ratio after last battle
        private float _resourceDepletion = 0f; // Resource depletion ratio

        // --- Area / Run state ---
        private int _playerLevel = 1;

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
        private bool _isFirstArea; // Skip reward for first area (baseline difficulty)

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
            _turnCount = 0;
            _damageDealt = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _lastDifficultyLevel = -1;
            _hpRatio = 1f;
            _resourceDepletion = 0f;
            _playerLevel = 1;
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
            _areasCompleted = 0;
            _battlesWon = 0;
            _battlesTotal = 0;
            _isFirstArea = true;
        }

        /// <summary>
        /// 5 observations — all normalized to [0,1]:
        /// 1. HP Ratio
        /// 2. Turn Count
        /// 3. Player Level
        /// 4. Damage Ratio
        /// 5. Resource Depletion
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. HP Ratio (0=dead, 1=full)
            sensor.AddObservation(_hpRatio);

            // 2. Turn Count normalized (cap at 20)
            sensor.AddObservation(Mathf.Clamp01(_turnCount / 20f));

            // 3. Player Level normalized (cap at 10)
            sensor.AddObservation(Mathf.Clamp01(_playerLevel / 10f));

            // 4. Damage Ratio (damage dealt relative to battle start HP)
            float dmgRatio = _battleStartHP > 0
                ? Mathf.Clamp01(_damageDealt / (float)(_battleStartHP * 2))
                : 0f;
            sensor.AddObservation(dmgRatio);

            // 5. Resource Depletion (ratio of used actions)
            sensor.AddObservation(_resourceDepletion);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!_decisionPending) return;
            _decisionPending = false;

            if (_difficultySettings == null) return;

            int prevLevel = _difficultySettings.CurrentLevelIndex;
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
            _isFirstArea = true;

            // Reset difficulty to baseline at start of run
            _difficultySettings?.ResetToNormal();

            Debug.Log($"[DDAAgent] Run started. Difficulty reset to baseline.");
        }

        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            if (_isTrainingMode)
            {
                // Run completion bonus/penalty
                float runBonus = runWon ? 0.5f : -0.1f;
                AddReward(runBonus);

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
        public void OnAreaEnter(int areaIndex)
        {
            // Reset area-level tracking only
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
        }

        public void OnBattleStart(int playerStartHP)
        {
            // Track HP at start of first battle in area
            if (_areaStartHP == 0)
                _areaStartHP = playerStartHP;

            _battleStartHP = playerStartHP;
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

            Debug.Log($"[DDAAgent] Battle end. Won={playerWon}, HP={playerEndHP}/{_battleStartHP}, " +
                      $"HPRatio={_hpRatio:F2}, Difficulty={_difficultySettings?.GetLevelName()}, " +
                      $"BattlesWon={_battlesWon}/{_battlesTotal}");
        }

        /// <summary>
        /// Called when area is complete.
        /// Reward attribution (MDP):
        /// - First area (baseline): skip reward (not caused by agent action)
        /// - Other areas: AddReward shifts 1 step — reward from area t
        ///   is accumulated between step t-1 and step t, attributing it to
        ///   action a_{t-1} which set difficulty for area t.
        /// </summary>
        public void OnAreaComplete(bool areaWon)
        {
            _areasCompleted++;
            _areaWon = areaWon;

            if (_isTrainingMode)
            {
                if (_isFirstArea)
                {
                    // First area: skip reward (baseline difficulty, not caused by agent)
                    _isFirstArea = false;

                    Debug.Log($"[DDAAgent] Area complete (first, baseline). Won={areaWon}, " +
                              $"AreasCompleted={_areasCompleted}. Reward skipped.");
                }
                else
                {
                    // Calculate area-level reward based on overall area outcome
                    float areaReward = CalculateReward(areaWon, _areaEndHP, _areaStartHP);
                    AddReward(areaReward);

                    Debug.Log($"[DDAAgent] Area complete. Won={areaWon}, " +
                              $"AreaHP={_areaEndHP}/{_areaStartHP}, " +
                              $"AreaReward={areaReward:F3}, " +
                              $"AreasCompleted={_areasCompleted}, " +
                              $"Cumulative={GetCumulativeReward():F3}");
                }
            }

            // Request decision — agent observes current state and decides difficulty for next area
            _decisionPending = true;
            RequestDecision();
        }

        public void SetPlayerLevel(int level) => _playerLevel = level;
        public void SetTrainingMode(bool v) => _isTrainingMode = v;

        /// <summary>
        /// Update battle phase observations from simulator.
        /// Call each turn to provide real-time battle state.
        /// </summary>
        public void UpdateBattlePhase(float hpRatio, float resourceDepletion)
        {
            _hpRatio = hpRatio;
            _resourceDepletion = resourceDepletion;
        }

        // ----------------------------------------------------------------
        // Reward — based on area outcome, HP 40-60% sweet spot
        // ----------------------------------------------------------------

        /// <summary>
        /// Area-level reward function:
        /// - Area lost: -1.0
        /// - Area won with HP 40-60%: 0.9 to 1.0 (peak at 50%)
        /// - Area won with HP > 60%: 0.1 to 0.5 (too easy, decay)
        /// - Area won with HP < 40%: 0.1 to 0.5 (too hard, decay)
        /// </summary>
        public static float CalculateReward(bool won, int endHP, int startHP)
        {
            if (!won) return -1.0f;

            float hpRatio = startHP > 0 ? (float)endHP / startHP : 0f;

            // Sweet spot: HP between 40-60%
            if (hpRatio >= 0.4f && hpRatio <= 0.6f)
            {
                // Peak at 50%, smooth within zone
                float distFromCenter = Mathf.Abs(hpRatio - 0.5f);
                return 1.0f - distFromCenter; // 0.9 to 1.0
            }

            // Outside sweet spot — scale down
            if (hpRatio > 0.6f)
            {
                // Too easy: linear decay from 0.5 at 60% to 0.1 at 100%
                return 0.5f - 0.4f * ((hpRatio - 0.6f) / 0.4f);
            }
            else
            {
                // Too hard (HP < 40%): linear decay from 0.5 at 40% to 0.1 at 0%
                return 0.5f - 0.4f * ((0.4f - hpRatio) / 0.4f);
            }
        }

#if UNITY_EDITOR
        public string GetDebugState()
        {
            return $"HPRatio={_hpRatio:F2} | " +
                   $"Turns={_turnCount} | " +
                   $"Level={_playerLevel} | " +
                   $"DmgRatio={(_battleStartHP > 0 ? (_damageDealt / (float)(_battleStartHP * 2)).ToString("F2") : "N/A")} | " +
                   $"ResDepl={_resourceDepletion:F2} | " +
                   $"Diff={_difficultySettings?.GetLevelName() ?? "N/A"} | " +
                   $"AreasCompleted={_areasCompleted} | " +
                   $"WinRate={(_battlesTotal > 0 ? ((float)_battlesWon / _battlesTotal).ToString("F2") : "N/A")}";
        }
#endif
    }
}