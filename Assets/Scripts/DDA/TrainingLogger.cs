using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Logs training events to a file with timestamps for debugging and analysis.
    /// Single file per training session, stored in PersistantDataPath.
    /// </summary>
    public static class TrainingLogger
    {
        private static string _logFilePath;
        private static StringBuilder _buffer = new StringBuilder();
        private static int _flushCounter = 0;
        private const int FLUSH_INTERVAL = 10; // Flush every N logs

        /// <summary>
        /// Initialize the logger. Call once at training start.
        /// </summary>
        public static void Initialize(string runId = null)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = string.IsNullOrEmpty(runId)
                ? $"training_{timestamp}.log"
                : $"training_{runId}_{timestamp}.log";

            _logFilePath = Path.Combine(Application.persistentDataPath, "DDA_Training", fileName);

            // Ensure directory exists
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write header
            string header = $"=== DDA Training Log ===\n" +
                           $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                           $"Log file: {_logFilePath}\n" +
                           $"==========================\n\n";

            File.WriteAllText(_logFilePath, header);
            _buffer.Clear();
            _flushCounter = 0;

            Debug.Log($"[TrainingLogger] Log file created: {_logFilePath}");
        }

        /// <summary>
        /// Log a run start event.
        /// </summary>
        public static void LogRunStart(int runNumber, int totalAreas, string difficulty)
        {
            WriteLog($"[RUN START] Run #{runNumber} | Areas: {totalAreas} | Difficulty: {difficulty}");
        }

        /// <summary>
        /// Log a run end event.
        /// </summary>
        public static void LogRunEnd(int runNumber, bool won, int areasCompleted, int totalAreas,
            float cumulativeReward, float runBonus)
        {
            WriteLog($"[RUN END] Run #{runNumber} | Won: {won} | Areas: {areasCompleted}/{totalAreas} | " +
                    $"CumulativeReward: {cumulativeReward:F3} | RunBonus: {runBonus:F2}");
        }

        /// <summary>
        /// Log area enter event.
        /// </summary>
        public static void LogAreaEnter(int areaIndex, MapType areaType, float hpMult, float dmgMult,
            string difficultyName)
        {
            WriteLog($"[AREA ENTER] Area {areaIndex} | Type: {areaType} | Difficulty: {difficultyName} | " +
                    $"HP Mult: {hpMult:F2} | DMG Mult: {dmgMult:F2}");
        }

        /// <summary>
        /// Log area complete event with reward.
        /// </summary>
        public static void LogAreaComplete(int areaIndex, bool won, int endHP, int startHP,
            float areaReward, float cumulativeReward, bool isFirstArea)
        {
            if (isFirstArea)
            {
                WriteLog($"[AREA COMPLETE] Area {areaIndex} (FIRST/BASELINE) | Won: {won} | " +
                        $"HP: {endHP}/{startHP} | Reward: SKIPPED (baseline)");
            }
            else
            {
                WriteLog($"[AREA COMPLETE] Area {areaIndex} | Won: {won} | HP: {endHP}/{startHP} | " +
                        $"AreaReward: {areaReward:F3} | Cumulative: {cumulativeReward:F3}");
            }
        }

        /// <summary>
        /// Log agent action (difficulty decision).
        /// </summary>
        public static void LogAgentAction(int action, string actionName, int prevDifficulty,
            int newDifficulty, string prevDiffName, string newDiffName)
        {
            WriteLog($"[AGENT ACTION] Action: {action} ({actionName}) | " +
                    $"Difficulty: {prevDiffName}({prevDifficulty}) -> {newDiffName}({newDifficulty})");
        }

        /// <summary>
        /// Log battle result.
        /// </summary>
        public static void LogBattleEnd(int areaIndex, bool won, int playerHP, int playerMaxHP,
            int turns, string enemyName)
        {
            float hpRatio = playerMaxHP > 0 ? (float)playerHP / playerMaxHP : 0f;
            WriteLog($"[BATTLE END] Area {areaIndex} | Enemy: {enemyName} | Won: {won} | " +
                    $"HP: {playerHP}/{playerMaxHP} ({hpRatio:P0}) | Turns: {turns}");
        }

        /// <summary>
        /// Log player state after area.
        /// </summary>
        public static void LogPlayerState(int hp, int maxHP, int level, int coin,
            int swordUses, int gunUses, int defendUses)
        {
            WriteLog($"[PLAYER STATE] HP: {hp}/{maxHP} | Level: {level} | Coin: {coin} | " +
                    $"Sword: {swordUses} | Gun: {gunUses} | Defend: {defendUses}");
        }

        /// <summary>
        /// Log rest area effect.
        /// </summary>
        public static void LogRestArea(int healAmount, int currentHP, int maxHP)
        {
            WriteLog($"[REST AREA] Healed: {healAmount} | HP: {currentHP}/{maxHP}");
        }

        /// <summary>
        /// Log shop area effect.
        /// </summary>
        public static void LogShopArea(int coin, int shield, int maxShield)
        {
            WriteLog($"[SHOP AREA] Coin: {coin} | Shield: {shield}/{maxShield}");
        }

        /// <summary>
        /// Log a generic message.
        /// </summary>
        public static void LogMessage(string message)
        {
            WriteLog($"[INFO] {message}");
        }

        /// <summary>
        /// Write a log entry with timestamp.
        /// </summary>
        private static void WriteLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logLine = $"[{timestamp}] {message}\n";

            _buffer.Append(logLine);
            _flushCounter++;

            // Periodic flush to disk
            if (_flushCounter >= FLUSH_INTERVAL)
            {
                Flush();
            }
        }

        /// <summary>
        /// Flush buffer to disk.
        /// </summary>
        public static void Flush()
        {
            if (_buffer.Length > 0 && !string.IsNullOrEmpty(_logFilePath))
            {
                File.AppendAllText(_logFilePath, _buffer.ToString());
                _buffer.Clear();
                _flushCounter = 0;
            }
        }

        /// <summary>
        /// Close the logger and flush remaining logs.
        /// </summary>
        public static void Close()
        {
            Flush();

            string footer = $"\n=== Training Log Closed ===\n" +
                           $"Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n";

            if (!string.IsNullOrEmpty(_logFilePath))
            {
                File.AppendAllText(_logFilePath, footer);
            }
        }

        /// <summary>
        /// Get the log file path.
        /// </summary>
        public static string GetLogFilePath() => _logFilePath;
    }
}
