using System;
using UnityEngine;
using Manager;
using Player;
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
        private int _playerStartHP;

        public static DDAIntegration Instance { get; private set; }

        /// <summary>Is DDA currently enabled?</summary>
        public bool IsEnabled => _enableDDA;

        /// <summary>Is training mode active?</summary>
        public bool IsTrainingMode => _isTrainingMode;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // keep agent + difficulty state alive across battle scenes
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private bool _runStarted = false;

        /// <summary>
        /// Lazily resolves DDAAgent and DifficultySettings if references are stale/null
        /// (e.g. after scene reload where scene-level agents are destroyed/recreated).
        /// </summary>
        private void ResolveReferences()
        {
            // BattleSystem is scene-level (recreated per battle scene) — must re-resolve each call
            if (_battleSystem == null)
                _battleSystem = BattleSystem.Instance;

            if (_ddaAgent == null)
                _ddaAgent = FindObjectOfType<DDAAgent>();

            if (_difficultySettings == null)
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");

            if (_difficultyApplier == null)
                _difficultyApplier = DifficultyApplier.Instance;

            // Keep applier in sync with the shared settings instance
            if (_difficultyApplier != null && _difficultySettings != null)
                _difficultyApplier.SetDifficultySettings(_difficultySettings);
        }

        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            _battleLogger = BattleLogger.Instance;

            ResolveReferences();

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
        /// Called after each turn. Updates DDA state.
        /// </summary>
        public void OnTurnEnd(int damageDealtThisTurn)
        {
            if (!_enableDDA) return;
            ResolveReferences();
            if (_ddaAgent == null) return;

            _ddaAgent.OnTurnEnd(damageDealtThisTurn);
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
            _ddaAgent.OnAreaComplete(areaWon);
            // Note: agent action is async (RequestDecision). Difficulty may change next frame.
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
    }
}