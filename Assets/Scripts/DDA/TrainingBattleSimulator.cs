using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player;

namespace DDA
{
    /// <summary>
    /// Simulates battles automatically for ML-Agents training.
    /// Game-accurate mechanics: actions, damage roulette, area progression.
    /// Shows learning progress in real-time.
    /// </summary>
    public class TrainingBattleSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DDAAgent _ddaAgent;
        [SerializeField] private DifficultySettings _difficultySettings;
        [SerializeField] private DifficultyApplier _difficultyApplier;
        [SerializeField] private MapData _mapData;

        [Header("Area Configuration")]
        [SerializeField] private bool _loadFromMapData = true;
        [SerializeField] private int _areasPerRun = 12;

        [Header("Player Configuration")]
        [SerializeField] private PlayerSO _playerData;
        [SerializeField] private float _playerAccuracy = 0.85f;

        [Header("Training Configuration")]
        [SerializeField] private bool _autoTrain = true;
        [SerializeField] private float _battleDelay = 0.1f;
        [SerializeField] private float _turnDelay = 0.05f;
        [SerializeField] private int _maxTurnsPerBattle = 20;
        [SerializeField] private bool _useSmartAI = true;
        [SerializeField] [Range(0f, 1f)] private float _playerSkill = 0.5f;
        [SerializeField] private bool _resetOnRunComplete = true;

        // Simulated state
        private SimPlayer _player;
        private List<SimArea> _areas;
        private SimEnemy _currentEnemy;
        private int _currentAreaIndex;
        private int _enemyIndex; // Current enemy in area
        private int _turnCount;
        private bool _battleInProgress;
        private bool _runInProgress;

        // Training stats
        private int _episodeCount;
        private int _runCount;
        private int _winCount;
        private int _lossCount;
        private int _totalTurns;
        private float _totalReward;
        private float _lastReward;
        private int _consecutiveWins;
        private int _consecutiveLosses;

        // Events for UI
        public event Action<int, int, int> OnBattleStateChanged; // (playerHP, enemyHP, turn)
        public event Action<bool, float, int> OnBattleEnded; // (won, reward, episode)
        public event Action<int> OnDifficultyChanged; // (level)
        public event Action<int, int> OnAreaChanged; // (areaIndex, totalAreas)
        public event Action<TrainingStats> OnStatsUpdated;
        public event Action<RunResult> OnRunComplete;

        public static TrainingBattleSimulator Instance { get; private set; }

