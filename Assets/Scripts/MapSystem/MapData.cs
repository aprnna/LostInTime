using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Maps/MapData")]
public class MapData : ScriptableObject
{
    public MapNode[] mapItems;
}
