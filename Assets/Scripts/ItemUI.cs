using Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerStats _playerStats;
    [SerializeField] private Image image;
    public ItemSO itemSO;

    void Start()
    {
        _playerStats = PlayerStats.Instance;
        image.sprite = itemSO.sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("You clicked " + itemSO.itemName);
        _playerStats.PaymentItem(itemSO.consumableType, itemSO.amount, itemSO.itemPrice);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        switch (itemSO.consumableType)
        {
            case ConsumableType.Health:
                ShopManager.Instance.ChangeDescription(itemSO.itemPrice 
                                                       + " Gold - A warm, hearty meal that restores your strength. Recovers + "
                                                       +itemSO.amount+" Health instantly."); 
                break;
            case ConsumableType.Shield:
                ShopManager.Instance.ChangeDescription(itemSO.itemPrice 
                                                       + " Gold - Compact defense core that recharges your shield. Restores + "
                                                       +itemSO.amount+" Shield when used."); 
                break;
            default:Debug.Log("Not Match");
                break;
        };
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ShopManager.Instance.ChangeDescription("");
    }

    void OnDestroy()
    {
        ShopManager.Instance.ChangeDescription("");
    }

}
