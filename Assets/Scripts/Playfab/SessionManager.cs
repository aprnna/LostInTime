using System;
using UnityEngine;

namespace Playfab
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance;
        public string SessionId { get; private set; }
        public long SessionStartTime { get; private set; }

        private int _lastSentIndex; // tracks how many BattleRecords already enqueued for upload

        private void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(gameObject); }
        }

        private void Start() { StartNewSession(); }

        public void StartNewSession()
        {
            SessionId = Guid.NewGuid().ToString();
            SessionStartTime = DateTime.UtcNow.Ticks;
            _lastSentIndex = 0;

            BattleLogger.Instance?.CreateNewLog(SessionId);
            PlayfabManager.Instance?.EnqueueEvent("session_started", new { session_id = SessionId, started_at = DateTime.UtcNow.ToString("o") });

            Debug.Log($"[SessionManager] New session: {SessionId}");
        }

        // Enqueue any BattleRecords not yet queued, then flush.
        // Called per battle end (BattleSystem.OnBattleEnd) + on focus loss / quit.
        public void EndSessionAndSend()
        {
            var mgr = PlayfabManager.Instance;
            var logger = BattleLogger.Instance;
            if (mgr == null || logger == null) return;

            var log = logger.GetCurrentLog();
            if (log == null) return;

            var records = log.Battle_Record;
            for (int i = _lastSentIndex; i < records.Count; i++)
                mgr.EnqueueEvent("battle_completed", records[i]);
            _lastSentIndex = records.Count;

            mgr.TryFlush();
        }

        // WebGL: tab hidden -> best-effort enqueue+flush before user leaves.
        private void OnApplicationFocus(bool focus) { if (!focus) EndSessionAndSend(); }
        private void OnApplicationQuit() { EndSessionAndSend(); }
    }
}