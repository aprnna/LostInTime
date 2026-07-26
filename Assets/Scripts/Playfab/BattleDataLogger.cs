using System;
using System.Collections.Generic;
using Player;
using Player.Item;
using Sirenix.OdinInspector;
using UnityEngine;
using Newtonsoft.Json;
namespace Playfab
{
    public class BattleLogger : MonoBehaviour
    {
        public static BattleLogger Instance { get; private set; }
        private BattleLog currentLog;
        
        [Header("Battle Tracking")]
        [SerializeField] private DateTime battleStartTime;
        [SerializeField] private DateTime battleEndTime;
        [SerializeField] private int playerStartHP;
        [SerializeField] private int playerEndHP;
        [SerializeField] private int totalEnemyStartHP;
        [SerializeField] private int totalEnemyEndHP;
        [SerializeField] private int playerTurnCount;
        [SerializeField] private int enemyTurnCount;
        [SerializeField] private List<EnemyController> Enemies;
        [SerializeField] private int playerWonCount;
        [SerializeField] private int playerDeathCount;

        [Header("Player Actions Tracking")]
        [SerializeField] private int fistUsedCount;
        [SerializeField] private int swordUsedCount;
        [SerializeField] private int gunUsedCount;
        [SerializeField] private int defendUsedCount;
        [SerializeField] private int damageDealtTotal;
        [SerializeField] private int damageTakenTotal;
        [SerializeField] private int criticalSuccessCount;
        
        [Header("Battle Events")]
        private List<BattleEvent> battleEvents = new List<BattleEvent>();
        
        [Header("References")]
        [SerializeField] private string sessionId;
        [SerializeField] private string levelId;
        public bool HasActiveSession => currentLog != null;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // keep currentLog alive across scene loads until uploaded
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public void CreateNewLog(string sessionId)
        {
            this.sessionId = sessionId;
            currentLog = GenerateBattleLog(sessionId);
            BattleFileLogger.Initialize(sessionId);
            BattleFileLogger.WriteEvent("session_start", new { session_id = sessionId, started_at = DateTime.UtcNow.ToString("o") });
        }
        public BattleLog GetCurrentLog()
        {
            return currentLog;
        }
        public void AddLevelRecord(BattleRecord record)
        {
            if (currentLog == null)
            {
                Debug.LogWarning("⚠ AddLevelRecord called without session! Auto creating session.");
                CreateNewLog(Guid.NewGuid().ToString());
            }

            currentLog.Battle_Record.Add(record);

            if (record.battle_result == "Win")
            {
                playerWonCount++;
                currentLog.win_count = playerWonCount;
            }
            else
            {
                playerDeathCount++;
                currentLog.player_death_count = playerDeathCount;
            }
        }

       

        #region Battle Lifecycle

        [Header("Battle Context (set by BattleSystem)")]
        [SerializeField] private string currentAreaType;
        [SerializeField] private string currentDifficultyName;
        [SerializeField] private float currentHpMultiplier;
        [SerializeField] private float currentDamageMultiplier;

        private PlayerSnapshot playerSnapshotStart;
        private int totalExpEarned;
        private int totalCoinEarned;

        /// <summary>
        /// Set battle context (area type, difficulty) before calling StartBattle.
        /// Called by BattleSystem.
        /// </summary>
        public void SetBattleContext(string areaType, string difficultyName, float hpMultiplier, float damageMultiplier)
        {
            currentAreaType = areaType;
            currentDifficultyName = difficultyName;
            currentHpMultiplier = hpMultiplier;
            currentDamageMultiplier = damageMultiplier;
        }

