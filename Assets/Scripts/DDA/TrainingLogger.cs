using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Logs training events to a file with timestamps for debugging and analysis.
    /// Single file per training session, stored in PersistantDataPath.
    /// Thread-safe for multi-environment training (num_envs > 1).
    /// </summary>
    public static class TrainingLogger
    {
        private static string _logFilePath;
        private static StringBuilder _buffer = new StringBuilder();
        private static int _flushCounter = 0;
        private const int FLUSH_INTERVAL = 10; // Flush every N logs
        private static readonly object _lock = new object(); // Thread safety
        private static bool _initialized = false; // Prevent multiple Initialize calls
        private static int _primaryEnvId = -1; // Track which env initialized first

        /// <summary>
        /// Initialize the logger. Thread-safe: only first call creates file.
        /// Subsequent calls from other environments are ignored.
        /// </summary>
        /// <param name="runId">Optional run identifier</param>
        /// <param name="envId">Environment ID (default 0 for single-env)</param>
        public static void Initialize(string runId = null, int envId = 0)
        {
            lock (_lock)
            {
                // Only initialize once across all environments
                if (_initialized)
                {
                    Debug.Log($"[TrainingLogger] Env {envId}: Logger already initialized by Env {_primaryEnvId}, skipping");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = string.IsNullOrEmpty(runId)
                    ? $"training_{timestamp}.log"
                    : $"training_{runId}.log";

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
                               $"Multi-env: num_envs > 1 may interleave logs\n" +
                               $"==========================\n\n";

                File.WriteAllText(_logFilePath, header);
                _buffer.Clear();
                _flushCounter = 0;
                _initialized = true;
                _primaryEnvId = envId;

                Debug.Log($"[TrainingLogger] Log file created by Env {envId}: {_logFilePath}");
            }
        }

        /// <summary>
        /// Log a run start event.
        /// </summary>
        public static void LogRunStart(int runNumber, int totalAreas, string difficulty, int envId = 0)
        {
            WriteLog($"[RUN START] Run #{runNumber} | Areas: {totalAreas} | Difficulty: {difficulty}", envId);
        }

        /// <summary>
        /// Log a run end event.
        /// </summary>
        public static void LogRunEnd(int runNumber, bool won, int areasCompleted, int totalAreas,
            float cumulativeReward, float runBonus, int envId = 0)
        {
            WriteLog($"[RUN END] Run #{runNumber} | Won: {won} | Areas: {areasCompleted}/{totalAreas} | " +
                    $"CumulativeReward: {cumulativeReward:F3} | RunBonus: {runBonus:F2}", envId);
        }

        /// <summary>
        /// Log area enter event.
        /// </summary>
        public static void LogAreaEnter(int areaIndex, MapType areaType, float hpMult, float dmgMult,
            string difficultyName, int envId = 0)
        {
            WriteLog($"[AREA ENTER] Area {areaIndex} | Type: {areaType} | Difficulty: {difficultyName} | " +
                    $"HP Mult: {hpMult:F2} | DMG Mult: {dmgMult:F2}", envId);
        }

        /// <summary>
        /// Log area complete event with reward.
        /// </summary>
        public static void LogAreaComplete(int areaIndex, bool won, int endHP, int startHP,
            float areaReward, float cumulativeReward, float progressWeight = 0f, int envId = 0)
        {
            WriteLog($"[AREA COMPLETE] Area {areaIndex} | Won: {won} | HP: {endHP}/{startHP} | " +
                    $"AreaReward: {areaReward:F3} | ProgressWeight: {progressWeight:F2} | Cumulative: {cumulativeReward:F3}", envId);
        }

        /// <summary>
        /// Log agent action (difficulty decision).
        /// </summary>
        public static void LogAgentAction(int action, string actionName, int prevDifficulty,
            int newDifficulty, string prevDiffName, string newDiffName, int envId = 0)
        {
            WriteLog($"[AGENT ACTION] Action: {action} ({actionName}) | " +
                    $"Difficulty: {prevDiffName}({prevDifficulty}) -> {newDiffName}({newDifficulty})", envId);
        }

        /// <summary>
        /// Log battle result.
        /// </summary>
        public static void LogBattleEnd(int areaIndex, bool won, int playerHP, int playerMaxHP,
            int turns, string enemyName, int envId = 0)
        {
            float hpRatio = playerMaxHP > 0 ? (float)playerHP / playerMaxHP : 0f;
            WriteLog($"[BATTLE END] Area {areaIndex} | Enemy: {enemyName} | Won: {won} | " +
                    $"HP: {playerHP}/{playerMaxHP} ({hpRatio:P0}) | Turns: {turns}", envId);
        }

        /// <summary>
        /// Log player state after area.
        /// </summary>
        public static void LogPlayerState(int hp, int maxHP, int level, int coin,
            int swordUses, int gunUses, int defendUses, int envId = 0)
        {
            WriteLog($"[PLAYER STATE] HP: {hp}/{maxHP} | Level: {level} | Coin: {coin} | " +
                    $"Sword: {swordUses} | Gun: {gunUses} | Defend: {defendUses}", envId);
        }

        /// <summary>
        /// Log rest area effect.
        /// </summary>
        public static void LogRestArea(int healAmount, int currentHP, int maxHP, int envId = 0)
        {
            WriteLog($"[REST AREA] Healed: {healAmount} | HP: {currentHP}/{maxHP}", envId);
        }

        /// <summary>
        /// Log shop area effect.
        /// </summary>
        public static void LogShopArea(int coin, int shield, int maxShield, int envId = 0)
        {
            WriteLog($"[SHOP AREA] Coin: {coin} | Shield: {shield}/{maxShield}", envId);
        }

        /// <summary>
        /// Log a generic message.
        /// </summary>
        public static void LogMessage(string message, int envId = 0)
        {
            WriteLog($"[INFO] {message}", envId);
        }

        /// <summary>
        /// Log the generated map path for this run.
        /// </summary>
        public static void LogMapPath(int runNumber, System.Collections.Generic.List<SimArea> areas, int envId = 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[MAP PATH] Run #{runNumber} | Path: ");

            for (int i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                string enemyList = area.Enemies.Count > 0
                    ? string.Join("+", area.Enemies.ConvertAll(e => e.Name))
                    : "None";

                sb.Append($"[{i}:{area.AreaType}");
                if (area.AreaType == MapType.Enemy || area.AreaType == MapType.Boss)
                {
                    sb.Append($":{enemyList}");
                }
                sb.Append("]");

                if (i < areas.Count - 1) sb.Append(" -> ");
            }

            WriteLog(sb.ToString(), envId);
        }

        /// <summary>
        /// Write a log entry with timestamp. Thread-safe.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="envId">Environment ID (default 0 for single-env)</param>
        private static void WriteLog(string message, int envId = 0)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string envPrefix = envId >= 0 ? $"[Env{envId}] " : "";
            string logLine = $"[{timestamp}] {envPrefix}{message}\n";

            lock (_lock)
            {
                _buffer.Append(logLine);
                _flushCounter++;

                // Periodic flush to disk
                if (_flushCounter >= FLUSH_INTERVAL)
                {
                    FlushInternal();
                }
            }
        }

        /// <summary>
        /// Flush buffer to disk. Thread-safe.
        /// </summary>
        public static void Flush()
        {
            lock (_lock)
            {
                FlushInternal();
            }
        }

        /// <summary>
        /// Internal flush without lock (caller must hold lock).
        /// </summary>
        private static void FlushInternal()
        {
            if (_buffer.Length > 0 && !string.IsNullOrEmpty(_logFilePath))
            {
                File.AppendAllText(_logFilePath, _buffer.ToString());
                _buffer.Clear();
                _flushCounter = 0;
            }
        }

        /// <summary>
        /// Close the logger and flush remaining logs. Thread-safe.
        /// </summary>
        public static void Close()
        {
            lock (_lock)
            {
                FlushInternal();

                string footer = $"\n=== Training Log Closed ===\n" +
                               $"Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n";

                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    File.AppendAllText(_logFilePath, footer);
                }

                _initialized = false;
                _primaryEnvId = -1;
            }
        }

        /// <summary>
        /// Get the log file path.
        /// </summary>
        public static string GetLogFilePath() => _logFilePath;
    }
}
