using System;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Player.Item
{
    public class LevelUpUIController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text _tmpText;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private ButtonAction _itemLevelUp;
        
        private PlayerStats _playerStats;
        void Start()
        {
            _tmpText.text = _itemLevelUp.Amount.ToString();
            _playerStats = PlayerStats.Instance;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (_itemLevelUp.Type)
            {
                case ConsumableType.Health: _playerStats.LevelUpHealth(_itemLevelUp.Amount); break;
                case ConsumableType.Shield: _playerStats.LevelUpShield(_itemLevelUp.Amount); break;
                case ConsumableType.Damage: _playerStats.LevelUpDamage(_itemLevelUp.Amount); break;
            }
        }
        
     
        public void OnPointerEnter(PointerEventData eventData)
        {
            SetLevelUpDescription(_itemLevelUp);

        }
        public void OnPointerExit(PointerEventData eventData)
        {
            SetDescription("");

        }

        private void SetDescription(string text)
        {
            _description.text = text;
        }

        private void SetLevelUpDescription(ButtonAction itemLevelUp)
        {
            switch (itemLevelUp.Type)
            {
                case ConsumableType.Health: SetDescription($"Grants +{itemLevelUp.Amount} to your maximum Health, helping you survive longer against enemies."); break;
                case ConsumableType.Shield:  SetDescription($"Grants +{itemLevelUp.Amount} to your Shield value, reducing more incoming damage with each hit."); ; break;
                case ConsumableType.Damage:  SetDescription($"Grants +{itemLevelUp.Amount} to your Attack Points, allowing you to deal more damage and defeat enemies faster."); ; break;
            }
        }
        
        void OnDestroy()
        {
            SetDescription("");
        }
    }
    [Serializable]
    public class ButtonAction
    {
        [SerializeField] private ConsumableType _type;
        [SerializeField] private int _amount;
        public int Amount => _amount;
        public ConsumableType Type => _type;
    }
}