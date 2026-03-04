using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Player;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Playfab
{
    public class PlayfabManager: MonoBehaviour
    {
        public static PlayfabManager Instance {get ; private set; }
        [SerializeField] private string _id;
        private LoadingManager _loadingManager;
        private PlayerStats _playerStats;
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
            DontDestroyOnLoad(gameObject);
        }
        
        void Start()
        {
            _loadingManager = LoadingManager.Instance;
            Login();
        }
        
        void Login()
        {
            string customId;
            
            if (PlayerPrefs.HasKey("CustomID"))
            {
                customId = PlayerPrefs.GetString("CustomID");
            }
            else
            {
                customId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("CustomID", customId);
            }
        
            var request = new LoginWithCustomIDRequest()
            {
                CustomId = customId,
                CreateAccount = true
            };
        
            PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
        }
        
        private void OnLoginFailure(PlayFabError error)
        {
            Debug.LogWarning("Something went wrong with your first API call.  :(");
            Debug.LogError("Here's some debug information:");
            Debug.LogError(error.GenerateErrorReport());
        }

        private void OnLoginSuccess(LoginResult result)
        {
            Debug.Log("Congratulations, you successful login!");
            _id = result.PlayFabId;
        
            if (_loadingManager != null) _loadingManager.OnLoginComplete();
        
         
        }
        
        public void UploadSessionLogToPlayFab( long sessionStartTime,Action onDone = null)
        {
            BattleLog currentLog = BattleLogger.Instance.GetCurrentLog();
            string json = JsonConvert.SerializeObject(currentLog, Formatting.None);

            var request = new UpdateUserDataRequest()
            {
                Data = new Dictionary<string, string>()
                {
                    { $"BattleSessionLog_{sessionStartTime}", json }
                }
            };

            PlayFabClientAPI.UpdateUserData(request, result =>
                {
                    Debug.Log("[BattleLogManager] Session uploaded.");
                    onDone?.Invoke();
                },
                OnError);
        }
      
        void OnError(PlayFabError error)
        {
            Debug.LogError("PlayFab Error: " + error.GenerateErrorReport());
           
        }
        
    }
    
    
}