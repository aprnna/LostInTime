using System;
using Audio;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class MainMenuController:MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private GameObject _settingsPanel;
        private GameManager _gameManager;
        private void Start()
        {
            _gameManager = GameManager.Instance;
            _startButton.onClick.AddListener(OnStartButtonPressed);
            _exitButton.onClick.AddListener(OnExitButtonPressed);
            _settingsButton.onClick.AddListener(OnSettingsButtonPressed);
        }
        private void OnStartButtonPressed()
        {
            AudioManager.Instance.PlaySound(SoundType.SFX_Button);
            if(_gameManager.GameData.PrologCompleted) _gameManager.StartGame();
            else _gameManager.StartPrologue();
        }
        private void OnExitButtonPressed()
        {            
            AudioManager.Instance.PlaySound(SoundType.SFX_Button);
            Application.Quit();
        }
        private void OnSettingsButtonPressed()
        {
            AudioManager.Instance.PlaySound(SoundType.SFX_Button);
            _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }
    }
}