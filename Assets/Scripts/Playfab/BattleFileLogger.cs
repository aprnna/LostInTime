using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Playfab
{
    /// <summary>
    /// Writes battle events to a JSON Lines (.jsonl) file as a parallel sink to PlayFab upload.
    /// One file per session: {persistentDataPath}/Battle_Logs/battle_{sessionId}.jsonl
    /// Each line = one JSON envelope { ts, session_id, event_type, payload }.
    /// Flushed every event (no buffering) so a crash mid-session loses at most the last line.
    /// Thread-safe via lock; only active in the live game (training skips BattleLogger wiring).
    /// </summary>
    public static class BattleFileLogger
    {
        private static string _logFilePath;
        private static string _sessionId;
        private static bool _initialized;
        private static readonly object _lock = new object();

        public static bool IsInitialized => _initialized;
        public static string LogFilePath => _logFilePath;

        /// <summary>
        /// Create (or reopen) the .jsonl file for this session. Idempotent for the same sessionId.
        /// </summary>
        public static void Initialize(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            lock (_lock)
            {
                // Already bound to this exact session -> no-op
                if (_initialized && _sessionId == sessionId) return;

                string dir = Path.Combine(Application.persistentDataPath, "Battle_Logs");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); }
                    catch (Exception e) { Debug.LogWarning($"[BattleFileLogger] mkdir fail: {e.Message}"); return; }
                }

                _logFilePath = Path.Combine(dir, $"battle_{sessionId}.jsonl");
                _sessionId = sessionId;

                bool newFile = !File.Exists(_logFilePath);
                if (newFile)
                {
                    string header = $"# battle_log session={sessionId} started={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n";
                    try { File.WriteAllText(_logFilePath, header); }
                    catch (Exception e) { Debug.LogWarning($"[BattleFileLogger] header write fail: {e.Message}"); return; }
                }

                _initialized = true;
                Debug.Log($"[BattleFileLogger] {(newFile ? "Created" : "Reopened")} file: {_logFilePath}");
            }
        }

        /// <summary>
        /// Serialize one event as a JSON line and append it to the file. Flush happens immediately.
        /// </summary>
        public static void WriteEvent(string eventType, object payload)
        {
            if (!_initialized || string.IsNullOrEmpty(_logFilePath)) return;
            if (string.IsNullOrEmpty(eventType)) return;

            var envelope = new FileLogEntry
            {
                ts = DateTime.UtcNow.ToString("o"),
                session_id = _sessionId,
                event_type = eventType,
                payload = payload
            };

            string line;
            try
            {
                line = JsonConvert.SerializeObject(envelope, Formatting.None,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BattleFileLogger] serialize fail ({eventType}): {e.Message}");
                return;
            }

            lock (_lock)
            {
                try { File.AppendAllText(_logFilePath, line + "\n"); }
                catch (Exception e) { Debug.LogWarning($"[BattleFileLogger] write fail ({eventType}): {e.Message}"); }
            }
        }

        /// <summary>
        /// Emit a session_end marker and detach the active file. Safe to re-Initialize after.
        /// </summary>
        public static void Close()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    WriteEvent("session_end", new { ended_at = DateTime.UtcNow.ToString("o") });
                }
                _initialized = false;
                _logFilePath = null;
                _sessionId = null;
            }
        }
    }

    [Serializable]
    internal struct FileLogEntry
    {
        public string ts;
        public string session_id;
        public string event_type;
        public object payload;
    }
}