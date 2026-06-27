using System;
using System.Collections.Generic;
using Manager;
using Player;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class MapNode
{
    public string mapNodeId;
    public bool isNextBiome;
    public MapType mapType;
    public Vector2 position;
    public EnemySO[] enemies;
    public DropItem[] DropItems;

    public string[] connectionId;
    public bool isVisited;
}

[Serializable]
public class DropItem
{
    [SerializeField] private ConsumableType _type;
    [SerializeField] private Sprite _icon;
    [SerializeField] private int _amount;
    public ConsumableType Type=> _type;
    public Sprite Icon => _icon;
    public int Amount => _amount;

    // Runtime ctor for enemy-derived rewards (BattleSystem builds these from EnemySO rewards).
    public DropItem(ConsumableType type, Sprite icon, int amount)
    {
        _type = type;
        _icon = icon;
        _amount = amount;
    }
    public void AppliedToPlayerStats(PlayerStats playerStats)
    {
        switch (_type)
        {
            case ConsumableType.Health:
                playerStats.Heal(_amount);
                break;
            case ConsumableType.Exp:
                playerStats.AddExp(_amount); ;
                break;
            case ConsumableType.Shield:
                playerStats.AddShield(_amount);
                break;
            case ConsumableType.Coin:
                playerStats.CollectCoin(_amount);;
                break;
            default:
                Debug.Log("Type not match");
                break;
        }
    }
}
