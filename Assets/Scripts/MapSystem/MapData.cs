using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Maps/MapData")]
public class MapData : ScriptableObject
{
    public Vector2 OffSet;
    public MapNode[] mapItems;
}
