    using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Player;

namespace DDA
{

    public class DDAAgent : Agent
    {
        [Header("References")] [SerializeField]
        private DifficultySettings _difficultySettings;

        [SerializeField] private DifficultyApplier _difficultyApplier;

        [Header("State Configuration")] [SerializeField]
        private int _expectedTurnsPerBattle = 10;

        [SerializeField] private bool _isTrainingMode = true;

        // --- Battle state (per-battle) ---
        private int _battleStartHP;
        private int _battleEndHP;
        private int _damageDealt;
        private int _turnCount;
        private bool _battleInProgress;

        // --- Battle phase features ---
        private float _currentHPRatio = 1f;
        private float _resourceDepletion = 0f;
        private float _enemyHPRatio = 1f;
        private int _swordUsesRemaining = 15;
        private int _gunUsesRemaining = 10;
        private int _defendUsesRemaining = 3;

        // --- Area / Run state ---
        private int _currentArea;
        private int _totalAreas;
        private int _playerLevel = 1;
        private MapType _nextAreaType = MapType.Enemy;

        // --- Running win-rate window (last 20 battles) ---
        private const int WIN_WINDOW = 20;
        private bool[] _winHistory = new bool[WIN_WINDOW];
        private int _winHistoryIdx = 0;
        private int _winHistoryCount = 0; // how many slots filled
        private int _totalBattles = 0;

        // --- Episode control ---
        private bool _decisionPending = false;
        private int _lastDifficultyLevel = -1;

        // --- Accumulated reward dalam satu area (bisa multi-battle) ---
        private float _areaAccumulatedReward = 0f;

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
            // Reset hanya state yang bersifat per-episode
            _battleStartHP = 0;
            _battleEndHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _areaAccumulatedReward = 0f;

            // Reset battle phase features
            _currentHPRatio = 1f;
            _resourceDepletion = 0f;
            _enemyHPRatio = 1f;
            _swordUsesRemaining = 15;
            _gunUsesRemaining = 10;
            _defendUsesRemaining = 3;
        }

        /// <summary>
        /// 12 observasi — SEMUA dinormalisasi ke [0,1]
        /// Battle phase aware untuk better decision making.
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. HP ratio SETELAH battle terakhir (0=mati, 1=full)
            float hpRatio = _battleStartHP > 0
                ? Mathf.Clamp01((float)_battleEndHP / _battleStartHP)
                : 1.0f;
            sensor.AddObservation(hpRatio);

            // 2. Running win rate window terakhir (target ~0.6)
            float winRate = GetRunningWinRate();
            sensor.AddObservation(winRate);

            // 3. Turn count normalized
            sensor.AddObservation(Mathf.Clamp01(_turnCount / (float)Mathf.Max(1, _expectedTurnsPerBattle)));

            // 4. Difficulty normalized
            float diffNorm = _difficultySettings != null
                ? _difficultySettings.GetNormalizedDifficulty()
                : 0.5f;
            sensor.AddObservation(diffNorm);

            // 5. Area progress
            sensor.AddObservation(_totalAreas > 0 ? Mathf.Clamp01((float)_currentArea / _totalAreas) : 0f);

            // 6. Player level normalized (maks 10)
            sensor.AddObservation(Mathf.Clamp01(_playerLevel / 10f));

            // 7. Area type
            float areaTypeNorm = _nextAreaType switch
            {
                MapType.Rest => 0.0f,
                MapType.Enemy => 0.33f,
                MapType.Shop => 0.67f,
                MapType.Boss => 1.0f,
                _ => 0.33f
            };
            sensor.AddObservation(areaTypeNorm);

            // 8. Damage dealt ratio (relatif ke HP awal, cap di 2.0 lalu scale)
            float dmgRatio = _battleStartHP > 0
                ? Mathf.Clamp01(_damageDealt / (float)(_battleStartHP * 2))
                : 0f;
            sensor.AddObservation(dmgRatio);

            // === BATTLE PHASE FEATURES ===

            // 9. Current HP ratio (real-time during battle)
            sensor.AddObservation(_currentHPRatio);

            // 10. Resource depletion (actions used ratio)
            sensor.AddObservation(_resourceDepletion);

            // 11. Enemy HP ratio (remaining enemy health)
            sensor.AddObservation(_enemyHPRatio);

            // 12. Critical phase flag (HP < 30%)
            float criticalFlag = _currentHPRatio < 0.3f ? 1f : 0f;
            sensor.AddObservation(criticalFlag);
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
        // Battle lifecycle hooks (dipanggil dari TrainingBattleSimulator)
        // ----------------------------------------------------------------

        public void OnRunStart()
        {
            _currentArea = 0;
            _totalAreas = 0;
            _battleStartHP = 0;
            _battleEndHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            _decisionPending = false;
            _lastDifficultyLevel = -1;
            _areaAccumulatedReward = 0f;
            _playerLevel = 1;
            _nextAreaType = MapType.Enemy;
            // Jangan reset win history — biarkan continuous
        }

        public void OnAreaEnter(int areaIndex, int totalAreas, MapType areaType = MapType.Enemy)
        {
            // End episode SEBELUM state di-update → reward sudah ter-flush di OnAreaComplete
            if (areaIndex > 0 && _isTrainingMode)
                EndEpisode(); // ← episode lama berakhir DI SINI

            _currentArea = areaIndex;
            _totalAreas = totalAreas;
            _nextAreaType = areaType;
            _areaAccumulatedReward = 0f;
        }

