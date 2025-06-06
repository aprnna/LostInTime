using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Image image;
    public ItemSO itemSO;

    void Start()
    {
        image = GetComponent<Image>();
        image.sprite = itemSO.sprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("You clicked " + itemSO.itemName);
        switch (itemSO.consumableType)
        {
            case ConsumableType.Health:
            
                break;
            case ConsumableType.Shield:
                break;
            default:
                break;
        }
        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShopManager.Instance.ChangeDescription(itemSO.itemPrice + " Gold - " + itemSO.itemDescription);
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
