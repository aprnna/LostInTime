using System;
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
        [SerializeField] private TMP_Text _textDescription;
        [SerializeField] private TMP_Text _textInstruction;
        [SerializeField] private Image _background;
        private PlayerStats _playerStats;
        public PlayerStatsUIController PlayerStatsUI => _playerStatsUi;
        private void OnEnable()
        {
            _playerStats = PlayerStats.Instance;
            _playerStats.OnPlayerLevelUp += OnPlayerLevelUp;
        }

        private void OnDisable()
        {
            _playerStats.OnPlayerLevelUp -= OnPlayerLevelUp;
        }

        private void OnPlayerLevelUp()
        {
            SetLevelUpPanel(true);
        }

        public void SetTextInstruction(string value)
        {
            _textInstruction.text = value;
        }
        public void SetDescription(string value)
        {
            _textDescription.text = value;
        }
        public void SetLevelUpPanel(bool value)
        {
            _levelUpPanel.SetActive(value);
        }
        public void SetMap(bool value)
        {
            _background.transform.parent.gameObject.SetActive(!value);
            _map.SetActive(value);
        }

        public void SetBackground(Sprite image)
        {
            _background.sprite = image;
            _background.type = Image.Type.Simple;
            _background.preserveAspect = true;
        }

    }
}