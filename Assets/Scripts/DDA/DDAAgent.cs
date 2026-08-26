using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace DDA
{
    public struct AgentDecisionInfo
    {
        public int action;           
        public int prevLevelIndex;
        public int newLevelIndex;
        public string prevLevelName;
        public string newLevelName;
        public bool changed;          
        public int areaIndex;        
        
        public float hpRatio;
        public float turnCountNorm;
        public float playerLevelNorm;
        public float damageDealtRatio;
        public float qteAccuracy;
        public float resourceDepletion;
    }

    public class DDAAgent : Agent
    {
        [Header("References")] [SerializeField]
        private DifficultySettings _difficultySettings;

        [SerializeField] private DifficultyApplier _difficultyApplier;

        [Header("State Configuration")] [SerializeField]
        private bool _isTrainingMode = true;

        // --- Battle state (per-battle) ---
        private int _battleStartHP;
        private int _totalEnemyHP; 
        private int _turnCount;
        private int _damageDealt;
        private int _damageTaken;
        private bool _battleInProgress;

        // --- QTE tracking (per-battle) ---
        private int _successfulQTE;
        private int _totalQTEOpportunities;

        // --- Observations ---
        private float _hpRatio = 1f;          
        private float _resourceDepletion = 0f; 

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
        private int _areaTotalEnemyHP; // Total enemy HP across all battles in area

        // --- Run-level tracking ---
        private int _areasCompleted;
        private int _battlesWon;
        private int _battlesTotal;

        // --- Environment ID for multi-env logging ---
        private int _envId = 0;

        public event Action<int> OnDifficultyChanged;
        public event Action<AgentDecisionInfo> OnAgentDecision;


        public override void Initialize()
        {
            if (_difficultySettings == null)
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");

            // Log model status for debugging
            var bp = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (bp != null)
            {
                bool hasModel = bp.Model != null;
                Debug.Log($"[DDAAgent] Initialize: BehaviorType={bp.BehaviorType}, " +
                          $"hasModel={hasModel}, BehaviorName={bp.BehaviorName}");
            }
            else
            {
                Debug.LogWarning("[DDAAgent] Initialize: BehaviorParameters NOT FOUND!");
            }
        }

        public override void OnEpisodeBegin()
        {
            // Full reset — 1 episode = 1 full run (12 areas)
            _battleStartHP = 0;
            _totalEnemyHP = 0;
            _turnCount = 0;
            _damageDealt = 0;
            _damageTaken = 0;
            _successfulQTE = 0;
            _totalQTEOpportunities = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _lastDifficultyLevel = -1;
            _hpRatio = 1f;
            _resourceDepletion = 0f;
            _playerLevel = 1;
            _totalAreas = 12;
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
            _areaTotalEnemyHP = 0;
            _areasCompleted = 0;
            _battlesWon = 0;
            _battlesTotal = 0;
        }

        /// <summary>
        /// 6 observations — all normalized to [0,1]:
        ///  1. HP Ratio
        ///  2. Turn Count (cap 15)
        ///  3. Player Level (cap 5)
        ///  4. Damage Dealt Ratio (totalEnemyHP / dealt in area)
        ///  5. QTE Accuracy (successful QTE / total QTE opportunities)
        ///  6. Resource Depletion
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(_hpRatio);
            sensor.AddObservation(Mathf.Clamp01(_turnCount / 15f));
            sensor.AddObservation(Mathf.Clamp01(_playerLevel / 5f));
            float dmgDealtRatio = _damageDealt > 0
                ? Mathf.Clamp01((float)_areaTotalEnemyHP / _damageDealt)
                : 0f;
            sensor.AddObservation(dmgDealtRatio);
            float qteAccuracy = _totalQTEOpportunities > 0
                ? Mathf.Clamp01(_successfulQTE / (float)_totalQTEOpportunities)
                : 0f;
            sensor.AddObservation(qteAccuracy);
            sensor.AddObservation(_resourceDepletion);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!_decisionPending) return;
            _decisionPending = false;
            if (_difficultySettings == null) return;

            int action = actions.DiscreteActions[0];
            int prevLevel = _difficultySettings.CurrentLevelIndex;
            string prevDiffName = _difficultySettings.GetLevelName();

            _difficultySettings.SetLevel(action);

            string actionName = _difficultySettings.GetLevelName();
            bool levelChanged = _difficultySettings.CurrentLevelIndex != prevLevel;

            TrainingLogger.LogAgentAction(action, actionName, prevLevel,
                _difficultySettings.CurrentLevelIndex, prevDiffName,
                _difficultySettings.GetLevelName(), _envId);
            var decision = new AgentDecisionInfo
            {
                action = action,
                prevLevelIndex = prevLevel,
                newLevelIndex = _difficultySettings.CurrentLevelIndex,
                prevLevelName = prevDiffName,
                newLevelName = actionName,
                changed = levelChanged,
                areaIndex = _areasCompleted,
                hpRatio = _hpRatio,
                turnCountNorm = Mathf.Clamp01(_turnCount / 15f),
                playerLevelNorm = Mathf.Clamp01(_playerLevel / 5f),
                damageDealtRatio = GetDamageDealtRatio(),
                qteAccuracy = GetQTEAccuracy(),
                resourceDepletion = _resourceDepletion,
            };
            OnAgentDecision?.Invoke(decision);

            if (levelChanged)
            {
                _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
                OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
                Debug.Log($"[DDAAgent] DECISION: action={action}, {prevDiffName} → {actionName}");
            }
            else
            {
                Debug.Log($"[DDAAgent] DECISION: action={action}, stays {actionName}");
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var disc = actionsOut.DiscreteActions;
            disc[0] = 2; // Normal (default)
            if (UnityEngine.Input.GetKey(KeyCode.Alpha1)) disc[0] = 0; // Very Easy
            else if (UnityEngine.Input.GetKey(KeyCode.Alpha2)) disc[0] = 1; // Easy
            else if (UnityEngine.Input.GetKey(KeyCode.Alpha3)) disc[0] = 2; // Normal
            else if (UnityEngine.Input.GetKey(KeyCode.Alpha4)) disc[0] = 3; // Hard
            else if (UnityEngine.Input.GetKey(KeyCode.Alpha5)) disc[0] = 4; // Very Hard
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

            TrainingLogger.LogMessage($"DDAAgent: Run started. Difficulty reset to baseline. " +
                $"Epsilon={(_isTrainingMode ? "training" : "inference")}", _envId);
        }

        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            if (_isTrainingMode)
            {
                float runBonus = runWon ? 0.5f : -0.3f;
                AddReward(runBonus);

                TrainingLogger.LogMessage($"DDAAgent: Run end. Won={runWon}, Areas={areasCompleted}/{totalAreas}, " +
                    $"Cumulative={GetCumulativeReward():F3}, RunBonus={runBonus:F2}", _envId);
                Debug.Log($"[DDAAgent] Run end. Won={runWon}, Areas={areasCompleted}/{totalAreas}, " +
                          $"Cumulative={GetCumulativeReward():F3}, " +
                          $"RunBonus={runBonus:F2}");
            }

            // EndEpisode — terminal step (run completed)
            EndEpisode();
        }

        public void OnPlayerDeath(int areasCompleted, int totalAreas)
        {
            if (_isTrainingMode)
            {
                float deathPenalty = -0.5f;
                AddReward(deathPenalty);

                TrainingLogger.LogMessage($"DDAAgent: Player died! Areas={areasCompleted}/{totalAreas}, " +
                    $"Cumulative={GetCumulativeReward():F3}, DeathPenalty={deathPenalty:F2}", _envId);
                Debug.Log($"[DDAAgent] Player died! Areas={areasCompleted}/{totalAreas}, " +
                          $"Cumulative={GetCumulativeReward():F3}, " +
                          $"DeathPenalty={deathPenalty:F2}");
            }

            // EndEpisode — terminal step (player died)
            EndEpisode();
        }

        // ----------------------------------------------------------------
        // Area lifecycle hooks (1 episode spans all 12 areas)
        // ----------------------------------------------------------------

        public void OnAreaEnter(int areaIndex, MapType areaType, int totalAreas)
        {
            
            _areaStartHP = 0;
            _areaEndHP = 0;
            _areaWon = false;
            _areaTotalEnemyHP = 0;

            _damageDealt = 0;
            _damageTaken = 0;
            _successfulQTE = 0;
            _totalQTEOpportunities = 0;
            _turnCount = 0;
        }

        public void OnBattleStart(int playerStartHP, int totalEnemyHP)
        {
            // Track HP at start of first battle in area
            if (_areaStartHP == 0)
                _areaStartHP = playerStartHP;

            _battleStartHP = playerStartHP;
            _totalEnemyHP = totalEnemyHP;
            _areaTotalEnemyHP += totalEnemyHP; 
            _battleInProgress = true;
            Debug.Log($"[DDAAgent] OnBattleStart: playerHP={playerStartHP}, enemyHP={totalEnemyHP}, " +
                      $"areaTotalEnemyHP={_areaTotalEnemyHP}");
        }

        public void OnTurnEnd(int damageDealtThisTurn, int damageTakenThisTurn = 0)
        {
            _damageTaken += damageTakenThisTurn;
            _turnCount++;
            Debug.Log($"[DDAAgent] OnTurnEnd: dmgTaken={damageTakenThisTurn}, " +
                      $"totalDealt={_damageDealt}, totalTaken={_damageTaken}, turns={_turnCount}");
        }

        public void OnPlayerAttack(int damage)
        {
            _damageDealt += damage;
            Debug.Log($"[DDAAgent] OnPlayerAttack: +{damage} damage, totalDealt={_damageDealt}");
        }

        public void OnQTECompleted(bool success)
        {
            _totalQTEOpportunities++;
            if (success) _successfulQTE++;
        }

        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            _battleInProgress = false;
            _battlesTotal++;

            _hpRatio = _battleStartHP > 0
                ? Mathf.Clamp01((float)playerEndHP / _battleStartHP)
                : 1.0f;

            _areaEndHP = playerEndHP;
            if (playerWon) _battlesWon++;

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

            Debug.Log($"[DDAAgent] OnAreaComplete: won={areaWon}, areasCompleted={_areasCompleted}, " +
                      $"damageDealt={_damageDealt}, damageTaken={_damageTaken}, turns={_turnCount}, " +
                      $"hpRatio={_hpRatio:F2}, areaTotalEnemyHP={_areaTotalEnemyHP}, " +
                      $"damageDealtRatio={GetDamageDealtRatio():F2}");

            // float progressWeight = 0.5f + 0.5f * ((float)_areasCompleted / _totalAreas);

            if (_isTrainingMode)
            {
                float baseReward = CalculateReward(areaWon, _areaEndHP, _areaStartHP);

                // float weightedReward = baseReward * progressWeight;
                AddReward(baseReward);
            }

            _decisionPending = true;
            RequestDecision();
        }

        public void SetPlayerLevel(int level) => _playerLevel = level;
        public void SetTrainingMode(bool v) => _isTrainingMode = v;
        public void SetEnvId(int envId) => _envId = envId;

        // Public getters for DDA logging
        public float GetHpRatio() => _hpRatio;
        public int GetTurnCount() => _turnCount;
        public int GetDamageDealt() => _damageDealt;
        public int GetDamageTaken() => _damageTaken;
        public float GetTurnCountNormalized() => Mathf.Clamp01(_turnCount / 15f);
        public float GetPlayerLevelNormalized() => Mathf.Clamp01(_playerLevel / 5f);
        public float GetDamageDealtRatio()
        {
            return _damageDealt > 0 ? Mathf.Clamp01((float)_areaTotalEnemyHP / _damageDealt) : 0f;
        }
        public int GetDamageDealtRaw() => _damageDealt;
        public int GetAreaTotalEnemyHP() => _areaTotalEnemyHP;
        public float GetDamageTakenRatio()
        {
            int total = _damageDealt + _damageTaken;
            return total > 0 ? Mathf.Clamp01(_damageTaken / (float)total) : 0f;
        }
        public float GetQTEAccuracy() => _totalQTEOpportunities > 0 ? Mathf.Clamp01(_successfulQTE / (float)_totalQTEOpportunities) : 0f;
        public int GetSuccessfulQTE() => _successfulQTE;
        public int GetTotalQTEOpportunities() => _totalQTEOpportunities;
        public float GetResourceDepletion() => _resourceDepletion;
        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }

        public float GetCumulativeRewardValue => GetCumulativeReward();

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

        public void UpdateBattlePhase(float hpRatio, float resourceDepletion)
        {
            _hpRatio = hpRatio;
            _resourceDepletion = resourceDepletion;
        }

      
        public static float CalculateReward(bool won, int endHP, int startHP)
        {
            if (!won) return -0.5f;

            float r = startHP > 0 ? Mathf.Clamp01((float)endHP / startHP) : 0f;

            return Mathf.Exp(-8f * (r - 0.5f) * (r - 0.5f));
        }

#if UNITY_EDITOR
        public string GetDebugState()
        {
            // float progressWeight = 0.5f + 0.5f * ((float)_areasCompleted / _totalAreas);
            return $"HPRatio={_hpRatio:F2} | " +
                   $"Turns={_turnCount} | " +
                   $"Level={_playerLevel} | " +
                   $"DmgDealt={GetDamageDealtRatio():F2} | " +
                   $"QTE={GetQTEAccuracy():F2} ({_successfulQTE}/{_totalQTEOpportunities}) | " +
                   $"ResDepl={_resourceDepletion:F2} | " +
                   $"Diff={_difficultySettings?.GetLevelName() ?? "N/A"} | " +
                   // $"ProgWeight={progressWeight:F2} | " +
                   $"AreasCompleted={_areasCompleted} | " +
                   $"WinRate={(_battlesTotal > 0 ? ((float)_battlesWon / _battlesTotal).ToString("F2") : "N/A")}";
        }
#endif
    }
}