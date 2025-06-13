using System;
using Input;
using Player;
using Player.UI;
using Roulette;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class UIManager:MonoBehaviour
    {
        [SerializeField] private PlayerStatsUIController _playerStatsUi;
        [SerializeField] private GameObject _map;
        [SerializeField] private GameObject _levelUpPanel;
        [SerializeField] private TMP_Text _textInstruction;
        [SerializeField] private GameObject _battleResult;
        [SerializeField] private GameObject _pauseMenuUI;
        [SerializeField] private BiomeController _biomeController;
        private InputManager _inputManager;
        private PlayerStats _playerStats;
        private GameManager _gameManager;

        private void OnEnable()
        {
            _inputManager = InputManager.Instance;
            _playerStats = PlayerStats.Instance;
            _gameManager = GameManager.Instance;
            

            _playerStats.OnPlayerLevelUp += OnPlayerLevelUp;
            _gameManager.OnChangeInstruction += OnChangeInstruction;
            _gameManager.OnChangeDungeon += OnChangeDungeon;
            _gameManager.OnBattleEnd += OnBattleEnd;

        }

        private void OnDisable()
        {

            _playerStats.OnPlayerLevelUp -= OnPlayerLevelUp;
            _gameManager.OnChangeInstruction -= OnChangeInstruction;
            _gameManager.OnChangeDungeon -= OnChangeDungeon;
            _gameManager.OnBattleEnd -= OnBattleEnd;

        }

        private void OnPlayerLevelUp()
        {
            SetLevelUpPanel(!_levelUpPanel.activeSelf);
        }

        private void OnChangeDungeon(bool value)
        {
            _map.SetActive(value);
            if (value) _biomeController.HideBackground();
            else _biomeController.ShowBackground();
        }
        public void OnBattleEnd(BattleResult battleResult)
        {
            switch (battleResult)
            {
                case BattleResult.PlayerWin: 
                    _biomeController.HideBackground();
                    _map.SetActive(true);
                    break;
                case BattleResult.EnemiesWin: 
                    _battleResult.SetActive(true); 
                    break;
            }
        }
        public void OnChangeInstruction(string value)
        {
            _textInstruction.text = value;
        }
        public void OnResumeGame()
        {
            _pauseMenuUI.SetActive(false);
        }

        public void OnPauseGame()
        {
            _pauseMenuUI.SetActive(true);
        }
        public void SetLevelUpPanel(bool value)
        {
            _levelUpPanel.SetActive(value);
        }
    }
}