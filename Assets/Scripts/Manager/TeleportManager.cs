using System;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class TeleportManager:MonoBehaviour
    {
        [Header("TeleportProgress")] 
        [SerializeField] private GameObject _progressContainer;
        [SerializeField] private Slider _slider;
        private GameManager _gameManager;

        public void OnEnable()
        {
            _gameManager.OnIncreaseProgress += UpdateTeleportProgress;
        }

        private void OnDisable()
        {
            _gameManager.OnIncreaseProgress += UpdateTeleportProgress;
        }

        public void SetTeleportProgress(bool value)
        {
            _progressContainer.SetActive(value);
        }

        public void UpdateTeleportProgress()
        {
            _slider.value = _gameManager.ProgressTeleport / 100f;
        }
    }
}