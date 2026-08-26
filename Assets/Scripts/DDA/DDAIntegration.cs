using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Manager;
using Player;
using Player.Item;
using DDA;
using Playfab;

namespace DDA
{
    /// <summary>
    /// Coordinates DDA agent with battle system.
    /// Hooks into battle lifecycle events.
    /// </summary>
    public class DDAIntegration : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DDAAgent _ddaAgent;
        [SerializeField] private DifficultyApplier _difficultyApplier;
        [SerializeField] private DifficultySettings _difficultySettings;

        [Header("Configuration")]
        [SerializeField] private bool _enableDDA = true;
        [SerializeField] private bool _isTrainingMode = false;

        private BattleSystem _battleSystem;
        private BattleLogger _battleLogger;
        private DDADebugPanel _debugPanel;
        private int _playerStartHP;

        private PlayerStats _playerStats;

        private BaseAction[] _playerActions;
        private bool _actionsLoaded;

        public static DDAIntegration Instance { get; private set; }

        public bool IsEnabled => _enableDDA;
        public bool IsTrainingMode => _isTrainingMode;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private bool _runStarted = false;

        private void ResolveReferences()
        {
            if (_battleSystem == null)
                _battleSystem = BattleSystem.Instance;

            if (_battleLogger == null)
                _battleLogger = BattleLogger.Instance;

            if (_ddaAgent == null)
                _ddaAgent = FindObjectOfType<DDAAgent>();

            if (_difficultySettings == null)
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");

            if (_difficultyApplier == null)
                _difficultyApplier = DifficultyApplier.Instance;

            if (_difficultyApplier != null && _difficultySettings != null)
                _difficultyApplier.SetDifficultySettings(_difficultySettings);

            if (_debugPanel == null)
                _debugPanel = FindObjectOfType<DDADebugPanel>();

            if (_playerStats == null)
            {
                _playerStats = PlayerStats.Instance;
                if (_playerStats != null)
                {
                    _playerStats.OnPlayerLevelUp += HandlePlayerLevelUp;
                    // Push the current level immediately in case we missed earlier events
                    if (_ddaAgent != null)
                        _ddaAgent.SetPlayerLevel(_playerStats.Level);
                }
            }

            // Player action ScriptableObjects loaded from Resources (one-time). Same lookup path
            // GameManager.InitializeActions uses — we get the live, limit-tracking instances.
            if (!_actionsLoaded)
            {
                _playerActions = Resources.LoadAll<BaseAction>("Player/Actions");
                _actionsLoaded = true;
            }
        }

        /// <summary>
        /// Computes resource depletion exactly like training-sim:
        ///   (swordDepletion + gunDepletion + defendDepletion) / 3
        /// Each depletion = 1 - (CurrentLimit / Limit). Unlimited actions → 0 for that term.
        /// </summary>
        private float ComputeResourceDepletion()
        {
            if (_playerActions == null || _playerActions.Length == 0) return 0f;

            float sword = 0f, gun = 0f, defend = 0f;
            foreach (var action in _playerActions)
            {
                if (action == null) continue;
                // Align with training-sim's enum mapping: Sword=1, Gun=2, Shield=3
                switch ((int)action.ActionType)
                {
                    case 1: sword   = action.GetDepletionRatio(); break;
                    case 2: gun     = action.GetDepletionRatio(); break;
                    case 3: defend  = action.GetDepletionRatio(); break;
                }
            }
            return (sword + gun + defend) / 3f;
        }

        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            _battleLogger = BattleLogger.Instance;

            ResolveReferences();

            Debug.Log($"[DDAIntegration] Start: _ddaAgent={(_ddaAgent != null ? "OK" : "NULL")}, " +
                      $"_difficultySettings={(_difficultySettings != null ? "OK" : "NULL")}, " +
                      $"_enableDDA={_enableDDA}");

            // Set training mode
            if (_ddaAgent != null)
            {
                _ddaAgent.SetTrainingMode(_isTrainingMode);
            }

            // Real-game (inference) run bootstrap: start a run + enter area 0 baseline.
            // Training simulator drives these itself, so skip in training mode.
            if (!_isTrainingMode && !_runStarted)
            {
                _runStarted = true;
                OnRunStart();
                OnAreaEnter(MapType.Enemy, 0, 12);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe to avoid dangling delegate into a destroyed DDAIntegration
            // (e.g. if a second DDABootstrap wins the singleton race).
            if (_playerStats != null)
            {
                _playerStats.OnPlayerLevelUp -= HandlePlayerLevelUp;
                _playerStats = null;
            }
        }

        /// <summary>
        /// PlayerStats.OnPlayerLevelUp handler — pushes current player level to the DDA agent
        /// so the "Player Level" observation (normalized /5) is up-to-date when RequestDecision fires.
        /// </summary>
        private void HandlePlayerLevelUp()
        {
            if (_ddaAgent == null) return;
            if (_playerStats == null) _playerStats = PlayerStats.Instance;
            if (_playerStats == null) return;
            _ddaAgent.SetPlayerLevel(_playerStats.Level);
            Debug.Log($"[DDAIntegration] Player leveled up → {_playerStats.Level}. Agent observation updated.");
        }

