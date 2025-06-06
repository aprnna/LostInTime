using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Items/ItemSO")]


public class ItemSO : ScriptableObject
{
    public string itemName;
    public string itemDescription;
    public Sprite sprite;
    public int itemPrice;
    

    public ConsumableType consumableType;
    public int amount;
}
