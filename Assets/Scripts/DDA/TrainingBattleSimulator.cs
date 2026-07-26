using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Player;
using DDA;

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
        [SerializeField] private int _maxTurnsPerBattle = 40;
        [SerializeField] private bool _useSmartAI = true;
        [SerializeField] [Range(0f, 1f)] private float _playerSkill = 0.7f;
        [SerializeField] private bool _resetOnRunComplete = true;
        [SerializeField] private int _randomSeed;

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

        // Track HP at battle start for battle-level logging
        private int _battleStartHP;

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

        // Environment ID for multi-env logging
        private int _envId = 0;

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

            UnityEngine.Random.InitState(_randomSeed);
            _envId = System.Diagnostics.Process.GetCurrentProcess().Id * 1000 + UnityEngine.Random.Range(0, 999);
        }

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE
            // Optimize engine speed for ML-Agents training
            Application.targetFrameRate = 9999;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
#endif

            // Load references
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }

            // CRITICAL for multi-env: Create a runtime copy of DifficultySettings
            // so each environment has independent difficulty state.
            // Without this, all envs share the same difficulty level.
            // The widened 0.6x-1.4x range is authored on the DefaultDifficultySettings SO asset
            // (single source of truth) and cloned here per environment.
            if (_difficultySettings != null)
            {
                _difficultySettings = _difficultySettings.CreateRuntimeCopy();
            }

            // Find DDAAgent in same scene/area (for multi-env, each env has its own agent)
            // Prefer serialized reference, fallback to GetComponentInChildren (finds in own hierarchy)
            // NEVER use FindObjectOfType as it returns ANY agent in the scene (wrong for multi-env)
            if (_ddaAgent == null)
            {
                // Try to find in own hierarchy first (correct for multi-env)
                _ddaAgent = GetComponentInChildren<DDAAgent>(includeInactive: true);

                // Fallback to self if this GameObject has DDAAgent
                if (_ddaAgent == null)
                {
                    _ddaAgent = GetComponent<DDAAgent>();
                }

                // Only use FindObjectOfType as last resort (WARN: may find wrong agent in multi-env)
                if (_ddaAgent == null)
                {
                    _ddaAgent = FindObjectOfType<DDAAgent>();
                    Debug.LogWarning($"[TrainingSim] Using FindObjectOfType<DDAAgent> - may cause issues in multi-env! Agent ID: {_ddaAgent?.GetInstanceID()}");
                }
            }

            // Pass envId to agent for logging
            if (_ddaAgent != null)
            {
                _ddaAgent.SetEnvId(_envId);

                // CRITICAL for convergence: share the runtime difficulty instance with the agent.
                // Without this the agent mutates a separate DifficultySettings instance and its
                // difficulty-level actions never reach the battles this simulator runs
                // (root cause of non-convergence: Q-values collapsed to ~0 because no action
                // affected reward). Done after the runtime copy is created above.
                _ddaAgent.SetDifficultySettings(_difficultySettings);
            }

            if (_playerData == null)
            {
                _playerData = Resources.Load<PlayerSO>("Player/CurrentPlayerData");
            }

            // Initialize areas
            InitializeAreas();

            // Initialize player
            _player = new SimPlayer();

            // Enforce survivable skill for training. The serialized _playerSkill value bakes
            // into the build and may still hold the old 0.5; force a minimum of 0.7 so Normal
            // difficulty is survivable and the full widened range spans win (Very Easy) to loss
            // (Very Hard). Inspector values above 0.7 are preserved.
            _playerSkill = Mathf.Max(_playerSkill, 0.7f);

            // Initialize training logger (thread-safe, only first env creates file)
            TrainingLogger.Initialize(envId: _envId);

            // Start training
            if (_autoTrain)
            {
                StartCoroutine(TrainingLoop());
            }
        }

        private void OnDestroy()
        {
            TrainingLogger.Close();
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
                // Minimal delay between runs in instant mode
                if (_instantMode)
                {
                    yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(_battleDelay);
                }
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
                TrainingLogger.LogMapPath(_runCount, _areas, _envId);
            }

            // Notify agent that run is starting
            _ddaAgent?.OnRunStart();

            // Difficulty is reset to baseline inside DDAAgent.OnRunStart

            TrainingLogger.LogRunStart(_runCount, _areas.Count, _difficultySettings?.GetLevelName() ?? "Normal", _envId);
            Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

            // Process each area
            while (_currentAreaIndex < _areas.Count && _player.IsAlive())
            {
                MapType areaType = _areas[_currentAreaIndex].AreaType;
                bool isBattleArea = areaType == MapType.Enemy || areaType == MapType.Boss;

                if (isBattleArea)
                {
                    // Yield 1 frame so Academy can process the previous RequestDecision
                    // before we read _difficultySettings for this area.
                    // First battle has no prior RequestDecision, so skip the yield.
                    if (_currentAreaIndex > 0)
                    {
                        yield return null;
                    }

                    // Apply difficulty that was set at end of PREVIOUS enemy area
                    // (or default difficulty for first enemy area)
                    float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
                    float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
                    _areas[_currentAreaIndex].ApplyDifficulty(hpMult, dmgMult);

                    TrainingLogger.LogAreaEnter(_currentAreaIndex, areaType, hpMult, dmgMult,
                        _difficultySettings?.GetLevelName() ?? "Normal", _envId);

                    // Notify agent we're entering this battle area
                    _ddaAgent?.OnAreaEnter(_currentAreaIndex, areaType, _areas.Count);

                    // Track HP at area start for reward calculation
                    _areaStartHP = _player.CurrentHP;
                }

                if (_instantMode)
                {
                    ProcessAreaInstant(_areas[_currentAreaIndex]);
                }
                else
                {
                    yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));
                }

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

                    // OnAreaComplete calls RequestDecision() internally.
                    // The next battle area will yield 1 frame for Academy to process it.
                    // No yield needed here — the yield at the top of the next battle area
                    // ensures Academy processes the decision before we read difficulty settings.

                    // Calculate area-level reward for UI stats
                    _lastReward = DDAAgent.CalculateReward(areaWon, _player.CurrentHP, _areaStartHP);
                    _totalReward += _lastReward;

                    // Calculate progress weight for logging (matches DDAAgent)
                    float progressWeight = 0.5f + 0.5f * ((float)(_currentAreaIndex + 1) / _areasPerRun);

                    TrainingLogger.LogAreaComplete(_currentAreaIndex, areaWon,
                        _player.CurrentHP, _areaStartHP, _lastReward,
                        _ddaAgent != null ? _ddaAgent.GetCumulativeRewardValue : 0f,
                        progressWeight, _envId);
                    TrainingLogger.LogPlayerState(_player.CurrentHP, _player.MaxHP,
                        _player.Level, _player.Coin,
                        _player.SwordUses, _player.GunUses, _player.DefendUses, _envId);
                }
                else
                {
                    // Rest/Shop areas: no reward, no decision
                    // State changes (healing, items) already updated above
                    _lastReward = 0f;
                    TrainingLogger.LogMessage($"Area {_currentAreaIndex} ({areaType}) - No reward, no decision", _envId);
                }

                _episodeCount++;

                OnStatsUpdated?.Invoke(GetStats());

                if (!_player.IsAlive())
                {
                    // Player died — terminal step
                    TrainingLogger.LogMessage($"Player died at area {_currentAreaIndex}! Ending episode.", _envId);
                    _ddaAgent?.OnPlayerDeath(_currentAreaIndex, _areas.Count);
                    break; // End run
                }

                _currentAreaIndex++;
                OnAreaChanged?.Invoke(_currentAreaIndex, _areas.Count);

                // No yield here in instant mode — the yield before the next battle area
                // is the only frame needed for Academy to process RequestDecision.
                // Non-instant mode yields for visual pacing.
                if (!_instantMode)
                {
                    yield return null;
                }
            }

            // Run complete
            bool runWon = _player.IsAlive() && _currentAreaIndex >= _areas.Count;

            TrainingLogger.LogRunEnd(_runCount, runWon, _currentAreaIndex, _areas.Count,
                _ddaAgent != null ? _ddaAgent.GetCumulativeRewardValue : 0f,
                runWon ? 0.5f : -0.1f, _envId);

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

            // Only call OnRunEnd if player survived (completed all areas)
            // If player died, OnPlayerDeath was already called with EndEpisode
            if (runWon)
            {
                _ddaAgent?.OnRunEnd(true, _currentAreaIndex, _areas.Count);
            }

            Debug.Log($"[TrainingSim] Run {_runCount} complete. Won: {runWon}, " +
                      $"Areas: {_currentAreaIndex}/{_areas.Count}, HP: {_player.CurrentHP}");

            if (_resetOnRunComplete)
            {
                _player.Reset();
            }

            _runInProgress = false;
        }

        /// <summary>
        /// Instant area processing — no yields, runs synchronously.
        /// Battles use RunBattleInstantWithMultipleEnemies, rest/shop run inline.
        /// </summary>
        private void ProcessAreaInstant(SimArea area)
        {
            switch (area.AreaType)
            {
                case MapType.Enemy:
                case MapType.Boss:
                    RunBattleInstantWithMultipleEnemies(area.Enemies);
                    if (_player.IsAlive())
                    {
                        area.ApplyDrops(_player);
                        _player.ResetActionUses();
                    }
                    break;

                case MapType.Rest:
                    ProcessRestArea(area);
                    break;

                case MapType.Shop:
                    ProcessShopArea(area);
                    break;
            }
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

            TrainingLogger.LogRestArea(healAmount, _player.CurrentHP, _player.MaxHP, _envId);
            Debug.Log($"[TrainingSim] Rest area: Healed {healAmount} HP. " +
                      $"HP: {_player.CurrentHP}/{_player.MaxHP}");
        }

        private void ProcessShopArea(SimArea area)
        {
            // Smart AI shopping
            area.ApplyShop(_player, _useSmartAI);

            TrainingLogger.LogShopArea(_player.Coin, _player.DefendUses, _player.MaxDefendUses, _envId);
            Debug.Log($"[TrainingSim] Shop area: Coin={_player.Coin}, " +
                      $"Shield={_player.DefendUses}/{_player.MaxDefendUses}");
        }

        private IEnumerator RunBattleEpisode()
        {
            _battleInProgress = true;
            _battleCount++;
            // _turnCount reset at area level, not here

            // Notify DDA agent battle starting (single enemy)
            _ddaAgent?.OnBattleStart(_player.CurrentHP, GetTotalEnemyMaxHP(_currentEnemy));

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

            _ddaAgent?.OnBattleStart(_player.CurrentHP, GetTotalEnemyMaxHP(_currentEnemy));

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

                _ddaAgent?.OnTurnEnd(0, 0);

                if (!_currentEnemy.IsAlive()) break;

                // Enemy turn
                _turnCount++;
                int enemyDamage = ExecuteEnemyTurn();
                if (enemyDamage > 0)
                {
                    _player.TakeDamage(enemyDamage);
                }

                // Update UI sliders each turn
                OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

                _ddaAgent?.OnTurnEnd(0, enemyDamage);
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
            _ddaAgent?.OnTurnEnd(0, 0);

            if (!_currentEnemy.IsAlive())
            {
                yield break; // Enemy defeated
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            _turnCount++;
            int enemyDamage = ExecuteEnemyTurn();
            if (enemyDamage > 0)
            {
                _player.TakeDamage(enemyDamage);
            }

            OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

            // Notify DDA agent turn ended
            _ddaAgent?.OnTurnEnd(0, enemyDamage);
        }

        /// <summary>
        /// Instant battle with multiple enemies - matches actual game flow.
        /// Player fights all enemies in ONE battle, one enemy at a time.
        /// Each turn: player attacks ONE enemy, ONE random enemy attacks back.
        /// </summary>
        private void RunBattleInstantWithMultipleEnemies(List<SimEnemy> enemies)
        {
            // Reset turn count for this battle
            _turnCount = 0;

            _battleInProgress = true;
            _battleCount++;

            // Track HP at battle start for logging
            _battleStartHP = _player.CurrentHP;

            _ddaAgent?.OnBattleStart(_player.CurrentHP, GetTotalEnemyMaxHP(enemies));

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

                _ddaAgent?.OnTurnEnd(0, 0);

                // Check if all enemies dead
                if (!HasAliveEnemies(enemies)) break;

                // --- Enemy Turn ---
                _turnCount++;
                _isPlayerTurn = false;
                OnTurnChanged?.Invoke(_isPlayerTurn);

                // ONE random alive enemy attacks (matches actual game)
                int enemyDamageMulti = 0;
                SimEnemy attackingEnemy = GetRandomAliveEnemy(enemies);
                _attackingEnemy = attackingEnemy; // Track for UI
                if (attackingEnemy != null)
                {
                    enemyDamageMulti = ExecuteEnemyTurnForEnemy(attackingEnemy);
                    if (enemyDamageMulti > 0)
                    {
                        _player.TakeDamage(enemyDamageMulti);
                    }
                }

                // Update UI with total enemy HP
                int totalEnemyHP = GetTotalEnemyHP(enemies);
                OnBattleStateChanged?.Invoke(_player.CurrentHP, totalEnemyHP, _turnCount);

                // Update battle phase features for DDA agent
                UpdateAgentBattlePhase(enemies);

                _ddaAgent?.OnTurnEnd(0, enemyDamageMulti);
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

            // Log battle end (area index, won, HP, turns)
            TrainingLogger.LogBattleEnd(_currentAreaIndex, playerWon, playerEndHP, _battleStartHP,
                _turnCount, GetEnemyListString(), _envId);

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

            _ddaAgent?.OnBattleStart(_player.CurrentHP, GetTotalEnemyMaxHP(enemies));

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
            
            _ddaAgent?.OnTurnEnd(0, 0);

            if (!HasAliveEnemies(enemies))
            {
                yield break;
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            _turnCount++;
            _isPlayerTurn = false;
            OnTurnChanged?.Invoke(_isPlayerTurn);

            // ONE random alive enemy attacks
            int enemyDamageMulti = 0;
            SimEnemy attackingEnemy = GetRandomAliveEnemy(enemies);
            _attackingEnemy = attackingEnemy; // Track for UI
            if (attackingEnemy != null)
            {
                enemyDamageMulti = ExecuteEnemyTurnForEnemy(attackingEnemy);
                if (enemyDamageMulti > 0)
                {
                    _player.TakeDamage(enemyDamageMulti);
                }
            }

            totalEnemyHP = GetTotalEnemyHP(enemies);
            OnBattleStateChanged?.Invoke(_player.CurrentHP, totalEnemyHP, _turnCount);

            UpdateAgentBattlePhase(enemies);
            _ddaAgent?.OnTurnEnd(0, enemyDamageMulti);
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
                PlayerShield = _player.DefendUses,
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
                {
                    var result = SmartBattleAI.CalculateDamageResult(SimAction.Punch, _player, _playerSkill);
                    damage = result.Damage;
                    _ddaAgent?.OnQTECompleted(result.QTESuccess);
                    break;
                }
                case SimAction.Sword:
                    if (_player.SwordUses > 0)
                    {
                        var result = SmartBattleAI.CalculateDamageResult(SimAction.Sword, _player, _playerSkill);
                        damage = result.Damage;
                        _ddaAgent?.OnQTECompleted(result.QTESuccess);
                        _player.SwordUses--;
                    }
                    break;

                case SimAction.Gun:
                    if (_player.GunUses > 0)
                    {
                        var result = SmartBattleAI.CalculateDamageResult(SimAction.Gun, _player, _playerSkill);
                        damage = result.Damage;
                        _ddaAgent?.OnQTECompleted(result.QTESuccess);
                        _player.GunUses--;
                    }
                    break;

                case SimAction.Defend:
                    if (_player.DefendUses > 0)
                    {
                        // Set Defend absorb value and consume a shield charge
                        // Matches real game: UseShield() decrements charge, SetPlayerDefend() sets absorb
                        _player.Defend = _player.BaseDefend;
                        _player.DefendUses--;
                        // Defend has no QTE (TapZone) — matches real game
                    }
                    break;
            }

            if (damage > 0)
            {
                _ddaAgent?.OnPlayerAttack(damage);
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
        /// Gets total max HP of all enemies in battle.
        /// </summary>
        private int GetTotalEnemyMaxHP(List<SimEnemy> enemies)
        {
            if (enemies == null || enemies.Count == 0) return 0;

            int totalMax = 0;
            foreach (var enemy in enemies)
            {
                totalMax += enemy.MaxHP;
            }
            return totalMax;
        }

        /// <summary>
        /// Gets total max HP for single enemy.
        /// </summary>
        private int GetTotalEnemyMaxHP(SimEnemy enemy)
        {
            return enemy?.MaxHP ?? 0;
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