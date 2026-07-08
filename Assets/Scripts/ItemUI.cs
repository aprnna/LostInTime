using Player;
using Playfab;
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
        int coinBefore = _playerStats.Coin;
        int hpBefore = _playerStats.Health;
        int shieldBefore = _playerStats.Shield;

        _playerStats.PaymentItem(itemSO.consumableType, itemSO.amount, itemSO.itemPrice);

        // Log only if purchase succeeded (coins decreased)
        if (_playerStats.Coin < coinBefore)
        {
            ShopManager.Instance.RegisterPurchase();
            LogShopPurchase(coinBefore, hpBefore, shieldBefore);
        }
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

    private void LogShopPurchase(int coinBefore, int hpBefore, int shieldBefore)
    {
        var ps = PlayerStats.Instance;
        BattleFileLogger.WriteEvent("shop_purchase", new
        {
            item_name = itemSO.itemName,
            item_type = itemSO.consumableType.ToString(),
            item_amount = itemSO.amount,
            item_price = itemSO.itemPrice,
            player_coin_before = coinBefore,
            player_coin_after = ps?.Coin ?? 0,
            player_hp_before = hpBefore,
            player_hp_after = ps?.Health ?? 0,
            player_max_hp = ps?.MaxHealth ?? 0,
            player_shield_before = shieldBefore,
            player_shield_after = ps?.Shield ?? 0,
            player_level = ps?.Level ?? 0
        });

        PlayfabManager.Instance?.EnqueueEvent("shop_purchase", new
        {
            item_name = itemSO.itemName,
            item_type = itemSO.consumableType.ToString(),
            item_amount = itemSO.amount,
            item_price = itemSO.itemPrice,
            player_coin_before = coinBefore,
            player_coin_after = ps?.Coin ?? 0,
            player_hp_before = hpBefore,
            player_hp_after = ps?.Health ?? 0,
            player_shield_before = shieldBefore,
            player_shield_after = ps?.Shield ?? 0,
            player_level = ps?.Level ?? 0
        });

        Debug.Log($"[ShopManager] Logged purchase: {itemSO.itemName} ({itemSO.consumableType}) for {itemSO.itemPrice} coin");
    }

}
