using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Playfab
{
    public class LoadingManager: MonoBehaviour
    {
        public static LoadingManager Instance;

        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Slider loadingBar;
        [SerializeField] private TMP_Text loadingText;
        
        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        
        private bool isDataLoaded = false;
        private bool isNewPlayer = false;

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

        private void Start()
        {
            ShowLoading();
        }

        public void ShowLoading()
        {
            loadingPanel.SetActive(true);
            UpdateLoadingProgress(0f, "Connecting to server...");
        }

        public void HideLoading()
        {
            loadingPanel.SetActive(false);
        }

        public void UpdateLoadingProgress(float progress, string message)
        {
            if (loadingBar != null)
            {
                loadingBar.value = progress;
            }
            
            if (loadingText != null)
            {
                loadingText.text = message;
            }
        }

        public void OnLoginComplete()
        {
            UpdateLoadingProgress(0.3f, "Loading player data...");
            OnDataLoadComplete(true);
        }

        public void OnDataLoadComplete(bool isNew)
        {
            isNewPlayer = isNew;
            isDataLoaded = true;
            UpdateLoadingProgress(0.7f, "Preparing game...");
            StartCoroutine(LoadNextScene());
        }

        private IEnumerator LoadNextScene()
        {
            yield return new WaitForSeconds(0.5f);
            
            UpdateLoadingProgress(1f, "Ready!");
            yield return new WaitForSeconds(0.3f);

            AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(mainMenuScene);
            
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            // HideLoading();
        }
    }
}