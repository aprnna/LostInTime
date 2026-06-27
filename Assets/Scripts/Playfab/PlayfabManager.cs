using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Playfab
{
    public class PlayfabManager : MonoBehaviour
    {
        public static PlayfabManager Instance { get; private set; }
        public bool IsLoggedIn { get; private set; }

        private const string PENDING_KEY = "pf_pending_events";
        private const int MAX_QUEUE = 200;
        private const int MAX_ATTEMPTS = 10;
        private const int BODY_SOFT_LIMIT = 24000; // ponytail: PlayStream custom event body has size cap; warn not drop

        private LoadingManager _loadingManager;
        [SerializeField] private string _id;

        private List<PendingEvent> _pending = new List<PendingEvent>();
        private bool _flushing;

        [Serializable]
        private class PendingEvent
        {
            public string eventName;
            public string bodyJson;
            public int attempts;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPending();
        }

        private void Start()
        {
            _loadingManager = LoadingManager.Instance;
            Login();
            InvokeRepeating(nameof(TryFlush), 5f, 30f);
        }

        // ---- Public API ----

        // Enqueue structured event (appends to local persistent queue, uploads async).
        public void EnqueueEvent(string eventName, object body)
        {
            if (string.IsNullOrEmpty(eventName) || body == null) return;

            string json = JsonConvert.SerializeObject(body);
            if (json.Length > BODY_SOFT_LIMIT)
                Debug.LogWarning($"[PlayfabManager] Event '{eventName}' body {json.Length} chars may exceed PlayStream cap; upload may reject.");

            var pe = new PendingEvent { eventName = eventName, bodyJson = json, attempts = 0 };
            _pending.Add(pe);
            if (_pending.Count > MAX_QUEUE) _pending.RemoveRange(0, _pending.Count - MAX_QUEUE); // bound localStorage
            SavePending();
            TryFlush();
        }

        // Kick flush if logged in and idle.
        public void TryFlush()
        {
            if (!IsLoggedIn || _flushing || _pending.Count == 0) return;
            _flushing = true;
            SendNext();
        }

        // ---- Login ----

        private void Login()
        {
            string customId = PlayerPrefs.HasKey("CustomID")
                ? PlayerPrefs.GetString("CustomID")
                : Guid.NewGuid().ToString();
            PlayerPrefs.SetString("CustomID", customId);

            PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = true },
                OnLoginSuccess, OnLoginFailure);
        }

        private void OnLoginFailure(PlayFabError error) => Debug.LogError("[PlayfabManager] Login failed: " + error.GenerateErrorReport());

        private void OnLoginSuccess(LoginResult result)
        {
            IsLoggedIn = true;
            _id = result.PlayFabId;
            Debug.Log("[PlayfabManager] Login success: " + _id);
            if (_loadingManager != null) _loadingManager.OnLoginComplete();
            TryFlush();
        }

        // ---- Send loop ----

        private void SendNext()
        {
            if (_pending.Count == 0) { _flushing = false; return; }
            var pe = _pending[0];

            Dictionary<string, object> body;
            try { body = ResolveBody(pe); }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayfabManager] Drop malformed event '{pe.eventName}': {e.Message}");
                _pending.RemoveAt(0); SavePending(); _flushing = false; TryFlush(); return;
            }

            PlayFabClientAPI.WritePlayerEvent(
                new WriteClientPlayerEventRequest { EventName = pe.eventName, Body = body },
                _ => OnSentSuccess(pe),
                err => OnSentFail(pe, err));
        }

        private void OnSentSuccess(PendingEvent pe)
        {
            _pending.Remove(pe);
            SavePending();
            if (_pending.Count > 0) SendNext();
            else _flushing = false;
        }

        private void OnSentFail(PendingEvent pe, PlayFabError err)
        {
            pe.attempts++;
            Debug.LogWarning($"[PlayfabManager] WritePlayerEvent '{pe.eventName}' fail (try {pe.attempts}): {err.ErrorMessage}");
            if (pe.attempts >= MAX_ATTEMPTS)
            {
                _pending.Remove(pe); // ponytail: drop poison event after N tries, keep queue draining
                Debug.LogError($"[PlayfabManager] Giving up on event '{pe.eventName}' after {MAX_ATTEMPTS} tries");
            }
            SavePending();
            _flushing = false; // retry on next TryFlush (timer / focus / next enqueue)
        }

        // ---- Persistence ----

        private void LoadPending()
        {
            string raw = PlayerPrefs.GetString(PENDING_KEY, "");
            if (string.IsNullOrEmpty(raw)) return;
            try { _pending = JsonConvert.DeserializeObject<List<PendingEvent>>(raw) ?? new List<PendingEvent>(); }
            catch (Exception e) { Debug.LogWarning($"[PlayfabManager] Reset corrupt queue: {e.Message}"); _pending = new List<PendingEvent>(); }
        }

        private void SavePending()
        {
            try { PlayerPrefs.SetString(PENDING_KEY, JsonConvert.SerializeObject(_pending)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning($"[PlayfabManager] Save queue failed: {e.Message}"); }
        }

        // ---- Rehydrate persisted JSON to PlayFab-serializable Dictionary<string,object> ----
        // PlayFab Body is Dictionary<string,object> and serialized by SimpleJson, which reflects dicts/primitives/lists.
        // Newtonsoft nests JObject/JArray/JValue which SimpleJson can't read, so normalize to plain dict/list/primitive.
        private static Dictionary<string, object> ResolveBody(PendingEvent pe)
        {
            if (string.IsNullOrEmpty(pe.bodyJson)) return new Dictionary<string, object>();
            var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(pe.bodyJson) ?? new Dictionary<string, object>();
            var clean = new Dictionary<string, object>(raw.Count);
            foreach (var kv in raw) clean[kv.Key] = Normalize(kv.Value);
            return clean;
        }

        // JObject -> Dictionary, JArray -> List, JValue -> primitive, else pass-through.
        private static object Normalize(object o)
        {
            if (o is JObject jo)
            {
                var d = new Dictionary<string, object>(jo.Count);
                foreach (var p in jo) d[p.Key] = Normalize(p.Value);
                return d;
            }
            if (o is JArray ja)
            {
                var l = new List<object>(ja.Count);
                foreach (var v in ja) l.Add(Normalize(v));
                return l;
            }
            if (o is JValue jv) return jv.Value;
            return o;
        }

        // ---- WebGL-friendly flush triggers ----
        // WebGL: tab close may not fire OnApplicationQuit reliably; OnApplicationFocus(false) is the real "user leaving" signal.
        private void OnApplicationFocus(bool focus) { if (!focus) TryFlush(); }
        private void OnApplicationPause(bool paused) { if (paused) TryFlush(); }
        private void OnApplicationQuit() { TryFlush(); }
    }
}