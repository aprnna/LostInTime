using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Player.Item
{
    public class DropItemController:MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text text;

        public void SetDropItem(Sprite newIcon, int value)
        {
            icon.sprite = newIcon;
            text.text = value.ToString();
        }
    }
}