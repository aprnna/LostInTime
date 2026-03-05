using System;
using System.Collections.Generic;
using Player;
using Sirenix.OdinInspector;
using UnityEngine;
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
        [SerializeField] private int turnCount;
        [SerializeField] private List<EnemyController> Enemies;
        [SerializeField] private int playerWonCount;
        [SerializeField] private int playerDeathCount;
        
        [Header("Player Actions Tracking")]
        [SerializeField] private int actionCount;
        [SerializeField] private int fistUsedCount;
        [SerializeField] private int swordUsedCount;
        [SerializeField] private int gunUsedCount;
        [SerializeField] private int defendUsedCount;
        [SerializeField] private int damageDealtTotal;
        [SerializeField] private int damageTakenTotal;
        
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
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public void CreateNewLog(string sessionId)
        {
            currentLog = GenerateBattleLog(sessionId);
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

            currentLog.win_count += record.battle_result == "Win" ? 1 : 0;
            currentLog.player_death_count += record.battle_result == "Lose" ? 1 : 0;
        }

       

        #region Battle Lifecycle

        public void StartBattle(string levelId, int playerHP, List<EnemyController> enemies)
        {
            this.levelId = levelId;
            
            battleStartTime = DateTime.UtcNow;
            playerStartHP = playerHP;
            
            Enemies = enemies;
            totalEnemyStartHP = 0;
            foreach (var enemy in Enemies)
            {
                totalEnemyStartHP += enemy.EnemyStats.Health;
            }
            totalEnemyEndHP = totalEnemyStartHP;
            
            ResetCounters();
            battleEvents.Clear();
            
            Debug.Log($"[BattleLogger] Battle started - Session: {sessionId}, Level: {levelId}");
        }

        public void EndBattle(int playerFinalHP, int totalEnemyFinalHP, bool playerWon)
        {
            battleEndTime = DateTime.UtcNow;
            playerEndHP = playerFinalHP;
            totalEnemyEndHP = totalEnemyFinalHP;

            var record = GenerateBattleRecord(playerWon);

            AddLevelRecord(record);
            
            string result = playerWon ? "Victory" : "Defeat";
            
            Debug.Log($"[BattleLogger] Battle ended - Duration: {GetBattleDuration():F2}s, Turns: {turnCount}, Result: {result}");
        }

        #endregion

        #region Turn Tracking

        public void OnPlayerTurn(PlayerActionType playerActionType,string targetName, int targetHpBefore, int targetHpAfter, int damage, bool isCritical = false)
        {
            turnCount++;
            string criticalText = isCritical ? " (CRITICAL!)" : "";
            damageDealtTotal += damage;
            CalculatePlayerAction(playerActionType);
            
            BattleEvent battleEvent = new BattleEvent
            {
                eventType = BattleEventType.PlayerTurn,
                timestamp = DateTime.UtcNow,
                playerActionType = playerActionType,
                turn = turnCount,
                damage = damage,
                target = targetName,
                targetHPBefore = targetHpBefore,
                targetHPAfter =  targetHpAfter,
                description =  $"Turn {turnCount} - Player attacked {targetName} for {damage} damage {criticalText}"
            };

            LogBattleEvent(battleEvent);
        }

        public void OnPlayerDeath()
        {
            playerDeathCount++;
        }
      
        public void OnEnemyTurn(int playerHpAfter, int playerHpBefore, int totalDamage)
        {
            damageTakenTotal += totalDamage;

            BattleEvent battleEvent = new BattleEvent
            {
                eventType = BattleEventType.EnemyTurn,
                timestamp = DateTime.UtcNow,
                playerActionType = PlayerActionType.Default,
                turn = turnCount,
                damage = totalDamage,
                target = "Player",
                targetHPAfter = playerHpAfter,
                targetHPBefore = playerHpBefore,
                description =  $"Turn {turnCount} - Enemies attack player for total {totalDamage} damage"
            };
            LogBattleEvent(battleEvent);
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
                timestamp = battleStartTime.ToString("o"),
                player_death_count = playerDeathCount,
                win_count = playerWonCount,
                Battle_Record = new List<BattleRecord>()
            };

            return log;
        }
        private BattleRecord GenerateBattleRecord(bool playerWon)
        {
            return new BattleRecord
            {
                level_id = levelId,
                battle_result = playerWon ? "Win" : "Lose",
                Enemy_count = Enemies.Count,
                enemy_list = ExtractEnemyListData(Enemies),
                player_performance = new PlayerPerformance
                {
                    player_hp_start = playerStartHP,
                    player_hp_end = playerEndHP,
                    enemy_total_hp_start = totalEnemyStartHP,
                    enemy_total_hp_end = totalEnemyEndHP,
                    battle_duration = GetBattleDuration(),
                    turn_count = turnCount,
                    damage_dealt = damageDealtTotal,
                    damage_taken = damageTakenTotal
                },
                player_behavior = new PlayerBehavior
                {
                    fist_used = fistUsedCount,
                    sword_used = swordUsedCount,
                    gun_used = gunUsedCount,
                    defend_used = defendUsedCount
                },
                turn_logs = ConvertEventsToTurnLog(),
                applied_difficulty_params = new AppliedDifficultyParams()
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

        private void ResetCounters()
        {
            turnCount = 0;
            gunUsedCount = 0;
            fistUsedCount = 0;
            swordUsedCount = 0;
            defendUsedCount = 0;
            damageDealtTotal = 0;
            damageTakenTotal = 0;
        }

        public void LogBattleEvent(BattleEvent battleEvent)
        {
     
            battleEvents.Add(battleEvent);
            
            if (Application.isEditor)
            {
                Debug.Log($"[BattleLogger] Turn {turnCount} | {battleEvent.eventType} | {battleEvent.description}");
            }
        }
        
        private List<TurnLog> ConvertEventsToTurnLog()
        {
            var list = new List<TurnLog>();

            foreach (var evt in battleEvents)
            {
                list.Add(new TurnLog
                {
                    turn = evt.turn,
                    actor = evt.eventType.ToString(),
                    target = evt.target,
                    action = evt.playerActionType.ToString(),
                    damage = evt.damage,  
                    targetHPAfter = evt.targetHPAfter,
                    targetHPBefore = evt.targetHPBefore,
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
                    hp_end = enemy.EnemyStats.Health 
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
            Debug.Log($"Duration: {GetBattleDuration():F2}s");
            Debug.Log($"Turns: {turnCount}");
            Debug.Log($"\nPlayer Stats:");
            Debug.Log($"  HP: {playerStartHP} → {playerEndHP}");
            Debug.Log($"  Damage Dealt: {damageDealtTotal}");
            Debug.Log($"  Damage Taken: {damageTakenTotal}");
            Debug.Log($"\nActions:");
            Debug.Log($"  Fist Used: {fistUsedCount}");
            Debug.Log($"  Sword Used: {swordUsedCount}");
            Debug.Log($"  Gun Used: {gunUsedCount}");
            Debug.Log($"  Defend Used: {defendUsedCount}");
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
        public int Enemy_count;
        public List<EnemyInfo> enemy_list;
        public PlayerPerformance player_performance;
        public PlayerBehavior player_behavior;
        public List<TurnLog> turn_logs;
        public AppliedDifficultyParams applied_difficulty_params;
    }

    [Serializable]
    public class EnemyInfo
    {
        public string enemy_id;
        public string enemy_type;
        public int hp_start;
        public int hp_end;
    }

    [Serializable]
    public class TurnLog
    {
        public int turn;
        public string actor;
        public string target;
        public string action;
        public int damage;
        public string description;
        public int targetHPBefore;
        public int targetHPAfter;
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
        public int player_hp_start;
        public int player_hp_end;
        public int enemy_total_hp_start;
        public int enemy_total_hp_end;
        public float battle_duration;
        public int turn_count;
        public int damage_dealt;
        public int damage_taken;
    }
    [Serializable]
    public class PlayerBehavior
    {
        public int fist_used;
        public int gun_used;
        public int sword_used;
        public int defend_used;
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