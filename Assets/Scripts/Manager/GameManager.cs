using System;
using Audio;
using Cysharp.Threading.Tasks;
using Input;
using Player;
using Player.Item;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class GameManager:PersistentSingleton<GameManager>
    {
        private AudioManager _audioManager;
        private PlayerStats _playerStats;
        private InputManager _inputManager;
        private MapSystem _mapSystem;
        private SceneController _sceneController;
        public static string SessionID { get; private set; }
        public int ProgressTeleport { get; private set; }
        public event Action<string> OnChangeInstruction;
        public event Action<BattleResult> OnBattleEnd;
        public event Action<bool> OnChangeDungeon;
        public event Action OnChangeBiome;
        public event Action OnIncreaseProgress;
        [field: SerializeField] public Biome[] ListBiome { get; private set; }
        public GameData GameData {get; private set;}
        public Biome ActiveBiome => ListBiome[_activeIndexBiome];
        private int _activeIndexBiome=0;
        private Transform[] _enemiesPosition;

        protected override void Awake()
        {
            base.Awake();
            StartNewSession();
        }

        private void Start()
        {
            PrepareGame().Forget();
        }

        private async UniTask PrepareGame()
        {
            await Initialize();
            await InitializeActions();
            await InitializePlayerStats();
        }
        private async UniTask Initialize()
        {
            _inputManager = InputManager.Instance;
            _playerStats = PlayerStats.Instance;
            _sceneController = GetComponent<SceneController>();
            GameData = Resources.Load<GameData>("GameData");

            await UniTask.Yield();
        }

        public async UniTask InitializeActions()
        {
            var actions = Resources.LoadAll<BaseAction>("Player/Actions");
            foreach (var item in actions)
            {
                item.Initialize(_playerStats);
                item.ResetLimit();
            }
            await UniTask.Yield();
        }
        public async UniTask InitializePlayerStats()
        {
            _playerStats.InitializeStats(
                "Kamikaze",
                100,
                100, 
                2, 
                2, 
                12,
                0,
                100,
                0,
                3,
                2,
                2);
            await UniTask.Yield();
        }
        private void OnEnable()
        {
            InputManager.Instance.PlayerInput.Pause.OnDown += PauseGame;
        }

        private void OnDisable()
        {
            InputManager.Instance.PlayerInput.Pause.OnDown -= PauseGame;
        }

        public void ChangeInstruction(string text) => OnChangeInstruction?.Invoke(text);
        public void BattleResult(BattleResult result) => OnBattleEnd?.Invoke(result);
        public void ChangeDungeon(bool value) => OnChangeDungeon?.Invoke(value);
        public void IncreaseProgress(int value)
        {
            ProgressTeleport = Mathf.Clamp(ProgressTeleport + value, 0, 100);
            OnIncreaseProgress?.Invoke();
        }
        public void NextBiome()
        {
            
            _activeIndexBiome = (_activeIndexBiome + 1) % ListBiome.Length;
            OnChangeBiome?.Invoke();
        }
        
        public void SetEnemyPosition(Transform[] transforms)
        {
            _enemiesPosition = transforms;
        }

        public Transform[] GetEnemiesPosition()
        {
            return _enemiesPosition;
        }
        public void StartGame()
        {
            _sceneController.ChangeScene("BaseScene");
        }
        public void StartPrologue()
        {
            _sceneController.ChangeScene("Prolog");
        }
        public void RestartGame()
        {
            OnRestartGame().Forget();
        }
        
        public async UniTask OnRestartGame()
        {
            PrepareGame().Forget();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            await _sceneController.ClearAllScene();
            StartGame();
            MapSystem.Instance.InitializeMap();
        }
        public void QuitGame()
        {
            Application.Quit();
        }
        public void MainMenu()
        {
            _sceneController.ChangeScene("MainMenu");
        }
        public void ResumeGame()
        {
            Time.timeScale = 1f; 
        }
        
        public void PauseGame()
        {
            Time.timeScale = 0f; 
        }

        public static void StartNewSession()
        {
            SessionID = Guid.NewGuid().ToString();
            Debug.Log("SESSION ID: " + SessionID);
        }
    }
}