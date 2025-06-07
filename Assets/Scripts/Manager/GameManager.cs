using System;
using Player;
using UnityEngine;

namespace Manager
{
    public class GameManager:PersistentSingleton<GameManager>
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private MapManager _mapManager;
        [SerializeField] private PlayerStats _playerStats;
        public UIManager UIManager => _uiManager;

        private void Start()
        {
            if(_mapManager.CurrentPlayerMapNode.mapType == MapType.Enemy)
            {
                
            }
        }

        public EnemySO[] GetEnemies()
        {
            return _mapManager.CurrentPlayerMapNode.enemies;
        }

        public DropItem[] GetDropItems()
        {
            return _mapManager.CurrentPlayerMapNode.DropItems;
        }
    }
}