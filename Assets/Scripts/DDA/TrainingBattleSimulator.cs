using System;
using System.Collections;
using UnityEngine;
using DDA;
using Player;

namespace DDA
{
    /// <summary>
    /// Simulates battles automatically for ML-Agents training.
    /// Runs rapid battle cycles without player input.
    /// Shows learning progress in real-time.
    /// </summary>
    public class TrainingBattleSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DDAAgent _ddaAgent;
        [SerializeField] private DifficultySettings _difficultySettings;
        [SerializeField] private DifficultyApplier _difficultyApplier;

        [Header("Battle Configuration")]
        [SerializeField] private int _playerBaseHP = 100;
        [SerializeField] private int _playerBaseDamage = 20;
        [SerializeField] private int _enemyBaseHP = 80;
        [SerializeField] private int _enemyBaseDamage = 15;
        [SerializeField] private float _playerAccuracy = 0.85f;
        [SerializeField] private float _enemyAccuracy = 0.80f;

        [Header("Training Configuration")]
        [SerializeField] private bool _autoTrain = true;
        [SerializeField] private float _battleDelay = 0.1f; // Delay between battles (seconds)
        [SerializeField] private float _turnDelay = 0.05f; // Delay between turns
        [SerializeField] private int _maxTurnsPerBattle = 20;

        [Header("Simulated Player Skill")]
        [SerializeField] [Range(0f, 1f)] private float _playerSkill = 0.5f; // 0=random, 1=optimal
        [SerializeField] private bool _useSmartPlayer = false;

        // Battle state
        private int _playerCurrentHP;
        private int _enemyCurrentHP;
        private int _enemyMaxHP;
        private int _enemyDamage;
        private int _turnCount;
        private int _damageDealtThisBattle;
        private bool _battleInProgress;

        // Training stats
        private int _episodeCount;
        private int _winCount;
        private int _totalTurns;
        private float _totalReward;
        private float _lastReward;
        private int _consecutiveWins;
        private int _consecutiveLosses;

        // Events for UI
        public event Action<int, int, int> OnBattleStateChanged; // (playerHP, enemyHP, turn)
        public event Action<bool, float, int> OnBattleEnded; // (won, reward, episode)
        public event Action<int> OnDifficultyChanged; // (level)
        public event Action<TrainingStats> OnStatsUpdated;

        public static TrainingBattleSimulator Instance { get; private set; }

        // Public properties for UI
        public int EpisodeCount => _episodeCount;
        public int WinCount => _winCount;
        public float WinRate => _episodeCount > 0 ? (float)_winCount / _episodeCount : 0f;
        public float AvgReward => _episodeCount > 0 ? _totalReward / _episodeCount : 0f;
        public float LastReward => _lastReward;
        public int CurrentDifficulty => _difficultySettings?.CurrentLevelIndex ?? 2;
        public string DifficultyName => _difficultySettings?.GetLevelName() ?? "Normal";
        public int PlayerHP => _playerCurrentHP;
        public int EnemyHP => _enemyCurrentHP;
        public int TurnCount => _turnCount;

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

        private void Start()
        {
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }

            if (_ddaAgent == null)
            {
                _ddaAgent = FindObjectOfType<DDAAgent>();
            }