        public void OnBattleStart(int playerStartHP)
        {
            _battleStartHP = playerStartHP;
            _battleEndHP = playerStartHP; // default jika tidak ada update
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
            _battleEndHP = playerEndHP;

            // Simpan ke win history
            _winHistory[_winHistoryIdx % WIN_WINDOW] = playerWon;
            _winHistoryIdx++;
            _winHistoryCount = Mathf.Min(_winHistoryCount + 1, WIN_WINDOW);
            _totalBattles++;

            // Reward per-battle (kecil, sebagai shaped reward)
            if (_isTrainingMode)
            {
                float battleReward = CalculateBattleReward(playerWon, playerEndHP, _battleStartHP, _turnCount);
                _areaAccumulatedReward += battleReward;
                // Tidak di-AddReward di sini — tunggu area complete
            }

            Debug.Log($"[DDAAgent] Battle end. Won={playerWon}, HP={playerEndHP}/{_battleStartHP}, " +
                      $"WinRate={GetRunningWinRate():P1}, Difficulty={_difficultySettings?.GetLevelName()}");
        }

        public void OnAreaComplete(bool areaWon)
        {
            if (!_isTrainingMode) return;

            // Reward utama: seberapa dekat win rate ke target 0.6
            float finalReward = _areaAccumulatedReward + CalculateFlowStateBonus();

            AddReward(finalReward);

            Debug.Log($"[DDAAgent] Area {_currentArea} complete. Won={areaWon}, " +
                      $"Reward={finalReward:F3}, Cumulative={GetCumulativeReward():F3}, " +
                      $"WinRate={GetRunningWinRate():P1}");

            // Request decision untuk area BERIKUTNYA
            _decisionPending = true;
            RequestDecision();
            // EndEpisode akan dipanggil di OnAreaEnter berikutnya
        }

        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            // Run-level bonus/penalty kecil
            if (_isTrainingMode)
            {
                float runBonus = runWon ? 0.2f : -0.1f;
                AddReward(runBonus);
                // Episode akhir
                EndEpisode();
            }
        }

        public void SetPlayerLevel(int level) => _playerLevel = level;
        public void SetTrainingMode(bool v) => _isTrainingMode = v;

        /// <summary>
        /// Updates battle phase features from simulator.
        /// Call each turn to provide real-time battle state.
        /// </summary>
        public void UpdateBattlePhase(float hpRatio, float resourceDepletion, float enemyHPRatio,
            int swordUses, int gunUses, int defendUses)
        {
            _currentHPRatio = hpRatio;
            _resourceDepletion = resourceDepletion;
            _enemyHPRatio = enemyHPRatio;
            _swordUsesRemaining = swordUses;
            _gunUsesRemaining = gunUses;
            _defendUsesRemaining = defendUses;
        }

        // ----------------------------------------------------------------
        // Reward helpers
        // ----------------------------------------------------------------

        /// <summary>
        /// Reward per-battle: fokus pada HP ratio, BUKAN win/lose binary.
        /// Range: [-0.3, +0.5] agar tidak terlalu dominan.
        /// </summary>
        private float CalculateBattleReward(bool won, int endHP, int startHP, int turns)
        {
            if (!won) return -0.3f; // Penalti kalah, tapi tidak terlalu besar

            float hpRatio = startHP > 0 ? (float)endHP / startHP : 0f;

            // Target HP zone: 40-60% (flow state)
            // Gaussian-like bell curve dengan peak di 50%
            float target = 0.50f;
            float width = 0.10f; // half-width of zone
            float dist = Mathf.Abs(hpRatio - target);

            float hpScore;
            if (dist <= width)
                hpScore = 0.5f * (1f - dist / width); // +0.0 to +0.5
            else
                hpScore = -0.1f * ((dist - width) / (1f - width)); // kecil negatif di ekstrem

            // Efficiency: slight bonus untuk turns normal
            float efficiencyBonus = 0f;
            if (turns <= _expectedTurnsPerBattle * 1.5f)
                efficiencyBonus = 0.05f;

            return Mathf.Clamp(hpScore + efficiencyBonus, -0.3f, 0.55f);
        }

        /// <summary>
        /// Bonus jika win rate mendekati target 0.6 (flow state).
        /// Range: [-0.2, +0.3]
        /// </summary>
        private float CalculateFlowStateBonus()
        {
            if (_winHistoryCount < 5) return 0f; // Butuh data cukup dulu

            float winRate = GetRunningWinRate();
            float targetWinRate = 0.60f;
            float dist = Mathf.Abs(winRate - targetWinRate);

            // Bonus bell curve: puncak di 60% win rate
            if (dist < 0.10f)
                return 0.3f * (1f - dist / 0.10f); // 0 to +0.3
            else
                return -0.2f * Mathf.Clamp01((dist - 0.10f) / 0.30f); // 0 to -0.2
        }

        private float GetRunningWinRate()
        {
            if (_winHistoryCount == 0) return 0.5f;
            int wins = 0;
            int count = Mathf.Min(_winHistoryCount, WIN_WINDOW);
            for (int i = 0; i < count; i++)
                if (_winHistory[i])
                    wins++;
            return (float)wins / count;
        }

#if UNITY_EDITOR
        public string GetDebugState()
        {
            return $"WinRate(win{WIN_WINDOW})={GetRunningWinRate():P1} | " +
                   $"HP={_battleEndHP}/{_battleStartHP} | " +
                   $"Turns={_turnCount} | " +
                   $"Diff={_difficultySettings?.GetLevelName() ?? "N/A"}";
        }
#endif
    }
}