using System;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class BiomeController : MonoBehaviour
    {
        [field:SerializeField] public Image BackgroundBattle { get; private set; }
        [field: SerializeField] public Sprite BackgroundCaveBiome { get; private set; }
        [field: SerializeField] public Sprite BackgroundForestBiome { get; private set; }
    
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

        private void SetBackground(Sprite image)
        {
            BackgroundBattle.sprite = image;
            BackgroundBattle.type = Image.Type.Simple;
            BackgroundBattle.preserveAspect = true;
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