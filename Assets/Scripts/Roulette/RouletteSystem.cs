using System;
using Audio;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace Roulette
{
    public class RouletteSystem:MonoBehaviour
    {
        public static RouletteSystem Instance { get; private set; }

        [Header("Roulette")] 
        [field:SerializeField] public Transform RouletteContainer { get; private set; }
        [field:SerializeField] public GameObject PrefabRoulette { get; private set; }
        [field:SerializeField] public GameObject ButtonStopRoulette{ get; private set; }
        [field:SerializeField] public GameObject ButtonStartRoulette { get; private set; }
        [field:SerializeField] public GameObject CheckBoxAutoStart { get; private set; }
        public bool AutoStartRoulette { get; private set; }
        private GameManager _gameManager;
        private AudioManager _audioManager;
        private Toggle _uiToggle;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _gameManager = GameManager.Instance;
            _audioManager = AudioManager.Instance;
            _uiToggle= CheckBoxAutoStart.GetComponent<Toggle>();
            ClearButtonRoulette();
        }

        public async UniTask<int> SetRoulette(int min, int max)
        {
            var rouletteObject = SpawnRoulette();
            ButtonStopRoulette.SetActive(true);
            int result = await PlayRoulette(rouletteObject, min, max, AutoStartRoulette);
            Destroy(rouletteObject);
            ClearButtonRoulette();
            return result;
        }
        public async UniTask<(GameObject rouletteObject, int result)> SetRoulette(int min, int max, bool auto)
        {
            var rouletteObject = SpawnRoulette();
            int result = await PlayRoulette(rouletteObject, min, max, auto);
            return (rouletteObject, result);
        }
        private GameObject SpawnRoulette()
        {
            return Instantiate(PrefabRoulette, RouletteContainer);
        }

        public void EnableRouletteAction()
        {
            ClearButtonRoulette();
            if (AutoStartRoulette){
                ButtonStopRoulette.SetActive(true);
                ButtonStartRoulette.SetActive(false);
            }
            else
            {
                ButtonStartRoulette.SetActive(true);
                ButtonStopRoulette.SetActive(false);
            }
            
            CheckBoxAutoStart.SetActive(true);
        }
        private async UniTask<int> PlayRoulette(GameObject rouletteObject,int min, int max, bool autoStart)
        {
            var resultRoulette = 0;
            var isRouletteStop = false;
            _audioManager.PlaySound(SoundType.SFX_Roulette);
            RouletteUIController rouletteUI = rouletteObject.GetComponent<RouletteUIController>();

            rouletteUI.minValue = min;
            rouletteUI.maxValue = max;
            rouletteUI.autoStop = autoStart;

            rouletteUI.onRouletteStopped = (result) =>
            {
                Debug.Log($"The roulette has stopped! The result is: {result}");
                isRouletteStop = true;
                _audioManager.StopSound(SoundType.SFX_Roulette);
                resultRoulette = result;
            };
            await UniTask.WaitUntil(() => isRouletteStop);
            await UniTask.Delay(TimeSpan.FromSeconds(2), ignoreTimeScale: false);
            return resultRoulette;
        }

        public void SetRouletteButton(Action callback)
        {
            _uiToggle.isOn = AutoStartRoulette;
            _uiToggle.onValueChanged.RemoveAllListeners();
            _uiToggle.onValueChanged.AddListener((bool isOn) =>
            {
                AutoStartRoulette = isOn;
            });
            
            Button button = ButtonStartRoulette.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }
        private void ClearButtonRoulette()
        {
            ButtonStopRoulette.SetActive(false);
            ButtonStartRoulette.SetActive(false);
            CheckBoxAutoStart.SetActive(false);
        }
    }
}