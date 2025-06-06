using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapNode
{
    public string mapNodeId;
    public MapType mapType;
    public Vector2 position;
    

    public string[] connectionId;
    [NonSerialized]
    public bool isVisited;

}
