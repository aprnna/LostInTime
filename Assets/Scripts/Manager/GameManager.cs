using System;
using System.Collections;
using Audio;
using Player;
using Roulette;
using Player.Item;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class GameManager:MonoBehaviour
    {
        public static GameManager Instance;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private MapManager _mapManager;
        [SerializeField] private EnemyBiomePos[] _enemyBiomePos;
        [SerializeField] private GameObject _prefabRoulette;
        
        [Header("Roulette")]
        [SerializeField] private Transform _rouletteContainer;
        [SerializeField] private bool _autoStartRoulette = false;
        [SerializeField] private GameObject _buttonStopRoulette;
        [SerializeField] private GameObject _buttonStartRoulette;
        [SerializeField] private GameObject _checkBoxAutoStart;

        [Header("TeleportProgress")] 
        [SerializeField] private GameObject _progressContainer;
        [SerializeField] private int _percentage;
        [SerializeField] private Slider _slider;
        
        public UIManager UIManager => _uiManager;
        public event Action PlayerLevelUp;
        public Biome ActiveBiome { get; private set; } = Biome.Cave;
        private PlayerStats _playerStats;
        public GameObject ButtonStopRoulette => _buttonStopRoulette;
        public GameObject ButtonStartRoulette => _buttonStartRoulette;
        public bool IsAutoStart => _autoStartRoulette;
        public EnemySO[] GetEnemies()
        {
            return _mapManager.CurrentPlayerMapNode.enemies;
        }
        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(this);
            }else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _playerStats = PlayerStats.Instance;
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
        }
        public Transform[] GetEnemiesPos(Biome type)
        {
            foreach (var enemyBiome in _enemyBiomePos)
            {
                if (enemyBiome.Type == type) return enemyBiome.EnemiesPos;
            }
            Debug.Log("BIOME NOT FOUND");
            return null;
        }
        public MapType GetMapType()
        {
            return _mapManager.CurrentPlayerMapNode.mapType;
        }
        public Sprite GetBackground()
        {
            return _mapManager.CurrentPlayerMapNode.changeBackground;
        }

        public Biome GetNextBiome()
        {
            return _mapManager.CurrentPlayerMapNode.changeBiome;
        }
        
        public void SetTeleportProgress(bool value)
        {
            _progressContainer.SetActive(value);
        }

        public void UpdateTeleportProgress(int value)
        {
            _percentage = Mathf.Clamp(_percentage + value, 0, 100);
            _slider.value = _percentage / 100f;
        }
        public void ChangeBiome(Sprite image, Biome type)
        {
            ActiveBiome = type;
            _uiManager.SetBackground(image);
            
        }
        public void OnHoverButtonLevelUp(ButtonAction itemLevelUp)
        {
            SetLevelUpDescription(itemLevelUp);
        }

        public void SetLevelUpDescription(ButtonAction itemLevelUp)
        {
            switch (itemLevelUp.Type)
            {
                case ConsumableType.Health: SetDescription($"Grants +{itemLevelUp.Amount} to your maximum Health, helping you survive longer against enemies."); break;
                case ConsumableType.Shield:  SetDescription($"Grants +{itemLevelUp.Amount} to your Shield value, reducing more incoming damage with each hit."); ; break;
                case ConsumableType.Damage:  SetDescription($"Grants +{itemLevelUp.Amount} to your Attack Points, allowing you to deal more damage and defeat enemies faster."); ; break;
            }
        }

        public void SetDescription(string value)
        {
            _uiManager.SetDescription(value);
        }

        public void SetInstruction(string value)
        {
            _uiManager.SetTextInstruction(value);
        }

        public GameObject SpawnRoulette()
        {
            var rouletteObject = Instantiate(_prefabRoulette, _rouletteContainer);
            return rouletteObject;
        }
        public IEnumerator SetAndPLayRoulette(GameObject rouletteObject,int min, int max, bool autoStart, Action<int> onStopped)
        {
            var isRouletteStop = false;
            AudioManager.Instance.PlaySound(SoundType.SFX_Roulette);
            RouletteUIController rouletteUI = rouletteObject.GetComponent<RouletteUIController>();

            rouletteUI.minValue = min;
            rouletteUI.maxValue = max;
            rouletteUI.autoStop = autoStart;

            rouletteUI.onRouletteStopped = (result) =>
            {
                Debug.Log($"The roulette has stopped! The result is: {result}");
                isRouletteStop = true;
                AudioManager.Instance.StopSound(SoundType.SFX_Roulette);
                onStopped?.Invoke(result);
            };
            yield return new WaitUntil(() => isRouletteStop);
        }

        public void SetRouletteButton(bool value, Action callback)
        {
            // _rouletteContainer.gameObject.SetActive(value);
            ClearButtonRoulette();
            if(_autoStartRoulette) _buttonStopRoulette.SetActive(value);
            else _buttonStartRoulette.SetActive(value);
            _checkBoxAutoStart.SetActive(value);
     
            Toggle toggle = _checkBoxAutoStart.GetComponent<Toggle>();
            toggle.isOn = _autoStartRoulette;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener((bool isOn) =>
            {
                _autoStartRoulette = isOn;
            });
            
            Button button = _buttonStartRoulette.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }

        public void ClearButtonRoulette()
        {
            _buttonStopRoulette.SetActive(false);
            _buttonStartRoulette.SetActive(false);
            _checkBoxAutoStart.SetActive(false);
        }
        public void OnClickLevelUp(ButtonAction itemLevelUp)
        {
            switch (itemLevelUp.Type)
            {
                case ConsumableType.Health: _playerStats.LevelUpHealth(itemLevelUp.Amount); break;
                case ConsumableType.Shield: _playerStats.LevelUpShield(itemLevelUp.Amount); break;
                case ConsumableType.Damage: _playerStats.LevelUpDamage(itemLevelUp.Amount); break;
            }
            SetLevelUpPanel(false);
            PlayerLevelUp?.Invoke();
        }
        public DropItem[] GetDropItems()
        {
            return _mapManager.CurrentPlayerMapNode.DropItems;
        }

        public void ChangeStatusMap(bool value)
        {
            _mapManager.TriggerChangeStatusMap(value);
        }

        public void DestroyObject(GameObject gameObject)
        {
            Destroy(gameObject);
        }
        public void SetLevelUpPanel(bool value)
        {
            _uiManager.SetLevelUpPanel(value);
        }
    }
}