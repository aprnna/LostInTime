using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Player;
using Player.Item;
using Roulette;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Manager
{
    public class RestSystem:MonoBehaviour
    {
        [SerializeField] private GameObject _mainCanvas;
        [SerializeField] private GameObject _buttonPanel;
        [SerializeField] private TMP_Text _textDescription;
        public static RestSystem Instance;
        private PlayerStats _playerStats;
        private RouletteSystem _rouletteSystem;
        private GameManager _gameManager;
        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }else
            {
                Destroy(gameObject);
            }
        }
        public void Start()
        {
            _playerStats = PlayerStats.Instance;
            _rouletteSystem = RouletteSystem.Instance;
            _gameManager = GameManager.Instance;
            
            _buttonPanel.SetActive(true);
        }

        public void SetDescription(string value)
        {
            _textDescription.text = value;
        }
        public void OnHoverAction(RestActionController restController)
        {
            switch (restController.RestItem.Type)
            {
                case RestType.Heal: SetDescription($"Take a short break to recover {restController.RestItem.Min}-{restController.RestItem.Max} Health"); break;
                case RestType.Repair: SetDescription($"Repair your weapon to restore its durability. Recovers {restController.RestItem.Min}-{restController.RestItem.Max} weapon usage"); break;
            }
        }
        public void OnClickAction(RestActionController restController)
        {
            _buttonPanel.SetActive(false);
            StartRoulette(restController).Forget();
        }

        private async UniTask StartRoulette(RestActionController restController)
        {
            var (rouletteObject, result) = await _rouletteSystem.SetRoulette(restController.RestItem.Min, restController.RestItem.Max,
                true);
            Destroy(rouletteObject);
            switch (restController.RestItem.Type)
            {
                case RestType.Heal: _playerStats.Heal(result);
                    break;
                case RestType.Repair:
                    RepairWeapon(result, restController.Action);
                    break;
                default:
                    Debug.Log("Type Not Match");
                    break;
            }
            Leave();
        }


        private void RepairWeapon(int value, BaseAction[] actions)
        {
            foreach (var action in actions)
            {
                if(action.IsLimited) action.AddLimit(value);
            }
        }
        private void Leave()
        {
            _buttonPanel.SetActive(false);
            _mainCanvas.SetActive(false);
            _gameManager.ChangeDungeon(true);
        }
    }
}