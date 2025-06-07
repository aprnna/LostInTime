using System;
using System.Collections.Generic;
using Player;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class MapNode
{
    public string mapNodeId;
    public MapType mapType;
    public Vector2 position;
    public EnemySO[] enemies;
    public DropItem[] DropItems;
    

    public string[] connectionId;
    [NonSerialized]
    public bool isVisited;
}

[Serializable]
public class DropItem
{
    [SerializeField] private ConsumableType _type;
    [SerializeField] private Sprite _icon;
    [SerializeField] private int _amount;

    public Sprite Icon => _icon;
    public int Amount => _amount;
    public void AppliedToPlayerStats(PlayerStats playerStats)
    {
        switch (_type)
        {
            case ConsumableType.Health:
                playerStats.Health += _amount;
                break;
            case ConsumableType.Exp:
                playerStats.Exp += _amount;
                break;
            case ConsumableType.Shield:
                playerStats.Shield += _amount;
                break;
            case ConsumableType.Coin:
                playerStats.Coin += _amount;
                break;
            default:
                Debug.Log("Type not match");
                break;
        }
    }
}
