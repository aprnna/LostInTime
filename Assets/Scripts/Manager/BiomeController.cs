using System;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class BiomeController : MonoBehaviour
    {
        [field: SerializeField] public GameObject BackgroundCaveBiome { get; private set; }
        [field: SerializeField] public GameObject BackgroundForestBiome { get; private set; }
    
        private GameManager _gameManager;

        public void OnEnable()
        {
            _gameManager = GameManager.Instance;
            _gameManager.OnChangeBiome += OnChangeBackgroundBiome;
        }

        public void OnDisable()
        {
            _gameManager.OnChangeBiome -= OnChangeBackgroundBiome;
        }

        private void SetBackground(GameObject backgroundBattle)
        {
            backgroundBattle.SetActive(true); 
        }
        private void OnChangeBackgroundBiome()
        {
            switch (_gameManager.ActiveBiome)
            {
                case Biome.Cave: SetBackground(BackgroundCaveBiome); break;
                case Biome.Forest: SetBackground(BackgroundForestBiome); break;
            }
        }

        public void ShowBackground()
        {
            gameObject.SetActive(true);
        }

        public void HideBackground()
        {
            gameObject.SetActive(false);
        }
    }

    public enum Biome
    {
        Forest,
        Cave
    }
}