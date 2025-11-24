using System;
using Cysharp.Threading.Tasks;
using Input;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Minigames
{
    public class MinigameManager: MonoBehaviour
    {
        public static MinigameManager Instance { get; private set; }
        private Minigame _currentMinigame;

        [SerializeField] private TapZone _tapZone;
        [SerializeField, Range(0, 11)] float _difficulty;
        [SerializeField] private TMP_Text _difficultyText;
        [SerializeField] private TMP_Text _statusText;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
            }
        }

        private async void Start()
        {
            _difficultyText.SetText(_difficulty.ToString());
            await InitMinigames();
        }

        public void SetDifficulty(float f)
        {
            _difficulty = f;
            _difficultyText.SetText(f.ToString());
        }

        private async UniTask InitMinigames()
        {
            await UniTask.WhenAll(_tapZone.Init()
            );

        }
        
        [Button]
        public UniTask<Minigame.Result> PlayTapZone()
        {
            InputManager.Instance.MinigamesMode();
            _currentMinigame?.Cancel();
            _currentMinigame = _tapZone;
            var res = PlayGame();
            return res;
        }
        
        async UniTask<Minigame.Result> PlayGame()
        {
            try
            {
                Debug.Log("Play Game");
                var res = await _currentMinigame.Play(_difficulty);
                Debug.Log("Done Play: " + res);
                _statusText.SetText(res.ToString()); 
                StopGame();
                _statusText.SetText("");
                return res;
            } 
            catch (OperationCanceledException)
            {
                Debug.Log("Canceled");
                StopGame();
                return Minigame.Result.Cancelled;
            }
            
        }

        [Button]
        public void StopGame()
        {
            _currentMinigame.Cancel();
        }
    }
}