        // Public properties
        public int EpisodeCount => _episodeCount;
        public int WinCount => _winCount;
        public float WinRate => _episodeCount > 0 ? (float)_winCount / _episodeCount : 0f;
        public float AvgReward => _episodeCount > 0 ? _totalReward / _episodeCount : 0f;
        public float LastReward => _lastReward;
        public int CurrentDifficulty => _difficultySettings?.CurrentLevelIndex ?? 2;
        public string DifficultyName => _difficultySettings?.GetLevelName() ?? "Normal";
        public int CurrentArea => _currentAreaIndex + 1;
        public int TotalAreas => _areas?.Count ?? _areasPerRun;
        public string CurrentEnemyName => _currentEnemy?.Name ?? "None";
        public SimPlayer Player => _player;

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
            // Load references
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }

            if (_ddaAgent == null)
            {
                _ddaAgent = FindObjectOfType<DDAAgent>();
            }

            if (_playerData == null)
            {
                _playerData = Resources.Load<PlayerSO>("Player/CurrentPlayerData");
            }

            // Initialize areas
            InitializeAreas();

            // Initialize player
            _player = new SimPlayer();

            // Start training
            if (_autoTrain)
            {
                StartCoroutine(TrainingLoop());
            }
        }

        private void InitializeAreas()
        {
            _areas = new List<SimArea>();

            if (_loadFromMapData && _mapData != null)
            {
                // Load from MapData ScriptableObject
                foreach (var node in _mapData.mapItems)
                {
                    if (node != null)
                    {
                        _areas.Add(new SimArea(node));
                    }
                }
                Debug.Log($"[TrainingSim] Loaded {_areas.Count} areas from MapData");
            }
            else
            {
                // Generate default 12-area sequence
                GenerateDefaultAreas();
            }
        }

        private void GenerateDefaultAreas()
        {
            // Default sequence based on design spec
            // 12 areas: Enemy x5, Rest x2, Shop x2, Boss x1
            _areas.Add(CreateEnemyArea("Caveman", EnemyType.caveman_basic, 15, 6, 2));
            _areas.Add(CreateEnemyArea("Sabertooth+Caveman", EnemyType.sabertooth_basic, 18, 8, 3, addSecondEnemy: true));
            _areas.Add(CreateRestArea());
            _areas.Add(CreateEnemyArea("Sabertooth+Caveman", EnemyType.sabertooth_basic, 18, 8, 3, addSecondEnemy: true));
            _areas.Add(CreateEnemyArea("Raptor x2", EnemyType.raptor_basic, 24, 9, 5, addSecondEnemy: true));
            _areas.Add(CreateEnemyArea("Raptor x2", EnemyType.raptor_basic, 24, 9, 5, addSecondEnemy: true));
            _areas.Add(CreateShopArea());
            _areas.Add(CreateEnemyArea("Raptor+Sabertooth+Caveman", EnemyType.raptor_basic, 24, 9, 5, addSecondEnemy: true, addThirdEnemy: true));
            _areas.Add(CreateEnemyArea("Raptor+Sabertooth+Caveman", EnemyType.raptor_basic, 24, 9, 5, addSecondEnemy: true, addThirdEnemy: true));
            _areas.Add(CreateRestArea());
            _areas.Add(CreateShopArea());
            _areas.Add(CreateBossArea("Trex", 35, 20, 5));

            Debug.Log($"[TrainingSim] Generated {_areas.Count} default areas");
        }

        private SimArea CreateEnemyArea(string name, EnemyType type, int hp, int baseDmg, int interval,
            bool addSecondEnemy = false, bool addThirdEnemy = false)
        {
            var area = new SimArea
            {
                AreaType = MapType.Enemy,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>
                {
                    new SimDropItem(ConsumableType.Coin, 30),
                    new SimDropItem(ConsumableType.Exp, 15)
                }
            };

            area.Enemies.Add(new SimEnemy(name, type, hp, baseDmg, interval));

            if (addSecondEnemy)
            {
                area.Enemies.Add(new SimEnemy("Sabertooth", EnemyType.sabertooth_basic, 18, 8, 3));
            }

            if (addThirdEnemy)
            {
                area.Enemies.Add(new SimEnemy("Caveman", EnemyType.caveman_basic, 15, 6, 2));
            }

            return area;
        }

        private SimArea CreateRestArea()
        {
            return new SimArea
            {
                AreaType = MapType.Rest,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>()
            };
        }

        private SimArea CreateShopArea()
        {
            return new SimArea
            {
                AreaType = MapType.Shop,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>()
            };
        }

        private SimArea CreateBossArea(string name, int hp, int baseDmg, int interval)
        {
            var area = new SimArea
            {
                AreaType = MapType.Boss,
                IsBossArea = true,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>
                {
                    new SimDropItem(ConsumableType.Coin, 50),
                    new SimDropItem(ConsumableType.Exp, 20)
                }
            };

            area.Enemies.Add(new SimEnemy(name, EnemyType.trex_basic, hp, baseDmg, interval));

            return area;
        }

        private IEnumerator TrainingLoop()
        {
            while (true)
            {
                if (!_runInProgress)
                {
                    yield return StartCoroutine(RunTrainingRun());
                }
                yield return new WaitForSeconds(_battleDelay);
            }
        }

        private IEnumerator RunTrainingRun()
        {
            _runInProgress = true;
            _currentAreaIndex = 0;
            _runCount++;

            // Reset player for new run
            _player.Reset();

            Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

            // Process each area
            while (_currentAreaIndex < _areas.Count && _player.IsAlive())
            {
                yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));

                if (!_player.IsAlive())
                {
                    break; // Player died, end run
                }

                _currentAreaIndex++;
                OnAreaChanged?.Invoke(_currentAreaIndex, _areas.Count);
            }

            // Run complete
            bool runWon = _player.IsAlive() && _currentAreaIndex >= _areas.Count;

            var runResult = new RunResult
            {
                RunNumber = _runCount,
                AreasCompleted = _currentAreaIndex,
                TotalAreas = _areas.Count,
                Won = runWon,
                FinalHP = _player.CurrentHP,
                Coin = _player.Coin,
                Level = _player.Level
            };

            OnRunComplete?.Invoke(runResult);

            Debug.Log($"[TrainingSim] Run {_runCount} complete. Won: {runWon}, " +
                      $"Areas: {_currentAreaIndex}/{_areas.Count}, HP: {_player.CurrentHP}");

            if (_resetOnRunComplete)
            {
                _player.Reset();
            }

            _runInProgress = false;
        }

        private IEnumerator ProcessArea(SimArea area)
        {
            // Apply difficulty to enemies
            float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
            float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
            area.ApplyDifficulty(hpMult, dmgMult);

            switch (area.AreaType)
            {
                case MapType.Enemy:
                case MapType.Boss:
                    yield return StartCoroutine(ProcessBattleArea(area));
                    break;

                case MapType.Rest:
                    ProcessRestArea(area);
                    break;

                case MapType.Shop:
                    ProcessShopArea(area);
                    break;
            }
        }

        private IEnumerator ProcessBattleArea(SimArea area)
        {
            // Fight all enemies in sequence
            foreach (var enemy in area.Enemies)
            {
                if (!_player.IsAlive())
                {
                    yield break;
                }

                _currentEnemy = enemy;
                yield return StartCoroutine(RunBattleEpisode());

                if (!_player.IsAlive())
                {
                    yield break; // Lost, don't process drops
                }
            }

            // Apply drops after winning
            area.ApplyDrops(_player);

            // Reset action uses for next area
            _player.ResetActionUses();
        }

        private void ProcessRestArea(SimArea area)
        {
            int healAmount = UnityEngine.Random.Range(10, 25);
            _player.Heal(healAmount);

            Debug.Log($"[TrainingSim] Rest area: Healed {healAmount} HP. " +
                      $"HP: {_player.CurrentHP}/{_player.MaxHP}");
        }

        private void ProcessShopArea(SimArea area)
        {
            // Smart AI shopping
            area.ApplyShop(_player, _useSmartAI);

            Debug.Log($"[TrainingSim] Shop area: Coin={_player.Coin}, " +
                      $"Shield={_player.CurrentShield}/{_player.MaxShield}");
        }

        private IEnumerator RunBattleEpisode()
        {
            _battleInProgress = true;
            _episodeCount++;
            _turnCount = 0;

            // Notify DDA agent battle starting
            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            // Difficulty changed callback
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            Debug.Log($"[TrainingSim] Episode {_episodeCount} started. " +
                      $"Enemy: {_currentEnemy.Name} (HP: {_currentEnemy.MaxHP}), " +
                      $"Difficulty: {DifficultyName}");

            // Run battle turns
            while (_player.IsAlive() && _currentEnemy.IsAlive() && _turnCount < _maxTurnsPerBattle)
            {
                yield return StartCoroutine(RunTurn());
                yield return new WaitForSeconds(_turnDelay);
            }

            // Determine outcome
            bool playerWon = _currentEnemy.CurrentHP <= 0 && _player.IsAlive();
            int playerEndHP = _player.CurrentHP;

            // Calculate reward
            _lastReward = CalculateReward(playerWon, playerEndHP, _player.MaxHP, _turnCount);

            // Update stats
            if (playerWon)
            {
                _winCount++;
                _consecutiveWins++;
                _consecutiveLosses = 0;
            }
            else
            {
                _lossCount++;
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
                      $"Won: {playerWon}, HP: {playerEndHP}/{_player.MaxHP}, " +
                      $"Turns: {_turnCount}, Reward: {_lastReward:F3}, " +
                      $"WinRate: {WinRate:P1}");

            _battleInProgress = false;
        }

        private IEnumerator RunTurn()
        {
            _turnCount++;

            // --- Player Turn ---
            int playerDamage = ExecutePlayerTurn();
            if (playerDamage > 0)
            {
                _currentEnemy.TakeDamage(playerDamage);
            }

            OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

            if (!_currentEnemy.IsAlive())
            {
                yield break; // Enemy defeated
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            int enemyDamage = ExecuteEnemyTurn();
            if (enemyDamage > 0)
            {
                _player.TakeDamage(enemyDamage);
            }

            OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

            // Notify DDA agent turn ended
            _ddaAgent?.OnTurnEnd(playerDamage);
        }

        private int ExecutePlayerTurn()
        {
            // Build battle state for AI
            var state = new BattleState
            {
                PlayerHP = _player.CurrentHP,
                PlayerMaxHP = _player.MaxHP,
                PlayerShield = _player.CurrentShield,
                EnemyHP = _currentEnemy.CurrentHP,
                EnemyMaxHP = _currentEnemy.MaxHP,
                SwordUsesRemaining = _player.SwordUses,
                GunUsesRemaining = _player.GunUses,
                DefendUsesRemaining = _player.DefendUses,
                TurnCount = _turnCount
            };

            // Choose action
            SimAction action;
            if (_useSmartAI)
            {
                action = SmartBattleAI.ChooseAction(state);
            }
            else
            {
                // Random action
                action = GetRandomAction(state);
            }

            // Execute action
            int damage = 0;
            switch (action)
            {
                case SimAction.Punch:
                    damage = SmartBattleAI.CalculateDamage(SimAction.Punch, _player);
                    break;

                case SimAction.Sword:
                    if (_player.SwordUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Sword, _player);
                        _player.SwordUses--;
                    }
                    break;

                case SimAction.Gun:
                    if (_player.GunUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Gun, _player);
                        _player.GunUses--;
                    }
                    break;

                case SimAction.Defend:
                    if (_player.DefendUses > 0)
                    {
                        int shieldHP = SmartBattleAI.CalculateDefend(_player);
                        _player.CurrentShield = Mathf.Min(_player.MaxShield, _player.CurrentShield + shieldHP);
                        _player.DefendUses--;
                    }
                    break;
            }

            return damage;
        }

        private SimAction GetRandomAction(BattleState state)
        {
            // Weighted random with skill influence
            float skill = _playerSkill;
            float punchWeight = 0.35f - skill * 0.15f;  // Less punch at high skill
            float swordWeight = state.SwordUsesRemaining > 0 ? 0.25f + skill * 0.1f : 0f;
            float gunWeight = state.GunUsesRemaining > 0 ? 0.25f + skill * 0.1f : 0f;
            float defendWeight = state.DefendUsesRemaining > 0 ? 0.15f : 0f;

            // Normalize
            float total = punchWeight + swordWeight + gunWeight + defendWeight;
            if (total > 0)
            {
                punchWeight /= total;
                swordWeight /= total;
                gunWeight /= total;
                defendWeight /= total;
            }

            float roll = UnityEngine.Random.value;
            if (roll < punchWeight)
            {
                return SimAction.Punch;
            }
            else if (roll < punchWeight + swordWeight)
            {
                return SimAction.Sword;
            }
            else if (roll < punchWeight + swordWeight + gunWeight)
            {
                return SimAction.Gun;
            }
            else
            {
                return SimAction.Defend;
            }
        }

        private int ExecuteEnemyTurn()
        {
            // Accuracy check (80% for enemy)
            if (UnityEngine.Random.value > 0.80f)
            {
                return 0; // Miss
            }

            // Calculate damage with variance
            int damage = _currentEnemy.CalculateDamage();
            return Mathf.Max(1, damage);
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
            _lossCount = 0;
            _runCount = 0;
            _totalTurns = 0;
            _totalReward = 0;
            _lastReward = 0;
            _consecutiveWins = 0;
            _consecutiveLosses = 0;
            _currentAreaIndex = 0;
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

        /// <summary>
        /// Sets use smart AI flag.
        /// </summary>
        public void SetUseSmartAI(bool useSmart)
        {
            _useSmartAI = useSmart;
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

    /// <summary>
    /// Result of a training run (12 areas).
    /// </summary>
    [Serializable]
    public struct RunResult
    {
        public int RunNumber;
        public int AreasCompleted;
        public int TotalAreas;
        public bool Won;
        public int FinalHP;
        public int Coin;
        public int Level;
    }
}