        /// <summary>
        /// Called before battle starts. Requests difficulty decision from agent.
        /// </summary>
        public void OnBattlePreStart(int playerStartHP)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            _playerStartHP = playerStartHP;

            // Calculate total enemy HP from BattleSystem
            int totalEnemyHP = 0;
            if (_battleSystem != null && _battleSystem.Enemies != null)
            {
                foreach (var enemy in _battleSystem.Enemies)
                {
                    if (enemy != null && enemy.EnemyStats != null)
                    {
                        totalEnemyHP += enemy.EnemyStats.MaxHealth;
                    }
                }
            }

            _ddaAgent.OnBattleStart(playerStartHP, totalEnemyHP);

            Debug.Log($"[DDAIntegration] Battle pre-start. Player HP: {playerStartHP}, " +
                      $"Total Enemy HP: {totalEnemyHP}, Difficulty: {_difficultySettings.GetLevelName()}");
        }

        /// <summary>
        /// Called immediately when player deals damage (before enemy turn).
        /// Updates agent's damage observation in real-time for debug panel display.
        /// </summary>
        public void OnPlayerAttack(int damage)
        {
            if (!_enableDDA)
            {
                Debug.LogWarning("[DDAIntegration] OnPlayerAttack: DDA is DISABLED!");
                return;
            }
            ResolveReferences();
            if (_ddaAgent == null)
            {
                Debug.LogWarning("[DDAIntegration] OnPlayerAttack: _ddaAgent is NULL!");
                return;
            }
            Debug.Log($"[DDAIntegration] OnPlayerAttack: damage={damage}");
            _ddaAgent.OnPlayerAttack(damage);
        }

        /// <summary>
        /// Called after each turn. Updates DDA state.
        /// </summary>
        public void OnTurnEnd(int damageDealtThisTurn, int damageTakenThisTurn = 0)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null)
            {
                Debug.LogWarning("[DDAIntegration] OnTurnEnd: _ddaAgent is NULL — damage not tracked!");
                return;
            }

            Debug.Log($"[DDAIntegration] OnTurnEnd: dmgDealt={damageDealtThisTurn}, dmgTaken={damageTakenThisTurn}");
            _ddaAgent.OnTurnEnd(damageDealtThisTurn, damageTakenThisTurn);
        }

        /// <summary>
        /// Called when a QTE (TapZone) minigame completes.
        /// </summary>
        public void OnQTECompleted(bool success)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            _ddaAgent.OnQTECompleted(success);
        }

        /// <summary>
        /// Called when battle ends. Calculates reward and triggers learning.
        /// </summary>
        public void OnBattleEnd(bool playerWon, int playerEndHP)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            _ddaAgent.OnBattleEnd(playerWon, playerEndHP);

            float hpRatio = _ddaAgent.GetHpRatio();
            float resourceDepletion = ComputeResourceDepletion();
            _ddaAgent.UpdateBattlePhase(hpRatio, resourceDepletion);

            if (_battleLogger != null)
            {
                var payload = new DDALogPayload
                {
                    dda_action_taken = _difficultySettings?.GetLevelName() ?? "N/A",
                    dda_reward = 0f, // reward is per-area, not per-battle in live game
                    dda_obs_snapshot = new float[]
                    {
                        _ddaAgent.GetHpRatio(),
                        _ddaAgent.GetTurnCountNormalized(),
                        _ddaAgent.GetPlayerLevelNormalized(),
                        _ddaAgent.GetDamageDealtRatio(),
                        _ddaAgent.GetQTEAccuracy(),
                        _ddaAgent.GetResourceDepletion()
                    },
                    dda_episode_count = 0, // not tracked in live game
                    player_hp_ratio = _playerStartHP > 0 ? (float)playerEndHP / _playerStartHP : 0f,
                    total_turns = _ddaAgent.GetTurnCount(),
                    damage_taken = _ddaAgent.GetDamageTaken(),
                    heals_used = 0 // TODO: track from BattleSystem
                };
                _battleLogger.LogDDAEvent(payload);
            }

            _debugPanel?.OnBattleEnd(playerWon, playerEndHP, _playerStartHP);

            Debug.Log($"[DDAIntegration] Battle end. Won: {playerWon}, " +
                      $"End HP: {playerEndHP}/{_playerStartHP}");
        }

        /// <summary>
        /// Applies difficulty to enemy stats before spawn.
        /// </summary>
        public void ApplyDifficultyToEnemy(EnemyStats enemyStats)
        {
            if (!_enableDDA || enemyStats == null) return;
            ResolveReferences();

            if (_difficultySettings == null)
            {
                Debug.LogWarning("[DDAIntegration] DifficultySettings null — cannot apply difficulty.");
                return;
            }

            float hpMult = _difficultySettings.HPMultiplier;
            float dmgMult = _difficultySettings.DamageMultiplier;

            // Prefer going through DifficultyApplier (keeps it as the canonical applier),
            // but fall back to applying directly if no applier exists in the scene.
            if (_difficultyApplier != null)
            {
                _difficultyApplier.ApplyDifficulty(enemyStats);
            }
            else
            {
                enemyStats.ApplyDifficultyMultiplier(hpMult, dmgMult);
                Debug.Log($"[DDAIntegration] Directly applied {_difficultySettings.GetLevelName()} " +
                          $"(HP x{hpMult:F2}, DMG x{dmgMult:F2}) to {enemyStats.EnemyName}");
            }
        }

        /// <summary>
        /// Enables or disables DDA at runtime.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enableDDA = enabled;
        }

        /// <summary>
        /// Injects a runtime DifficultySettings instance (called by DDABootstrap).
        /// </summary>
        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }

        /// <summary>
        /// Sets training mode at runtime.
        /// </summary>
        public void SetTrainingMode(bool isTraining)
        {
            _isTrainingMode = isTraining;
            if (_ddaAgent != null)
            {
                _ddaAgent.SetTrainingMode(isTraining);
            }
        }

        /// <summary>
        /// Gets current difficulty level name for UI.
        /// </summary>
        public string GetCurrentDifficultyName()
        {
            if (_difficultySettings == null)
            {
                return "Normal";
            }
            return _difficultySettings.GetLevelName();
        }

        /// <summary>
        /// Gets current difficulty multipliers for UI.
        /// </summary>
        public (float hp, float damage) GetCurrentMultipliers()
        {
            if (_difficultySettings == null)
            {
                return (1.0f, 1.0f);
            }
            return (_difficultySettings.HPMultiplier, _difficultySettings.DamageMultiplier);
        }

        /// <summary>
        /// Called when entering a new area in the real game.
        /// Passes area context (type, depth) to the DDA agent.
        /// </summary>
        public void OnAreaEnter(MapType areaType, int areaIndex, int totalAreas)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            _ddaAgent.OnAreaEnter(areaIndex, areaType, totalAreas);

            // Notify debug panel
            _debugPanel?.OnAreaEnter(areaIndex, areaType.ToString());

            Debug.Log($"[DDAIntegration] Area enter. Type={areaType}, " +
                      $"Depth={areaIndex}/{totalAreas}, " +
                      $"Difficulty: {_difficultySettings?.GetLevelName() ?? "N/A"}");
        }

        /// <summary>Begins a run (resets difficulty to baseline, resets run state).</summary>
        public void OnRunStart()
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;
            _ddaAgent.OnRunStart();
            Debug.Log($"[DDAIntegration] Run started. Difficulty reset to {_difficultySettings?.GetLevelName() ?? "N/A"}.");
        }

        /// <summary>Called after an area's battle resolves. Triggers agent decision for next area.</summary>
        public void OnAreaComplete(bool areaWon)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            string diffBefore = _difficultySettings?.GetLevelName() ?? "N/A";
            float hpMultBefore = _difficultySettings?.HPMultiplier ?? 1f;
            float dmgMultBefore = _difficultySettings?.DamageMultiplier ?? 1f;
            int areaIdx = MapSystem.Instance?.AreaIndex ?? 0;
            string areaType = MapSystem.Instance?.GetMapType().ToString() ?? "";

            _ddaAgent.OnAreaComplete(areaWon);

            StartCoroutine(LogDecisionAfterFrame(areaIdx, areaType, diffBefore, hpMultBefore, dmgMultBefore));

            Debug.Log($"[DDAIntegration] Area complete. Won={areaWon}, Difficulty was {diffBefore} (agent decision pending).");
        }

        /// <summary>Called when a run ends (boss cleared / player death).</summary>
        public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;
            _ddaAgent.OnRunEnd(runWon, areasCompleted, totalAreas);
            Debug.Log($"[DDAIntegration] Run end. Won={runWon}, Areas={areasCompleted}/{totalAreas}.");
        }

        /// <summary>Called when player dies mid-run (HP = 0).</summary>
        public void OnPlayerDeath(int areasCompleted, int totalAreas)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;
            _ddaAgent.OnPlayerDeath(areasCompleted, totalAreas);
            Debug.Log($"[DDAIntegration] Player died! Areas={areasCompleted}/{totalAreas}.");
        }

        /// <summary>
        /// Waits one frame for the agent's RequestDecision to be processed by Academy,
        /// then logs any difficulty change.
        /// </summary>
        private IEnumerator LogDecisionAfterFrame(int areaIdx, string areaType,
            string diffBefore, float hpMultBefore, float dmgMultBefore)
        {
            yield return null; // wait one frame for Academy to process decision

            string diffAfter = _difficultySettings?.GetLevelName() ?? "N/A";

            if (diffBefore != diffAfter)
            {
                Debug.Log($"[DDAIntegration] Difficulty changed: {diffBefore} → {diffAfter}");
            }
        }
    }
}