            if (_autoTrain)
            {
                StartCoroutine(TrainingLoop());
            }
        }

        private IEnumerator TrainingLoop()
        {
            while (true)
            {
                if (!_battleInProgress)
                {
                    yield return StartCoroutine(RunBattleEpisode());
                }
                yield return new WaitForSeconds(_battleDelay);
            }
        }

        private IEnumerator RunBattleEpisode()
        {
            _battleInProgress = true;
            _episodeCount++;

            // Apply difficulty multipliers to enemy
            float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
            float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;

            _enemyMaxHP = Mathf.RoundToInt(_enemyBaseHP * hpMult);
            _enemyDamage = Mathf.RoundToInt(_enemyBaseDamage * dmgMult);

            // Initialize battle state
            _playerCurrentHP = _playerBaseHP;
            _enemyCurrentHP = _enemyMaxHP;
            _turnCount = 0;
            _damageDealtThisBattle = 0;

            // Notify DDA agent battle starting
            _ddaAgent?.OnBattleStart(_playerBaseHP);

            // Difficulty changed callback
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            Debug.Log($"[TrainingSim] Episode {_episodeCount} started. " +
                      $"Difficulty: {DifficultyName} (HP: {hpMult:F2}x, DMG: {dmgMult:F2}x)");

            // Run battle turns
            while (_playerCurrentHP > 0 && _enemyCurrentHP > 0 && _turnCount < _maxTurnsPerBattle)
            {
                yield return StartCoroutine(RunTurn());
                yield return new WaitForSeconds(_turnDelay);
            }

            // Determine outcome
            bool playerWon = _enemyCurrentHP <= 0;
            int playerEndHP = _playerCurrentHP;

            // Calculate reward
            _lastReward = CalculateReward(playerWon, playerEndHP, _playerBaseHP, _turnCount);

            // Update stats
            if (playerWon)
            {
                _winCount++;
                _consecutiveWins++;
                _consecutiveLosses = 0;
            }
            else
            {
                _consecutiveLosses++;
                _consecutiveWins = 0;
            }

            _totalTurns += _turnCount;
            _totalReward += _lastReward;

            // Notify DDA agent battle ended
            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            // Unsubscribe
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            // Fire events for UI
            OnBattleEnded?.Invoke(playerWon, _lastReward, _episodeCount);
            OnStatsUpdated?.Invoke(GetStats());

            Debug.Log($"[TrainingSim] Episode {_episodeCount} ended. " +
                      $"Won: {playerWon}, HP: {playerEndHP}/{_playerBaseHP}, " +
                      $"Turns: {_turnCount}, Reward: {_lastReward:F3}, " +
                      $"WinRate: {WinRate:P1}");

            _battleInProgress = false;
        }

        private IEnumerator RunTurn()
        {
            _turnCount++;

            // --- Player Turn ---
            int playerDamage = SimulatePlayerAttack();
            if (playerDamage > 0)
            {
                _enemyCurrentHP = Mathf.Max(0, _enemyCurrentHP - playerDamage);
                _damageDealtThisBattle += playerDamage;
            }

            OnBattleStateChanged?.Invoke(_playerCurrentHP, _enemyCurrentHP, _turnCount);

            if (_enemyCurrentHP <= 0)
            {
                yield break; // Enemy defeated
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            int enemyDamage = SimulateEnemyAttack();
            if (enemyDamage > 0)
            {
                _playerCurrentHP = Mathf.Max(0, _playerCurrentHP - enemyDamage);
            }

            OnBattleStateChanged?.Invoke(_playerCurrentHP, _enemyCurrentHP, _turnCount);

            // Notify DDA agent turn ended
            _ddaAgent?.OnTurnEnd(playerDamage);
        }

        private int SimulatePlayerAttack()
        {
            // Check hit (accuracy)
            if (UnityEngine.Random.value > _playerAccuracy)
            {
                return 0; // Miss
            }

            // Base damage with variance
            int damage = _playerBaseDamage;

            // Add variance (±20%)
            damage = Mathf.RoundToInt(damage * UnityEngine.Random.Range(0.8f, 1.2f));

            // Smart player optimization
            if (_useSmartPlayer)
            {
                // Use skill to improve damage
                float skillBonus = _playerSkill * 0.3f; // Up to +30% damage
                damage = Mathf.RoundToInt(damage * (1f + skillBonus));

                // Higher skill = more consistent damage
                if (UnityEngine.Random.value < _playerSkill)
                {
                    damage = Mathf.RoundToInt(damage * 1.1f); // +10% critical
                }
            }

            return damage;
        }

        private int SimulateEnemyAttack()
        {
            // Check hit (accuracy)
            if (UnityEngine.Random.value > _enemyAccuracy)
            {
                return 0; // Miss
            }

            // Base damage with variance
            int damage = _enemyDamage;
            damage = Mathf.RoundToInt(damage * UnityEngine.Random.Range(0.8f, 1.2f));

            return damage;
        }

        private float CalculateReward(bool playerWon, int playerEndHP, int playerStartHP, int turns)
        {
            float reward = 0f;

            if (playerWon)
            {
                // Base win reward
                reward += 1.0f;

                // Calculate HP ratio
                float hpRatio = (float)playerEndHP / playerStartHP;

                // HP Target bonus (flow state: 40-60% HP remaining)
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

            // Efficiency penalty (turns over expected ~10)
            int expectedTurns = 10;
            if (turns > expectedTurns)
            {
                float efficiencyPenalty = 0.01f * (turns - expectedTurns);
                reward -= efficiencyPenalty;
            }

            return Mathf.Clamp(reward, -2f, 2f);
        }

        private void HandleDifficultyChanged(int newLevel)
        {
            OnDifficultyChanged?.Invoke(newLevel);
            Debug.Log($"[TrainingSim] Difficulty changed to: {DifficultyName}");
        }

        /// <summary>
        /// Gets current training statistics.
        /// </summary>
        public TrainingStats GetStats()
        {
            return new TrainingStats
            {
                EpisodeCount = _episodeCount,
                WinCount = _winCount,
                WinRate = WinRate,
                AvgReward = AvgReward,
                LastReward = _lastReward,
                TotalReward = _totalReward,
                CurrentDifficulty = CurrentDifficulty,
                DifficultyName = DifficultyName,
                ConsecutiveWins = _consecutiveWins,
                ConsecutiveLosses = _consecutiveLosses,
                AvgTurnsPerBattle = _episodeCount > 0 ? (float)_totalTurns / _episodeCount : 0f,
            };
        }

        /// <summary>
        /// Resets all training statistics.
        /// </summary>
        public void ResetStats()
        {
            _episodeCount = 0;
            _winCount = 0;
            _totalTurns = 0;
            _totalReward = 0;
            _lastReward = 0;
            _consecutiveWins = 0;
            _consecutiveLosses = 0;
        }

        /// <summary>
        /// Sets player skill level for simulation.
        /// </summary>
        public void SetPlayerSkill(float skill)
        {
            _playerSkill = Mathf.Clamp01(skill);
        }

        /// <summary>
        /// Enables or disables auto training.
        /// </summary>
        public void SetAutoTrain(bool enabled)
        {
            _autoTrain = enabled;
        }
    }

    /// <summary>
    /// Training statistics data structure for UI display.
    /// </summary>
    [Serializable]
    public struct TrainingStats
    {
        public int EpisodeCount;
        public int WinCount;
        public float WinRate;
        public float AvgReward;
        public float LastReward;
        public float TotalReward;
        public int CurrentDifficulty;
        public string DifficultyName;
        public int ConsecutiveWins;
        public int ConsecutiveLosses;
        public float AvgTurnsPerBattle;
    }
}