        public void StartBattle(string levelId, int playerHP, List<EnemyController> enemies)
        {
            this.levelId = levelId;

            battleStartTime = DateTime.UtcNow;
            playerStartHP = playerHP;

            Enemies = enemies;
            totalEnemyStartHP = 0;
            totalExpEarned = 0;
            totalCoinEarned = 0;
            foreach (var enemy in Enemies)
            {
                totalEnemyStartHP += enemy.EnemyStats.Health;
                totalExpEarned += enemy.EnemyStats.ExpReward;
                totalCoinEarned += enemy.EnemyStats.CoinReward;
            }
            totalEnemyEndHP = totalEnemyStartHP;

            // Capture player snapshot at battle start
            playerSnapshotStart = CapturePlayerSnapshot();

            ResetCounters();
            battleEvents.Clear();

            BattleFileLogger.WriteEvent("battle_start", new
            {
                level_id = levelId,
                area_type = currentAreaType,
                difficulty_name = currentDifficultyName,
                hp_multiplier = currentHpMultiplier,
                damage_multiplier = currentDamageMultiplier,
                enemy_count = Enemies.Count,
                total_enemy_start_hp = totalEnemyStartHP,
                exp_available = totalExpEarned,
                coin_available = totalCoinEarned,
                player_performance = new
                {
                    player_level = playerSnapshotStart?.level ?? 0,
                    player_hp = playerSnapshotStart?.hp ?? 0,
                    player_max_hp = playerSnapshotStart?.max_hp ?? 0,
                    player_defend = playerSnapshotStart?.defend ?? 0,
                    player_base_defend = playerSnapshotStart?.base_defend ?? 0,
                    player_base_damage = playerSnapshotStart?.base_damage ?? 0,
                    player_exp = playerSnapshotStart?.exp ?? 0,
                    player_max_exp = playerSnapshotStart?.max_exp ?? 0,
                    player_coin = playerSnapshotStart?.coin ?? 0
                }
            });

            Debug.Log($"[BattleLogger] Battle started - Session: {sessionId}, Level: {levelId}, Area: {currentAreaType}, Difficulty: {currentDifficultyName}");
        }

        public void EndBattle(int playerFinalHP, int totalEnemyFinalHP, bool playerWon)
        {
            battleEndTime = DateTime.UtcNow;
            playerEndHP = playerFinalHP;
            totalEnemyEndHP = totalEnemyFinalHP;

            var record = GenerateBattleRecord(playerWon);

            AddLevelRecord(record);

            BattleFileLogger.WriteEvent("battle_end", new
            {
                level_id = levelId,
                area_type = currentAreaType,
                difficulty_name = currentDifficultyName,
                battle_result = record.battle_result,
                battle_duration = record.battle_duration,
                player_turn_count = record.player_turn_count,
                enemy_turn_count = record.enemy_turn_count,
                enemy_count = record.Enemy_count,
                enemy_total_hp_start = record.enemy_total_hp_start,
                enemy_total_hp_end = record.enemy_total_hp_end,
                exp_earned = record.exp_earned,
                coin_earned = record.coin_earned,
                player_performance = record.player_performance,
                player_behavior = record.player_behavior,
                turn_logs = record.turn_logs
            });

            string result = playerWon ? "Victory" : "Defeat";
            
            Debug.Log($"[BattleLogger] Battle ended - Duration: {GetBattleDuration():F2}s, Player Turns: {playerTurnCount}, Enemy Turns: {enemyTurnCount}, Result: {result}");
        }

        #endregion

        #region Turn Tracking

        public void OnPlayerTurn(PlayerActionType playerActionType,string targetName, int targetHpBefore, int targetHpAfter, int damage, bool isCritical = false)
        {
            playerTurnCount++;
            string criticalText = isCritical ? " (CRITICAL!)" : "";
            damageDealtTotal += damage;
            if (isCritical) criticalSuccessCount++;
            CalculatePlayerAction(playerActionType);

            BattleEvent battleEvent = new BattleEvent
            {
                eventType = BattleEventType.PlayerTurn,
                timestamp = DateTime.UtcNow,
                playerActionType = playerActionType,
                turn = playerTurnCount,
                damage = damage,
                target = targetName,
                targetHPBefore = targetHpBefore,
                targetHPAfter =  targetHpAfter,
                is_critical = isCritical,
                description =  $"Player Turn {playerTurnCount} - Player attacked {targetName} for {damage} damage {criticalText}"
            };

            LogBattleEvent(battleEvent);

            BattleFileLogger.WriteEvent("player_turn", new
            {
                player_turn = playerTurnCount,
                player_level = PlayerStats.Instance?.Level ?? 0,
                action = playerActionType.ToString(),
                target = targetName,
                target_hp_before = targetHpBefore,
                target_hp_after = targetHpAfter,
                damage = damage,
                is_critical = isCritical
            });
        }

