using System;
using Audio;
using Input;
using Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class GameManager:PersistentSingleton<GameManager>
    {
        private AudioManager _audioManager;
        private PlayerStats _playerStats;
        private InputManager _inputManager;
        private MapSystem _mapSystem;
        public int ProgressTeleport { get; private set; }
        public event Action<string> OnChangeInstruction;
        public event Action<BattleResult> OnBattleEnd;
        public event Action<bool> OnChangeDungeon;
        public event Action OnChangeBiome;
        public event Action OnIncreaseProgress;
        [field: SerializeField] public Biome[] ListBiome { get; private set; }
        public Biome ActiveBiome => ListBiome[_activeIndexBiome];
        private int _activeIndexBiome=0;
        private Transform[] _enemiesPosition;

        private void Start()
        {
            Initialize();
            _inputManager.PlayerMode();
            _playerStats.InitializeStats(
                "Kamikaze",
                100,
                100, 
                2, 
                2, 
                12,
                90,
                100,
                0,
                3,
                2,
                2);
            StartGame();
        }

        private void Initialize()
        {
            _inputManager = InputManager.Instance;
            _playerStats = PlayerStats.Instance;
            _mapSystem = MapSystem.Instance;
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
            _mapSystem.InitializeMap();
        }
        public void ResumeGame()
        {
            Time.timeScale = 1f; 
        }

        public void PauseGame()
        {
            Time.timeScale = 0f; 
        }

    }
}