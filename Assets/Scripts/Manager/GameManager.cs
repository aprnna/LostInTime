using System;
using Player;
using Roulette;
using Player.Item;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class GameManager:PersistentSingleton<GameManager>
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private MapManager _mapManager;
        [SerializeField] private EnemyBiomePos[] _enemyBiomePos;
        [SerializeField] private GameObject _prefabRoulette;
        [SerializeField] private Transform _rouletteContainer;
        [SerializeField] private bool _autoStartRoulette = false;
        [SerializeField] private GameObject _buttonStopRoulette;
        [SerializeField] private GameObject _buttonStartRoulette;
        [SerializeField] private GameObject _checkBoxAutoStart;
        public UIManager UIManager => _uiManager;
        public event Action PlayerLevelUp;
        public Biome ActiveBiome { get; private set; } = Biome.Cave;
        private PlayerStats _playerStats;
        public bool IsRouletteStop { get; private set; }
        public int RouletteResult { get; private set; }
        public GameObject RouletteObject { get; private set; }
        public GameObject ButtonStopRoulette => _buttonStopRoulette;
        public GameObject ButtonStartRoulette => _buttonStartRoulette;
        public bool IsAutoStart => _autoStartRoulette;
        public EnemySO[] GetEnemies()
        {
            return _mapManager.CurrentPlayerMapNode.enemies;
        }

        private void Start()
        {
            _playerStats = PlayerStats.Instance;
            _playerStats.InitializeStats(
                "Kamikaze",
                100,
                100, 
                1, 
                3, 
                20,
                0,
                20,
                0,
                3,
                10,
                4);
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
      

        public void SetRoulette(int min, int max)
        {
            IsRouletteStop = false;
            RouletteObject = Instantiate(_prefabRoulette, _rouletteContainer);
            RouletteUIController rouletteUI = RouletteObject.GetComponent<RouletteUIController>();
           
            rouletteUI.minValue = min;
            rouletteUI.maxValue = max;
            rouletteUI.autoStop = _autoStartRoulette;

            rouletteUI.onRouletteStopped = (result) =>
            {
                Debug.Log($"The roulette has stopped! The result is: {result}");
                IsRouletteStop = true;
                RouletteResult = result;
            };
        }
        public void SetRoulette(bool value, Action callback)
        {
            _rouletteContainer.gameObject.SetActive(value);
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