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

        [Header("Fast Training Mode")]
        [Tooltip("Simulate battles instantly without yields - much faster training")]
        [SerializeField] private bool _instantMode = true;

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
        private int _battleCount;     // Counts individual battles
        private int _runCount;        // Counts full runs (episodes)
        private int _episodeCount;    // Counts episodes (1 episode = 1 area)
        private int _winCount;
        private int _lossCount;
        private int _totalTurns;
        private float _totalReward;
        private float _lastReward;
        private int _consecutiveWins;
        private int _consecutiveLosses;

        // Track HP at area start for area-level reward calculation
        private int _areaStartHP;

        // Events for UI
        public event Action<int, int, int> OnBattleStateChanged; // (playerHP, enemyHP, turn)
        public event Action<bool, float, int> OnBattleEnded; // (won, reward, episode)
        public event Action<int> OnDifficultyChanged; // (level)
        public event Action<int, int> OnAreaChanged; // (areaIndex, totalAreas)
        public event Action<TrainingStats> OnStatsUpdated;
        public event Action<RunResult> OnRunComplete;
        public event Action<bool> OnTurnChanged; // (isPlayerTurn) - true = player turn, false = enemy turn

        // Turn state for UI
        private bool _isPlayerTurn = true;
        public bool IsPlayerTurn => _isPlayerTurn;

        public static TrainingBattleSimulator Instance { get; private set; }

        // Public properties
        public int BattleCount => _battleCount;        // Total battles fought
        public int EpisodeCount => _episodeCount;      // Episodes = runs (1 episode = 1 full run)
        public int WinCount => _winCount;
        public float WinRate => _battleCount > 0 ? (float)_winCount / _battleCount : 0f;
        public float AvgReward => _battleCount > 0 ? _totalReward / _battleCount : 0f;
        public float LastReward => _lastReward;
        public int CurrentDifficulty => _difficultySettings?.CurrentLevelIndex ?? 2;
        public string DifficultyName => _difficultySettings?.GetLevelName() ?? "Normal";
        public int CurrentArea => _currentAreaIndex + 1;
        public int TotalAreas => _areas?.Count ?? _areasPerRun;
        public string CurrentEnemyName => _currentEnemy?.Name ?? "None";
        public SimEnemy CurrentEnemy => _currentEnemy;
        public SimPlayer Player => _player;

        // Current attacking enemy for UI (updated during multi-enemy battles)
        private SimEnemy _attackingEnemy;
        public SimEnemy AttackingEnemy => _attackingEnemy;

        // Area info properties
        public MapType CurrentAreaType => _currentAreaIndex < _areas?.Count ? _areas[_currentAreaIndex].AreaType : MapType.Enemy;
        public int EnemiesInArea => _currentAreaIndex < _areas?.Count ? _areas[_currentAreaIndex].Enemies.Count : 0;
        public int EnemiesDefeatedInArea => _currentAreaIndex < _areas?.Count
            ? _areas[_currentAreaIndex].Enemies.FindAll(e => !e.IsAlive()).Count
            : 0;
        public string CurrentAreaEnemyList => GetEnemyListString();

        private string GetEnemyListString()
        {
            if (_currentAreaIndex >= _areas?.Count || _areas[_currentAreaIndex].Enemies.Count == 0)
                return "None";

            var enemies = _areas[_currentAreaIndex].Enemies;
            var enemyCounts = new System.Text.StringBuilder();
            var counts = new System.Collections.Generic.Dictionary<string, int>();

            foreach (var enemy in enemies)
            {
                if (counts.ContainsKey(enemy.Name))
                    counts[enemy.Name]++;
                else
                    counts[enemy.Name] = 1;
            }

            bool first = true;
            foreach (var kvp in counts)
            {
                if (!first) enemyCounts.Append(", ");
                enemyCounts.Append($"{kvp.Key}{(kvp.Value > 1 ? $" x{kvp.Value}" : "")}");
                first = false;
            }

            return enemyCounts.ToString();
        }

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
                // Trace a random path through the branching map graph
                // (not all 22 nodes — only ~12 per run)
                _areas = BuildRandomPath();
                Debug.Log($"[TrainingSim] Built path with {_areas.Count} areas from MapData (22 nodes total, branching)");
            }
            else
            {
                // Generate default 12-area sequence
                GenerateDefaultAreas();
            }
        }

        /// <summary>
        /// Build a random path through the branching map graph.
        /// At each branch point, randomly pick one connection.
        /// Returns a linear list of areas (start to finish).
        /// </summary>
        private List<SimArea> BuildRandomPath()
        {
            var path = new List<SimArea>();
            var nodeMap = new System.Collections.Generic.Dictionary<string, MapNode>();

            // Index nodes by ID for fast lookup
            foreach (var node in _mapData.mapItems)
            {
                if (node != null && !string.IsNullOrEmpty(node.mapNodeId))
                {
                    nodeMap[node.mapNodeId] = node;
                }
            }

            // Start from first node, follow connections to end
            MapNode current = _mapData.mapItems[0];
            int safetyLimit = 50; // Prevent infinite loops

            while (current != null && safetyLimit-- > 0)
            {
                path.Add(new SimArea(current));

                // No connections = end of path (Boss2)
                if (current.connectionId == null || current.connectionId.Length == 0)
                {
                    break;
                }

                // Pick a random connection at branch points
                int nextIndex = UnityEngine.Random.Range(0, current.connectionId.Length);
                string nextId = current.connectionId[nextIndex];

                if (!nodeMap.TryGetValue(nextId, out MapNode nextNode))
                {
                    Debug.LogWarning($"[TrainingSim] Connection '{nextId}' not found in mapItems, ending path");
                    break;
                }

                current = nextNode;
            }

            return path;
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

            // Build a new random path through the branching map each run
            if (_loadFromMapData && _mapData != null)
            {
                _areas = BuildRandomPath();
            }

            // Notify agent that run is starting
            _ddaAgent?.OnRunStart();

            // Difficulty is reset to baseline inside DDAAgent.OnRunStart

            Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

            // Process each area
            while (_currentAreaIndex < _areas.Count && _player.IsAlive())
            {
                MapType areaType = _areas[_currentAreaIndex].AreaType;
                bool isBattleArea = areaType == MapType.Enemy || areaType == MapType.Boss;

                if (isBattleArea)
                {
                    // Yield to allow Academy to process the previous RequestDecision
                    // before applying difficulty for this area.
                    // This ensures the agent's difficulty choice is reflected in enemy stats.
                    if (_currentAreaIndex > 0)
                    {
                        yield return null;
                    }

                    // Apply difficulty that was set at end of PREVIOUS enemy area
                    // (or default difficulty for first enemy area)
                    float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
                    float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
                    _areas[_currentAreaIndex].ApplyDifficulty(hpMult, dmgMult);

                    // Notify agent we're entering this battle area
                    _ddaAgent?.OnAreaEnter(_currentAreaIndex, areaType, _areas.Count);

                    // Track HP at area start for reward calculation
                    _areaStartHP = _player.CurrentHP;
                }

                yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));

                // Update agent observations after any area (HP changes from rest/shop)
                float hpRatio = _player.MaxHP > 0 ? (float)_player.CurrentHP / _player.MaxHP : 1f;
                float swordDepletion = _player.MaxSwordUses > 0 ? 1f - (float)_player.SwordUses / _player.MaxSwordUses : 0f;
                float gunDepletion = _player.MaxGunUses > 0 ? 1f - (float)_player.GunUses / _player.MaxGunUses : 0f;
                float defendDepletion = _player.MaxDefendUses > 0 ? 1f - (float)_player.DefendUses / _player.MaxDefendUses : 0f;
                float resourceDepletion = (swordDepletion + gunDepletion + defendDepletion) / 3f;
                _ddaAgent.UpdateBattlePhase(hpRatio, resourceDepletion);
                _ddaAgent?.SetPlayerLevel(_player.Level);

                if (isBattleArea)
                {
                    // Notify agent that battle area is complete (reward + decision)
                    bool areaWon = _player.IsAlive();
                    _ddaAgent?.OnAreaComplete(areaWon);

                    // Calculate area-level reward for UI stats
                    _lastReward = DDAAgent.CalculateReward(areaWon, _player.CurrentHP, _areaStartHP);
                    _totalReward += _lastReward;
                }
                else
                {
                    // Rest/Shop areas: no reward, no decision
                    // State changes (healing, items) already updated above
                    _lastReward = 0f;
                }

                _episodeCount++;

                OnStatsUpdated?.Invoke(GetStats());

                if (!_player.IsAlive())
                {
                    break; // Player died, end run
                }

                _currentAreaIndex++;
                OnAreaChanged?.Invoke(_currentAreaIndex, _areas.Count);

                // Yield to allow Academy to process decision before next area
                yield return null;
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

            // Notify agent that run ended — EndEpisode called inside DDAAgent.OnRunEnd
            _ddaAgent?.OnRunEnd(runWon, _currentAreaIndex, _areas.Count);

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
            // Difficulty is already applied in RunTrainingRun before this is called
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
            // Reset turn count for entire area (not per enemy)
            _turnCount = 0;

            // Note: _areaStartHP is set in RunTrainingRun before this method is called

            // Set all enemies in area as active (like actual game)
            // In actual game, player fights ALL enemies in ONE battle
            // Each turn: player attacks ONE enemy, ONE random enemy attacks back

            if (_instantMode)
            {
                RunBattleInstantWithMultipleEnemies(area.Enemies);
            }
            else
            {
                yield return StartCoroutine(RunBattleEpisodeWithMultipleEnemies(area.Enemies));
            }

            if (!_player.IsAlive())
            {
                yield break; // Lost, don't process drops
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
            _battleCount++;
            // _turnCount reset at area level, not here

            // Notify DDA agent battle starting
            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            // Difficulty changed callback
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            Debug.Log($"[TrainingSim] Battle {_battleCount} started. " +
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

            // Notify DDA agent battle ended (updates observations only, reward at OnAreaComplete)
            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            // Unsubscribe
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            // Fire events for UI
            OnBattleEnded?.Invoke(playerWon, 0, _battleCount);
            OnStatsUpdated?.Invoke(GetStats());

            Debug.Log($"[TrainingSim] Battle {_battleCount} ended. " +
                      $"Won: {playerWon}, HP: {playerEndHP}/{_player.MaxHP}, " +
                      $"Turns: {_turnCount}, WinRate: {WinRate:P1}");

            _battleInProgress = false;
        }

        /// <summary>
        /// Instant battle simulation - no yields, much faster for training.
        /// </summary>
        private void RunBattleInstant()
        {
            _battleInProgress = true;
            _battleCount++;
            // _turnCount reset at area level, not here

            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            // Run battle turns instantly (no yields)
            while (_player.IsAlive() && _currentEnemy.IsAlive() && _turnCount < _maxTurnsPerBattle)
            {
                _turnCount++;

                // Player turn
                int playerDamage = ExecutePlayerTurn();
                if (playerDamage > 0)
                {
                    _currentEnemy.TakeDamage(playerDamage);
                }

                if (!_currentEnemy.IsAlive()) break;

                // Enemy turn
                int enemyDamage = ExecuteEnemyTurn();
                if (enemyDamage > 0)
                {
                    _player.TakeDamage(enemyDamage);
                }

                // Update UI sliders each turn
                OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

                _ddaAgent?.OnTurnEnd(playerDamage);
            }

            // Determine outcome
            bool playerWon = _currentEnemy.CurrentHP <= 0 && _player.IsAlive();
            int playerEndHP = _player.CurrentHP;

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

            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            OnBattleEnded?.Invoke(playerWon, 0, _battleCount);

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

        /// <summary>
        /// Instant battle with multiple enemies - matches actual game flow.
        /// Player fights all enemies in ONE battle, one enemy at a time.
        /// Each turn: player attacks ONE enemy, ONE random enemy attacks back.
        /// </summary>
        private void RunBattleInstantWithMultipleEnemies(List<SimEnemy> enemies)
        {
            _battleInProgress = true;
            _battleCount++;

            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            // Fight until all enemies dead or player dead
            int playerDamage = 0;
            while (_player.IsAlive() && HasAliveEnemies(enemies) && _turnCount < _maxTurnsPerBattle)
            {
                _turnCount++;

                // --- Player Turn ---
                _isPlayerTurn = true;
                OnTurnChanged?.Invoke(_isPlayerTurn);

                // Player selects a target (smart AI picks lowest HP enemy)
                SimEnemy targetEnemy = GetPlayerTarget(enemies);
                if (targetEnemy != null && targetEnemy.IsAlive())
                {
                    playerDamage = ExecutePlayerTurn(targetEnemy);
                    if (playerDamage > 0)
                    {
                        targetEnemy.TakeDamage(playerDamage);
                    }
                }

                // Check if all enemies dead
                if (!HasAliveEnemies(enemies)) break;

                // --- Enemy Turn ---
                _isPlayerTurn = false;
                OnTurnChanged?.Invoke(_isPlayerTurn);

                // ONE random alive enemy attacks (matches actual game)
                SimEnemy attackingEnemy = GetRandomAliveEnemy(enemies);
                _attackingEnemy = attackingEnemy; // Track for UI
                if (attackingEnemy != null)
                {
                    int enemyDamage = ExecuteEnemyTurnForEnemy(attackingEnemy);
                    if (enemyDamage > 0)
                    {
                        _player.TakeDamage(enemyDamage);
                    }
                }

                // Update UI with total enemy HP
                int totalEnemyHP = GetTotalEnemyHP(enemies);
                OnBattleStateChanged?.Invoke(_player.CurrentHP, totalEnemyHP, _turnCount);

                // Update battle phase features for DDA agent
                UpdateAgentBattlePhase(enemies);

                _ddaAgent?.OnTurnEnd(playerDamage > 0 ? playerDamage : 0);
            }

            // Determine outcome
            bool playerWon = !HasAliveEnemies(enemies) && _player.IsAlive();
            int playerEndHP = _player.CurrentHP;

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

            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            OnBattleEnded?.Invoke(playerWon, 0, _battleCount);

            _battleInProgress = false;
        }

        /// <summary>
        /// Coroutine version for non-instant mode.
        /// </summary>
        private IEnumerator RunBattleEpisodeWithMultipleEnemies(List<SimEnemy> enemies)
        {
            _battleInProgress = true;
            _battleCount++;

            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            Debug.Log($"[TrainingSim] Battle {_battleCount} started. " +
                      $"Enemies: {enemies.Count}, Difficulty: {DifficultyName}");

            while (_player.IsAlive() && HasAliveEnemies(enemies) && _turnCount < _maxTurnsPerBattle)
            {
                yield return StartCoroutine(RunTurnWithMultipleEnemies(enemies));
                yield return new WaitForSeconds(_turnDelay);
            }

            bool playerWon = !HasAliveEnemies(enemies) && _player.IsAlive();
            int playerEndHP = _player.CurrentHP;

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

            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            OnBattleEnded?.Invoke(playerWon, 0, _battleCount);
            OnStatsUpdated?.Invoke(GetStats());

            _battleInProgress = false;
        }

        private IEnumerator RunTurnWithMultipleEnemies(List<SimEnemy> enemies)
        {
            _turnCount++;

            // --- Player Turn ---
            _isPlayerTurn = true;
            OnTurnChanged?.Invoke(_isPlayerTurn);

            SimEnemy targetEnemy = GetPlayerTarget(enemies);
            if (targetEnemy != null && targetEnemy.IsAlive())
            {
                int playerDamage = ExecutePlayerTurn(targetEnemy);
                if (playerDamage > 0)
                {
                    targetEnemy.TakeDamage(playerDamage);
                }
            }

            int totalEnemyHP = GetTotalEnemyHP(enemies);
            OnBattleStateChanged?.Invoke(_player.CurrentHP, totalEnemyHP, _turnCount);

            if (!HasAliveEnemies(enemies))
            {
                yield break;
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            _isPlayerTurn = false;
            OnTurnChanged?.Invoke(_isPlayerTurn);

            // ONE random alive enemy attacks
            SimEnemy attackingEnemy = GetRandomAliveEnemy(enemies);
            _attackingEnemy = attackingEnemy; // Track for UI
            if (attackingEnemy != null)
            {
                int enemyDamage = ExecuteEnemyTurnForEnemy(attackingEnemy);
                if (enemyDamage > 0)
                {
                    _player.TakeDamage(enemyDamage);
                }
            }

            totalEnemyHP = GetTotalEnemyHP(enemies);
            OnBattleStateChanged?.Invoke(_player.CurrentHP, totalEnemyHP, _turnCount);

            // Update battle phase observations for DDA agent
            UpdateAgentBattlePhase(enemies);

            _ddaAgent?.OnTurnEnd(0);
        }

        // Helper methods for multi-enemy battles
        private bool HasAliveEnemies(List<SimEnemy> enemies)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive()) return true;
            }
            return false;
        }

        private SimEnemy GetRandomAliveEnemy(List<SimEnemy> enemies)
        {
            var aliveEnemies = new List<SimEnemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive()) aliveEnemies.Add(enemy);
            }

            if (aliveEnemies.Count == 0) return null;

            int randomIndex = UnityEngine.Random.Range(0, aliveEnemies.Count);
            return aliveEnemies[randomIndex];
        }

        private SimEnemy GetPlayerTarget(List<SimEnemy> enemies)
        {
            // Smart AI: target lowest HP enemy to maximize kill efficiency
            // Or random if using simple AI
            var aliveEnemies = new List<SimEnemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive()) aliveEnemies.Add(enemy);
            }

            if (aliveEnemies.Count == 0) return null;

            if (_useSmartAI)
            {
                // Target lowest HP enemy
                SimEnemy lowestHP = aliveEnemies[0];
                foreach (var enemy in aliveEnemies)
                {
                    if (enemy.CurrentHP < lowestHP.CurrentHP)
                    {
                        lowestHP = enemy;
                    }
                }
                return lowestHP;
            }
            else
            {
                // Random target
                int randomIndex = UnityEngine.Random.Range(0, aliveEnemies.Count);
                return aliveEnemies[randomIndex];
            }
        }

        private int GetTotalEnemyHP(List<SimEnemy> enemies)
        {
            int total = 0;
            foreach (var enemy in enemies)
            {
                total += enemy.CurrentHP;
            }
            return total;
        }

        private int ExecuteEnemyTurnForEnemy(SimEnemy enemy)
        {
            // No accuracy check - matches actual game (100% hit rate)
            int damage = enemy.CalculateDamage();
            return Mathf.Max(1, damage);
        }

        private int ExecutePlayerTurn()
        {
            return ExecutePlayerTurn(_currentEnemy);
        }

        private int ExecutePlayerTurn(SimEnemy targetEnemy)
        {
            // Build battle state for AI
            var state = new BattleState
            {
                PlayerHP = _player.CurrentHP,
                PlayerMaxHP = _player.MaxHP,
                PlayerShield = _player.CurrentShield,
                EnemyHP = targetEnemy.CurrentHP,
                EnemyMaxHP = targetEnemy.MaxHP,
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

            // Execute action with skill-based damage roll
            int damage = 0;
            switch (action)
            {
                case SimAction.Punch:
                    damage = SmartBattleAI.CalculateDamage(SimAction.Punch, _player, _playerSkill);
                    break;

                case SimAction.Sword:
                    if (_player.SwordUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Sword, _player, _playerSkill);
                        _player.SwordUses--;
                    }
                    break;

                case SimAction.Gun:
                    if (_player.GunUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Gun, _player, _playerSkill);
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
            // No accuracy check - matches actual game (100% hit rate)
            // Calculate damage with variance
            int damage = _currentEnemy.CalculateDamage();
            return Mathf.Max(1, damage);
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
                BattleCount = _battleCount,
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
                AvgTurnsPerBattle = _battleCount > 0 ? (float)_totalTurns / _battleCount : 0f,
            };
        }

        /// <summary>
        /// Resets all training statistics.
        /// </summary>
        public void ResetStats()
        {
            _battleCount = 0;
            _winCount = 0;
            _lossCount = 0;
            _runCount = 0;
            _episodeCount = 0;
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

        /// <summary>
        /// Updates DDA agent with battle phase observations.
        /// </summary>
        private void UpdateAgentBattlePhase(List<SimEnemy> enemies)
        {
            if (_ddaAgent == null) return;

            // HP ratio
            float hpRatio = _player.MaxHP > 0 ? (float)_player.CurrentHP / _player.MaxHP : 1f;

            // Resource depletion (weighted average of used actions)
            float swordDepletion = 1f - (float)_player.SwordUses / _player.MaxSwordUses;
            float gunDepletion = 1f - (float)_player.GunUses / _player.MaxGunUses;
            float defendDepletion = 1f - (float)_player.DefendUses / _player.MaxDefendUses;
            float resourceDepletion = (swordDepletion + gunDepletion + defendDepletion) / 3f;

            _ddaAgent.UpdateBattlePhase(hpRatio, resourceDepletion);
        }

        /// <summary>
        /// Gets total enemy HP ratio (remaining vs max).
        /// </summary>
        private float GetEnemyHPRatio(List<SimEnemy> enemies)
        {
            if (enemies == null || enemies.Count == 0) return 0f;

            int totalCurrent = 0;
            int totalMax = 0;

            foreach (var enemy in enemies)
            {
                totalCurrent += enemy.CurrentHP;
                totalMax += enemy.MaxHP;
            }

            return totalMax > 0 ? (float)totalCurrent / totalMax : 0f;
        }
    }

    /// <summary>
    /// Training statistics data structure for UI display.
    /// </summary>
    [Serializable]
    public struct TrainingStats
    {
        public int BattleCount;          // Individual battles fought
        public int EpisodeCount;         // Areas completed (1 episode = 1 area)
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