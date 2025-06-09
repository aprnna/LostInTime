using System;
using System.Collections;
using Player;
using Player.Item;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Manager
{
    public class RestManager:MonoBehaviour
    {
        [SerializeField] private GameObject _mainCanvas;
        [SerializeField] private GameObject _buttonPanel;
        [SerializeField] private TMP_Text _textDescription;
        public static RestManager Instance;
        private GameManager _gameManager;
        private PlayerStats _playerStats;
        
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
            _gameManager = GameManager.Instance;
            _playerStats = PlayerStats.Instance;
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
            StartCoroutine(Roulette(restController));
        }

        public IEnumerator Roulette(RestActionController restController)
        {
            var roulette = _gameManager.SpawnRoulette();
            var resultroll = 0;
            yield return _gameManager.SetAndPLayRoulette(roulette, restController.RestItem.Min, restController.RestItem.Max, true,(result) => resultroll = result);
            switch (restController.RestItem.Type)
            {
                case RestType.Heal: PlayerRest(resultroll); break;
                case RestType.Repair: RepairWeapon(resultroll, restController.Action); break;
            }

            yield return new WaitForSeconds(2f);
            Destroy(roulette);
            Leave();
        }
        private void RepairWeapon(int value, BaseAction[] actions)
        {
            foreach (var action in actions)
            {
                if(action.IsLimited) action.AddLimit(value);
            }
        }

        private void PlayerRest(int value)
        {
            _playerStats.Heal(value);
        }

        private void Leave()
        {
            _buttonPanel.SetActive(false);
            _mainCanvas.SetActive(false);
            _gameManager.UIManager.SetMap(true);
            _gameManager.ChangeStatusMap(true);
        }
    }
}