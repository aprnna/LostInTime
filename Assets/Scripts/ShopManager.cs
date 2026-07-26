using System.Collections.Generic;
using Manager;
using Player;
using Player.Item;
using Playfab;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }
    public GameObject MainCanvas;
    public ItemSO[] itemList;
    public ItemUI itemPrefab;
    public Transform itemContainer;
    public Text itemTextDescription;
    private GameManager _gameManager;
    private int _purchaseCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        _purchaseCount = 0;
        ShowItem();
        _gameManager = GameManager.Instance;
        LogShopEnter();
    }

    /// <summary>
    /// Snapshot all limited actions for logging (current uses / max uses).
    /// </summary>
    private List<object> SnapshotAllActions()
    {
        var all = Resources.LoadAll<BaseAction>("Player/Actions");
        var list = new List<object>();
        foreach (var a in all)
        {
            if (a == null || !a.IsLimited) continue;
            list.Add(new
            {
                action_name = a.ActionName,
                action_type = a.ActionType.ToString(),
                current_limit = a.CurrentLimit,
                max_limit = a.Limit
            });
        }
        return list;
    }

    public void ShowItem()
    {
        foreach (ItemSO item in itemList)
        {
            ItemUI itemSpawned = Instantiate(itemPrefab, itemContainer);
            itemSpawned.itemSO = item;
        }
    }

    public void RegisterPurchase()
    {
        _purchaseCount++;
    }

    public void Leave()
    {
        LogShopLeave();
        MainCanvas.SetActive(false);
        _gameManager.ChangeDungeon(true);
    }

    public void ChangeDescription(string description)
    {
        itemTextDescription.text = description;
    }

    private void LogShopEnter()
    {
        var ps = PlayerStats.Instance;
        BattleFileLogger.WriteEvent("shop_enter", new
        {
            available_items = itemList.Length,
            player_coin = ps?.Coin ?? 0,
            player_level = ps?.Level ?? 0,
            player_hp = ps?.Health ?? 0,
            player_max_hp = ps?.MaxHealth ?? 0,
            action_limits = SnapshotAllActions()
        });

        Debug.Log($"[ShopManager] Player entered shop with {ps?.Coin ?? 0} coin, {itemList.Length} items available");
    }

    private void LogShopLeave()
    {
        var ps = PlayerStats.Instance;
        BattleFileLogger.WriteEvent("shop_leave", new
        {
            purchase_count = _purchaseCount,
            player_coin = ps?.Coin ?? 0,
            player_level = ps?.Level ?? 0,
            player_hp = ps?.Health ?? 0,
            player_max_hp = ps?.MaxHealth ?? 0,
            action_limits = SnapshotAllActions()
        });

        PlayfabManager.Instance?.EnqueueEvent("shop_visit", new
        {
            purchase_count = _purchaseCount,
            player_coin = ps?.Coin ?? 0,
            player_level = ps?.Level ?? 0,
            player_hp = ps?.Health ?? 0,
            player_max_hp = ps?.MaxHealth ?? 0
        });

        Debug.Log($"[ShopManager] Player left shop after {_purchaseCount} purchases, {ps?.Coin ?? 0} coin remaining");
    }
}
