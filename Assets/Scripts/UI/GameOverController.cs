using System;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GameOverController: MonoBehaviour
    {
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;

        private void Start()
        {
            _restartButton.onClick.AddListener(OnRestartButtonPressed);
            _mainMenuButton.onClick.AddListener(OnMainMenuButtonPressed);
        }
        
        private void OnRestartButtonPressed()
        {
            GameManager.Instance.RestartGame();
        }
        private void OnMainMenuButtonPressed()
        {
            GameManager.Instance.MainMenu();
        }
    }
}