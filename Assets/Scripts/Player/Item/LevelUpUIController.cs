using System;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player.Item
{
    public class LevelUpUIController:MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private ButtonAction ItemLevelUp;
        private GameManager _gameManager;
        void Start()
        {
            text.text = ItemLevelUp.Amount.ToString();
            _gameManager = GameManager.Instance;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _gameManager.OnClickLevelUp(ItemLevelUp);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _gameManager.OnHoverButtonLevelUp(ItemLevelUp);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _gameManager.SetDescription("");

        }
        void OnDestroy()
        {
            _gameManager.SetDescription("");
        }
    }
    [Serializable]
    public class ButtonAction
    {
        [SerializeField] private int _amount;
        [SerializeField] private ConsumableType _type;
        public int Amount => _amount;
        public ConsumableType Type => _type;
    }
}