using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Playfab
{
    public class BattleLogger : MonoBehaviour
    {
        public static BattleLogger Instance { get; private set; }

        [Header("Battle Tracking")]
        [SerializeField] private DateTime battleStartTime;
        [SerializeField] private DateTime battleEndTime;
        [SerializeField] private int playerStartHP;
        [SerializeField] private int playerEndHP;
        [SerializeField] private int totalEnemyStartHP;
        [SerializeField] private int totalEnemyEndHP;
        [SerializeField] private int turnCount;
        
        [Header("Player Actions Tracking")]
        [SerializeField] private int actionCount;
        [SerializeField] private List<string> actionsUsed = new List<string>();
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

        #region Battle Lifecycle

        public void StartBattle(string sessionId, string levelId, int playerHP, List<int> enemyHPs)
        {
            this.sessionId = sessionId;
            this.levelId = levelId;
            
            battleStartTime = DateTime.UtcNow;
            playerStartHP = playerHP;
            playerEndHP = playerHP;
            
            totalEnemyStartHP = 0;
            foreach (int hp in enemyHPs)
            {
                totalEnemyStartHP += hp;
            }
            totalEnemyEndHP = totalEnemyStartHP;
            
            ResetCounters();
            battleEvents.Clear();
            
            LogEvent(BattleEventType.BattleStart, $"Battle started with {playerHP} HP vs {enemyHPs.Count} enemies");
            
            Debug.Log($"[BattleLogger] Battle started - Session: {sessionId}, Level: {levelId}");
        }

        public void EndBattle(int playerFinalHP, int totalEnemyFinalHP, bool playerWon)
        {
            battleEndTime = DateTime.UtcNow;
            playerEndHP = playerFinalHP;
            totalEnemyEndHP = totalEnemyFinalHP;
            
            string result = playerWon ? "Victory" : "Defeat";
            LogEvent(BattleEventType.BattleEnd, $"Battle ended - {result}");
            
            Debug.Log($"[BattleLogger] Battle ended - Duration: {GetBattleDuration():F2}s, Turns: {turnCount}, Result: {result}");
        }

        #endregion

        #region Turn Tracking

        public void OnPlayerTurnStart()
        {
            turnCount++;
            LogEvent(BattleEventType.TurnStart, $"Turn {turnCount} - Player");
        }

      
        public void OnEnemyTurnStart(string enemyName)
        {
            LogEvent(BattleEventType.TurnStart, $"Turn {turnCount} - {enemyName}");
        }

        #endregion

        #region Action Tracking

        public void OnPlayerAttack(string targetName, int damage, bool isCritical = false)
        {
            damageDealtTotal += damage;
            
            string critText = isCritical ? " (CRITICAL!)" : "";
            LogEvent(BattleEventType.PlayerAttack, $"Player attacked {targetName} for {damage} damage{critText}");
        }
        
        public void OnPlayerAction(string action,string targetName, int damage)
        {
            actionCount++;
            damageDealtTotal += damage;
            actionsUsed.Add(action);
            
            LogEvent(BattleEventType.PlayerAction, $"Player used Action on {targetName} for {damage} damage");
        }

        public void OnPlayerFistAction(string targetName, int damage)
        {
            fistUsedCount++;
            damageDealtTotal += damage;
            
            LogEvent(BattleEventType.PlayerFist, $"Player used Fist Action on {targetName} for {damage} damage");
        }
        public void OnPlayerSwordAction(string targetName, int damage)
        {
            swordUsedCount++;
            damageDealtTotal += damage;
            
            LogEvent(BattleEventType.PlayerSword, $"Player used Sword Action on {targetName} for {damage} damage");
        }

        public void OnPlayerDefendAction(string action,int defenseValue)
        {
            defendUsedCount++;
            actionsUsed.Add(action);

            LogEvent(BattleEventType.PlayerDefend, $"Player defended (+{defenseValue} defense)");
        }
        public void OnPlayerGunAction(string targetName, int damage)
        {
            gunUsedCount++;
            damageDealtTotal += damage;

            LogEvent(BattleEventType.PlayerGun, $"Player used Gun Action on {targetName} for {damage} damage");

        }

        public void OnEnemyAttack(string enemyName, int damage)
        {
            damageTakenTotal += damage;
            LogEvent(BattleEventType.EnemyAttack, $"{enemyName} attacked player for {damage} damage");
        }

        public void OnEnemyDefeated(string enemyName)
        {
            LogEvent(BattleEventType.EnemyDefeated, $"{enemyName} was defeated");
        }

        public void OnPlayerDeath()
        {
            LogEvent(BattleEventType.PlayerDeath, "Player was defeated");
        }

        #endregion

        #region Minigame Tracking

        public void OnMinigameCompleted(string minigameName, bool success, float difficulty)
        {
            string result = success ? "Success" : "Failed";
            LogEvent(BattleEventType.MinigameResult, $"Minigame '{minigameName}' - {result} (Difficulty: {difficulty:F1})");
        }

        public void OnRouletteResult(int damageMultiplier)
        {
            LogEvent(BattleEventType.RouletteResult, $"Roulette result: {damageMultiplier}x damage multiplier");
        }

        #endregion

        #region Data Generation

        public BattleLog GenerateBattleLog(bool playerWon)
        {
            float duration = GetBattleDuration();
            
            BattleLog log = new BattleLog
            {
                session_id = sessionId,
                device_id = SystemInfo.deviceUniqueIdentifier,
                level_id = levelId,
                timestamp = battleStartTime.ToString("o"),
                
                player_performance = new PlayerPerformance
                {
                    player_hp_start = playerStartHP,
                    player_hp_end = playerEndHP,
                    enemy_hp_start = totalEnemyStartHP,
                    enemy_hp_end = totalEnemyEndHP,
                    battle_duration = duration,
                    turn_count = turnCount,
                    player_death_count = playerWon ? 0 : 1,
                    win = playerWon ? 1 : 0,
                    damage_dealt = damageDealtTotal,
                    damage_taken = damageTakenTotal
                },
                
                player_behavior = new PlayerBehavior
                {
                    fist_used = fistUsedCount,
                    sword_used = swordUsedCount,
                    gun_used = gunUsedCount,
                    defend_used = defendUsedCount
                }
            };
            
            return log;
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

        private void LogEvent(BattleEventType eventType, string description)
        {
            BattleEvent battleEvent = new BattleEvent
            {
                eventType = eventType,
                timestamp = DateTime.UtcNow,
                turn = turnCount,
                description = description
            };
            
            battleEvents.Add(battleEvent);
            
            if (Application.isEditor)
            {
                Debug.Log($"[BattleLogger] Turn {turnCount} | {eventType} | {description}");
            }
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
        public string device_id;
        public string level_id;
        public string timestamp;

        public PlayerPerformance player_performance;
        public PlayerBehavior player_behavior;
        // public DDARuntime dda_runtime;
    }

    [Serializable]
    public class PlayerPerformance
    {
        public int player_hp_start;
        public int player_hp_end;
        public int enemy_hp_start;
        public int enemy_hp_end;
        public float battle_duration;
        public int turn_count;
        public int player_death_count;
        public int win;
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
    public class DDARuntime
    {
        public int difficulty_level;
        public float enemy_hp_multiplier;
        public float enemy_damage_multiplier;
        public float item_drop_rate_multiplier;

        public int dda_action;
        public float reward;

        public List<int> state;
        public List<float> q_values_before;
        public List<float> q_values_after;
    }
    [Serializable]
    public class BattleEvent
    {
        public BattleEventType eventType;
        public DateTime timestamp;
        public int turn;
        public string description;
    }

    public enum BattleEventType
    {
        BattleStart,
        BattleEnd,
        TurnStart,
        PlayerAttack,
        PlayerAction,
        PlayerFist,
        PlayerSword,
        PlayerDefend,
        PlayerGun,
        EnemyAttack,
        EnemyDefeated,
        PlayerDeath,
        MinigameResult,
        RouletteResult
    }

}