        public void OnPlayerDeath()
        {
            playerDeathCount++;
            BattleFileLogger.WriteEvent("player_death", new { player_turn = playerTurnCount });
        }
      
        public void OnEnemyTurn(int playerHpAfter, int playerHpBefore, int totalDamage)
        {
            enemyTurnCount++;
            damageTakenTotal += totalDamage;

            BattleEvent battleEvent = new BattleEvent
            {
                eventType = BattleEventType.EnemyTurn,
                timestamp = DateTime.UtcNow,
                playerActionType = PlayerActionType.Default,
                turn = enemyTurnCount,
                damage = totalDamage,
                target = "Player",
                targetHPAfter = playerHpAfter,
                targetHPBefore = playerHpBefore,
                description =  $"Enemy Turn {enemyTurnCount} - Enemies attack player for total {totalDamage} damage"
            };
            LogBattleEvent(battleEvent);

            BattleFileLogger.WriteEvent("enemy_turn", new
            {
                enemy_turn = enemyTurnCount,
                player_level = PlayerStats.Instance?.Level ?? 0,
                player_hp_before = playerHpBefore,
                player_hp_after = playerHpAfter,
                damage = totalDamage
            });
        }

        #endregion

        #region Action Tracking

        public void CalculatePlayerAction(PlayerActionType playerActionType)
        {
            switch (playerActionType)
            {
                case PlayerActionType.Fist:
                    fistUsedCount++;
                    break;
                case PlayerActionType.Sword:
                    swordUsedCount++;
                    break;
                case PlayerActionType.Gun:
                    gunUsedCount++;
                    break;
                case PlayerActionType.Shield:
                    defendUsedCount++;
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Data Generation

        public BattleLog GenerateBattleLog(string sessionId)
        {
            var log = new BattleLog
            {
                session_id = sessionId,
                timestamp = DateTime.UtcNow.ToString("o"),
                player_death_count = 0,
                win_count = 0,
                Battle_Record = new List<BattleRecord>()
            };

            return log;
        }
        private BattleRecord GenerateBattleRecord(bool playerWon)
        {
            // Capture player snapshot at battle end
            PlayerSnapshot playerSnapshotEnd = CapturePlayerSnapshot();

            var perf = new PlayerPerformance
            {
                player_hp_start = playerStartHP,
                player_hp_end = playerEndHP,
                player_max_hp = playerSnapshotEnd?.max_hp ?? 0,
                player_defend = playerSnapshotEnd?.defend ?? 0,
                player_base_defend = playerSnapshotEnd?.base_defend ?? 0,
                player_level = playerSnapshotEnd?.level ?? 0,
                player_base_damage = playerSnapshotEnd?.base_damage ?? 0,
                player_exp = playerSnapshotEnd?.exp ?? 0,
                player_max_exp = playerSnapshotEnd?.max_exp ?? 0,
                player_coin = playerSnapshotEnd?.coin ?? 0,
                damage_dealt = damageDealtTotal,
                damage_taken = damageTakenTotal
            };

            // Load actions to get max usage and critical bonus per action
            var actions = Resources.LoadAll<BaseAction>("Player/Actions");
            int fistMax = 0, swordMax = 0, gunMax = 0, defendMax = 0;
            int fistCrit = 0, swordCrit = 0, gunCrit = 0;
            foreach (var action in actions)
            {
                switch (action.ActionType)
                {
                    case PlayerActionType.Fist:
                        fistMax = action.CurrentLimit;
                        fistCrit = action.CriticalBonusPercent;
                        break;
                    case PlayerActionType.Sword:
                        swordMax = action.CurrentLimit;
                        swordCrit = action.CriticalBonusPercent;
                        break;
                    case PlayerActionType.Gun:
                        gunMax = action.CurrentLimit;
                        gunCrit = action.CriticalBonusPercent;
                        break;
                    case PlayerActionType.Shield:
                        defendMax = action.CurrentLimit;
                        break;
                }
            }

            return new BattleRecord
            {
                level_id = levelId,
                battle_result = playerWon ? "Win" : "Lose",
                battle_duration = GetBattleDuration(),
                player_turn_count = playerTurnCount,
                enemy_turn_count = enemyTurnCount,
                Enemy_count = Enemies.Count,
                enemy_list = ExtractEnemyListData(Enemies),
                enemy_total_hp_start = totalEnemyStartHP,
                enemy_total_hp_end = totalEnemyEndHP,
                player_performance = perf,
                player_behavior = new PlayerBehavior
                {
                    fist_used = fistUsedCount,
                    sword_used = swordUsedCount,
                    gun_used = gunUsedCount,
                    defend_used = defendUsedCount,
                    critical_success = criticalSuccessCount,
                    fist_max_usage = fistMax,
                    sword_max_usage = swordMax,
                    gun_max_usage = gunMax,
                    defend_max_usage = defendMax,
                    fist_critical_pct = fistCrit,
                    sword_critical_pct = swordCrit,
                    gun_critical_pct = gunCrit
                },
                turn_logs = ConvertEventsToTurnLog(),

                // Enriched fields
                player_snapshot_start = playerSnapshotStart,
                player_snapshot_end = playerSnapshotEnd,
                area_type = currentAreaType,
                difficulty_name = currentDifficultyName,
                hp_multiplier = currentHpMultiplier,
                damage_multiplier = currentDamageMultiplier,
                exp_earned = playerWon ? totalExpEarned : 0,
                coin_earned = playerWon ? totalCoinEarned : 0
            };
        }
        
        public float GetBattleDuration()
        {
            if (battleEndTime == DateTime.MinValue)
            {
                return (float)(DateTime.UtcNow - battleStartTime).TotalSeconds;
            }
            return (float)(battleEndTime - battleStartTime).TotalSeconds;
        }

        public List<BattleEvent> GetBattleEvents()
        {
            return new List<BattleEvent>(battleEvents);
        }

        #endregion

        #region Helper Methods

        private PlayerSnapshot CapturePlayerSnapshot()
        {
            var ps = PlayerStats.Instance;
            if (ps == null) return null;
            return new PlayerSnapshot
            {
                level = ps.Level,
                hp = ps.Health,
                max_hp = ps.MaxHealth,
                shield = ps.Shield,
                max_shield = ps.MaxShield,
                defend = ps.Defend,
                base_defend = ps.BaseDefend,
                base_damage = ps.BaseDamage,
                exp = ps.Exp,
                max_exp = ps.MaxExp,
                coin = ps.Coin
            };
        }

        private void ResetCounters()
        {
            playerTurnCount = 0;
            enemyTurnCount = 0;
            gunUsedCount = 0;
            fistUsedCount = 0;
            swordUsedCount = 0;
            defendUsedCount = 0;
            damageDealtTotal = 0;
            damageTakenTotal = 0;
            criticalSuccessCount = 0;
        }

        public void LogBattleEvent(BattleEvent battleEvent)
        {
     
            battleEvents.Add(battleEvent);
            
            if (Application.isEditor)
            {
                Debug.Log($"[BattleLogger] {battleEvent.eventType} Turn {battleEvent.turn} | {battleEvent.description}");
            }
        }
        
        private List<TurnLog> ConvertEventsToTurnLog()
        {
            var list = new List<TurnLog>();

            foreach (var evt in battleEvents)
            {
                list.Add(new TurnLog
                {
                    turn_number = evt.turn,
                    actor = evt.eventType.ToString(),
                    target = evt.target,
                    action = evt.playerActionType.ToString(),
                    damage = evt.damage,
                    is_critical = evt.is_critical,
                    target_hp_before = evt.targetHPBefore,
                    target_hp_after = evt.targetHPAfter,
                    description = evt.description,
                });
            }

            return list;
        }

        private List<EnemyInfo> ExtractEnemyListData(List<EnemyController> enemies)
        {
            var enemyInfos = new List<EnemyInfo>();
            foreach (var enemy in enemies)
            {
                enemyInfos.Add(new EnemyInfo
                {
                    enemy_id = enemy.EnemyStats.EnemyName,
                    enemy_type = enemy.EnemyStats.EnemyType.ToString(),
                    hp_start = enemy.EnemyStats.MaxHealth,
                    hp_end = enemy.EnemyStats.Health,
                    base_damage = enemy.EnemyStats.BaseDamage,
                    exp_reward = enemy.EnemyStats.ExpReward,
                    coin_reward = enemy.EnemyStats.CoinReward
                });
            }

            return enemyInfos;
        }
        #endregion
        
        #region Debug

        [Button]
        public void PrintBattleSummary()
        {
            Debug.Log("=== BATTLE SUMMARY ===");
            Debug.Log($"Session: {sessionId}");
            Debug.Log($"Level: {levelId}");
            Debug.Log($"Area: {currentAreaType} | Difficulty: {currentDifficultyName} (HP x{currentHpMultiplier:F2}, DMG x{currentDamageMultiplier:F2})");
            Debug.Log($"Duration: {GetBattleDuration():F2}s");
            Debug.Log($"Player Turns: {playerTurnCount} | Enemy Turns: {enemyTurnCount}");
            Debug.Log($"\nPlayer Stats:");
            Debug.Log($"  HP: {playerStartHP} → {playerEndHP}");
            if (playerSnapshotStart != null)
            {
                Debug.Log($"  Level: {playerSnapshotStart.level} | Base DMG: {playerSnapshotStart.base_damage} | Base DEF: {playerSnapshotStart.base_defend}");
                Debug.Log($"  Shield: {playerSnapshotStart.shield}/{playerSnapshotStart.max_shield} | Defend: {playerSnapshotStart.defend}");
                Debug.Log($"  EXP: {playerSnapshotStart.exp}/{playerSnapshotStart.max_exp} | Coin: {playerSnapshotStart.coin}");
            }
            Debug.Log($"  EXP Earned: {totalExpEarned} | Coin Earned: {totalCoinEarned}");
            Debug.Log($"  Damage Dealt: {damageDealtTotal}");
            Debug.Log($"  Damage Taken: {damageTakenTotal}");
            Debug.Log($"\nActions:");
            Debug.Log($"  Fist Used: {fistUsedCount}");
            Debug.Log($"  Sword Used: {swordUsedCount}");
            Debug.Log($"  Gun Used: {gunUsedCount}");
            Debug.Log($"  Defend Used: {defendUsedCount}");
            Debug.Log($"  Critical Success: {criticalSuccessCount}");
            Debug.Log($"\nTotal Events: {battleEvents.Count}");
        }

        [Button]
        public void PrintAllEvents()
        {
            Debug.Log("=== BATTLE EVENTS ===");
            foreach (var evt in battleEvents)
            {
                Debug.Log($"[Turn {evt.turn}] {evt.eventType}: {evt.description}");
            }
        }

        #endregion

        #region Level Up Logging

        /// <summary>
        /// Log when player levels up. Called by PlayerStats.LevelUp().
        /// </summary>
        public void OnPlayerLevelUp(int newLevel, int remainingExp, int newMaxExp, int oldLevel)
        {
            BattleFileLogger.WriteEvent("player_level_up", new
            {
                old_level = oldLevel,
                new_level = newLevel,
                remaining_exp = remainingExp,
                new_max_exp = newMaxExp,
                player_hp = PlayerStats.Instance?.Health ?? 0,
                player_max_hp = PlayerStats.Instance?.MaxHealth ?? 0,
                player_base_damage = PlayerStats.Instance?.BaseDamage ?? 0,
                player_base_defend = PlayerStats.Instance?.BaseDefend ?? 0
            });

            Debug.Log($"[BattleLogger] Player leveled up: {oldLevel} → {newLevel}");
        }

        /// <summary>
        /// Log when player selects a level-up reward item.
        /// </summary>
        public void OnLevelUpItemChosen(string itemType, int amount, int newStatValue)
        {
            BattleFileLogger.WriteEvent("level_up_item_chosen", new
            {
                item_type = itemType,
                amount = amount,
                new_stat_value = newStatValue,
                player_level = PlayerStats.Instance?.Level ?? 0,
                player_hp = PlayerStats.Instance?.Health ?? 0,
                player_max_hp = PlayerStats.Instance?.MaxHealth ?? 0,
                player_base_damage = PlayerStats.Instance?.BaseDamage ?? 0,
                player_base_defend = PlayerStats.Instance?.BaseDefend ?? 0
            });

            Debug.Log($"[BattleLogger] Level-up item chosen: {itemType} +{amount}");
        }

        #endregion

        #region DDA Logging

        /// <summary>
        /// Log event DDA (Dynamic Difficulty Adjustment) ke PlayFab.
        /// </summary>
        /// <param name="payload">Payload data DDA.</param>
        public void LogDDAEvent(DDALogPayload payload)
        {
            if (currentLog == null)
            {
                Debug.LogWarning("[BattleLogger] LogDDAEvent dipanggil tanpa session!");
                return;
            }

            // Tambahkan data DDA ke battle record terakhir
            var lastRecord = currentLog.Battle_Record.Count > 0
                ? currentLog.Battle_Record[currentLog.Battle_Record.Count - 1]
                : null;

            if (lastRecord != null)
            {
                lastRecord.dda_action_taken = payload.dda_action_taken;
                lastRecord.dda_reward = payload.dda_reward;
                lastRecord.dda_obs_snapshot = payload.dda_obs_snapshot;
                lastRecord.dda_episode_count = payload.dda_episode_count;

                // Populate AppliedDifficultyParams only when DDA is active
                lastRecord.applied_difficulty_params = new AppliedDifficultyParams
                {
                    enemy_count = lastRecord.Enemy_count,
                    enemy_damage_scale = currentDamageMultiplier,
                    player_heal_amount = 0, // TODO: track actual heals
                    shop_price_modifier = 1.0f // TODO: track actual modifier
                };
            }

            if (Application.isEditor)
            {
                Debug.Log($"[BattleLogger] DDA Event: Action={payload.dda_action_taken}, Reward={payload.dda_reward:F3}");
            }

            BattleFileLogger.WriteEvent("dda_event", new
            {
                dda_action_taken = payload.dda_action_taken,
                dda_reward = payload.dda_reward,
                dda_obs_snapshot = payload.dda_obs_snapshot,
                dda_episode_count = payload.dda_episode_count,
                player_level = PlayerStats.Instance?.Level ?? 0,
                player_hp_ratio = payload.player_hp_ratio,
                total_turns = payload.total_turns,
                damage_taken = payload.damage_taken,
                heals_used = payload.heals_used
            });
        }

        #endregion
    }

    [Serializable]
    public class BattleLog
    {
        public string session_id;
        public string timestamp;
        public int player_death_count;
        public int win_count;
        public List<BattleRecord> Battle_Record = new();
    }

    [Serializable]
    public class BattleRecord
    {
        public string level_id;
        public string battle_result;
        public float battle_duration;
        public int player_turn_count;
        public int enemy_turn_count;
        public int Enemy_count;
        public List<EnemyInfo> enemy_list;
        public int enemy_total_hp_start;
        public int enemy_total_hp_end;
        public PlayerPerformance player_performance;
        public PlayerBehavior player_behavior;
        public List<TurnLog> turn_logs;

        // Player state snapshots
        public PlayerSnapshot player_snapshot_start;
        public PlayerSnapshot player_snapshot_end;

        // Battle context
        public string area_type;
        public string difficulty_name;
        public float hp_multiplier;
        public float damage_multiplier;

        // Resource deltas
        public int exp_earned;
        public int coin_earned;

        // DDA fields (only populated when DDA is enabled)
        public string dda_action_taken;
        public float dda_reward;
        public float[] dda_obs_snapshot;
        public int dda_episode_count;
        public AppliedDifficultyParams applied_difficulty_params;
    }

    [Serializable]
    public class EnemyInfo
    {
        public string enemy_id;
        public string enemy_type;
        public int hp_start;
        public int hp_end;
        public int base_damage;
        public int exp_reward;
        public int coin_reward;
    }

    [Serializable]
    public class PlayerSnapshot
    {
        public int level;
        public int hp;
        public int max_hp;
        public int shield;         // consumable shield charges
        public int max_shield;     // max consumable shield charges
        public int defend;         // active defend points (absorbs damage before HP)
        public int base_defend;    // defend granted when using Shield action
        public int base_damage;
        public int exp;
        public int max_exp;
        public int coin;
    }

    [Serializable]
    public class TurnLog
    {
        public int turn_number;
        public string actor;
        public string target;
        public string action;
        public int damage;
        public bool is_critical;
        public int target_hp_before;
        public int target_hp_after;
        public string description;
    }

    [Serializable]
    public class AppliedDifficultyParams
    {
        public int enemy_count;
        public float enemy_damage_scale;
        public int player_heal_amount;
        public float shop_price_modifier;
    }

    [Serializable]
    public class PlayerPerformance
    {
        // HP tracking
        public int player_hp_start;
        public int player_hp_end;
        public int player_max_hp;

        // Defend
        public int player_defend;         // active defend points (absorbs damage before HP)
        public int player_base_defend;    // defend granted when using Shield action

        // Stats
        public int player_level;
        public int player_base_damage;

        // Resources
        public int player_exp;
        public int player_max_exp;
        public int player_coin;

        // Damage tracking
        public int damage_dealt;
        public int damage_taken;
    }
    [Serializable]
    public class PlayerBehavior
    {
        // Action usage counts
        public int fist_used;
        public int sword_used;
        public int gun_used;
        public int defend_used;
        public int critical_success;

        // Max usage per action (from BaseAction._limit)
        public int fist_max_usage;
        public int sword_max_usage;
        public int gun_max_usage;
        public int defend_max_usage;    // equals shield charges

        // Critical bonus % per action (from BaseAction._criticalBonusPercent)
        public int fist_critical_pct;
        public int sword_critical_pct;
        public int gun_critical_pct;
    }

    [Serializable]
    public class DDALogPayload
    {
        public string dda_action_taken;
        public float dda_reward;
        public float[] dda_obs_snapshot;
        public int dda_episode_count;
        public float player_hp_ratio;
        public int total_turns;
        public int damage_taken;
        public int heals_used;
    }
    
    [Serializable]
    public class BattleEvent
    {
        public string target;
        public BattleEventType eventType;
        public DateTime timestamp;
        public PlayerActionType playerActionType;
        public int turn;
        public int damage;
        public bool is_critical;
        public int targetHPAfter;
        public int targetHPBefore;
        public string description;
    }

    public enum BattleEventType
    {
        PlayerTurn,
        EnemyTurn,
    }
}