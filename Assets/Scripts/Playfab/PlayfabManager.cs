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
        public static PlayfabManager Instance;
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
            var request = new LoginWithCustomIDRequest()
            {
                CustomId = SystemInfo.deviceUniqueIdentifier,
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
        public void SendBattleLog(BattleLog log)
        {
            string json = JsonConvert.SerializeObject(log, Formatting.None);

            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { "BattleLog_" + DateTime.UtcNow.Ticks, json }
                }
            };

            PlayFabClientAPI.UpdateUserData(request,
                (r) => Debug.Log("Battle log sent!"),
                (e) => Debug.LogError(e.GenerateErrorReport()));
        }

        private void SaveDataToPlayfab()
        {
            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { "PlayerData", JsonConvert.SerializeObject(new PlayerDataContainer
                        {
                            health = _playerStats.Health
                        })
                    }
                }
            };
            PlayFabClientAPI.UpdateUserData(request, OnDataSendSuccess, OnError);
        }
      
      
        void OnDataSendSuccess(UpdateUserDataResult result)
        {
            Debug.Log("Data successfully sent to PlayFab.");
        }
        
        
        void OnError(PlayFabError error)
        {
            Debug.LogError("PlayFab Error: " + error.GenerateErrorReport());
            
            if (_loadingManager != null ) 
            {
                _loadingManager.UpdateLoadingProgress(0f, "Error. Please check connection.");
            }
        }
        
    }
    
    
}