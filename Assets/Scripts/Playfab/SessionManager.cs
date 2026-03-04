using System;
using UnityEngine;

namespace Playfab
{
    public class SessionManager: MonoBehaviour
    {
        public static SessionManager Instance;
        public string SessionId { get; private set; }
        public long SessionStartTime { get; private set; }

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
            }
        }

        private void Start()
        {
            StartNewSession();

        }

        public void StartNewSession()
        {
            SessionId = Guid.NewGuid().ToString();
            SessionStartTime = DateTime.UtcNow.Ticks;
            BattleLogger.Instance.CreateNewLog(SessionId);

            Debug.Log($"[SessionManager] New session started: {SessionId}");
        }

        public void EndSessionAndSend()
        {
            PlayfabManager.Instance.UploadSessionLogToPlayFab(SessionStartTime,() =>
            {
                Debug.Log("[SessionManager] Session uploaded.");
            });
        }

        private void OnApplicationQuit()
        {
            EndSessionAndSend();
        }
    }
}