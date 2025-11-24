using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class SceneController:MonoBehaviour
    {
        private bool _isPlaying;

        public void ChangeScene(string scene)
        {
            SceneManager.LoadScene(scene);
        }
        
        public  async UniTask ClearAllScenesAsync()
        {
            int sceneCount = SceneManager.sceneCount;
            
            for (int i = sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                Debug.Log(scene.name);
                if (scene.isLoaded && scene.name != "DontDestroyOnLoad")
                {
                    await SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        public async UniTask ClearAllScene()
        {
            await ClearAllScenesAsync();
        }

        public void ChangeSceneWithSound(string scene)
        {
            if (!_isPlaying)
            {
                StartCoroutine(PlayAudioAndChangeScene(scene));
            }
        }
        private IEnumerator PlayAudioAndChangeScene(string scene)
        {
            _isPlaying = true;
            var audioSource = gameObject.GetComponent<AudioSource>();
            audioSource.Play();
            yield return new WaitForSeconds(audioSource.clip.length);

            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
        public void QuitGame()
        {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        }

    }
}