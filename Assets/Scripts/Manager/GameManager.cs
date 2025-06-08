using System;
using Player;
using Player.Item;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Manager
{
    public class GameManager:PersistentSingleton<GameManager>
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private MapManager _mapManager;
        [SerializeField] private PlayerStats _playerStats;
        public UIManager UIManager => _uiManager;
        public event Action PlayerLevelUp;
        public EnemySO[] GetEnemies()
        {
            return _mapManager.CurrentPlayerMapNode.enemies;
        }

        public MapType GetMapType()
        {
            return _mapManager.CurrentPlayerMapNode.mapType;
        }
        public Sprite GetBackground()
        {
            return _mapManager.CurrentPlayerMapNode.changeBackground;
        }
        public void ChangeBackground(Sprite image)
        {
            _uiManager.SetBackground(image);
        }
        public void OnHoverButtonLevelUp(ButtonAction itemLevelUp)
        {
            SetLevelUpDescription(itemLevelUp);
        }

        public void SetLevelUpDescription(ButtonAction itemLevelUp)
        {
            switch (itemLevelUp.Type)
            {
                case ConsumableType.Health: SetDescription($"Grants +{itemLevelUp.Amount} to your maximum Health, helping you survive longer against enemies."); break;
                case ConsumableType.Shield:  SetDescription($"Grants +{itemLevelUp.Amount} to your Shield value, reducing more incoming damage with each hit."); ; break;
                case ConsumableType.Damage:  SetDescription($"Grants +{itemLevelUp.Amount} to your Attack Points, allowing you to deal more damage and defeat enemies faster."); ; break;
            }
        }

        public void SetDescription(string value)
        {
            _uiManager.SetDescription(value);
        }

        public void SetInstruction(string value)
        {
            _uiManager.SetTextInstruction(value);
        }
      
        public void OnClickLevelUp(ButtonAction itemLevelUp)
        {
            switch (itemLevelUp.Type)
            {
                case ConsumableType.Health: _playerStats.LevelUpHealth(itemLevelUp.Amount); break;
                case ConsumableType.Shield: _playerStats.LevelUpShield(itemLevelUp.Amount); break;
                case ConsumableType.Damage: _playerStats.LevelUpDamage(itemLevelUp.Amount); break;
            }
            SetLevelUpPanel(false);
            PlayerLevelUp?.Invoke();
        }
        public DropItem[] GetDropItems()
        {
            return _mapManager.CurrentPlayerMapNode.DropItems;
        }

        public void ChangeStatusMap(bool value)
        {
            _mapManager.TriggerChangeStatusMap(value);
        }

        public void SetLevelUpPanel(bool value)
        {
            _uiManager.SetLevelUpPanel(value);
        }
    }
}