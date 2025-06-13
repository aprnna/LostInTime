using System;
using UnityEngine;

namespace Manager
{
    public class EnemyPositionController:MonoBehaviour
    {
        [field:SerializeField] public EnemiesPositions[] EnemyPositions { get; private set; }
        private GameManager _gameManager;

        private void Start()
        {
            OnChangeBiome();
        }

        private void OnEnable()
        {
            _gameManager = GameManager.Instance;
            _gameManager.OnChangeBiome += OnChangeBiome;
        }

        private void OnDisable()
        {
            _gameManager.OnChangeBiome -= OnChangeBiome;
        }

        private void OnChangeBiome()
        {
            _gameManager.SetEnemyPosition(GetEnemiesPositions());
        }
        private Transform[] GetEnemiesPositions()
        {
            foreach (var enemy in EnemyPositions)
            {
                if (enemy.BiomeType == _gameManager.ActiveBiome) return enemy.EnemiesPos;
            }
            return null;
        }
            
    }
    [Serializable]
    public class EnemiesPositions
    {
        [SerializeField] private Transform[] _pos;
        [SerializeField] private Biome _biomeType;
        public Transform[] EnemiesPos => _pos;
        public Biome BiomeType => _biomeType;
    }
}