using System;
using System.Linq;
using Manager;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSystem : MonoBehaviour
{
    public static MapSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        _gameManager = GameManager.Instance;
    }
    public MapData mapData;
    public GameObject Maps;
    public MapItemUI mapItemUIPrefab;
    public GameObject linePrefab;
    public Transform mapContainer;
    public Transform lineContainer;

    //[HideInInspector]
    public MapNode CurrentPlayerMapNode;
    public MapNode lastNode;
    public event Action<MapNode, MapNode> OnPlayerMoved;
    public event Action OnMapItemChange;
    private GameManager _gameManager;
    public void TriggerPlayerMoved(MapNode startNode, MapNode endNode)
    {
        OnPlayerMoved?.Invoke(startNode, endNode);
    }
    public void InitializeMap()
    {
        Debug.Log("InitializeMap");
        for (int i = 0; i < mapData.mapItems.Count(); i++)
        {
            mapItemUIPrefab = Instantiate(mapItemUIPrefab, mapContainer);
            mapItemUIPrefab.Init(mapData.mapItems[i], this);
            mapData.mapItems[i].isVisited = false;
            foreach (string item in mapData.mapItems[i].connectionId)
            {
                DrawLine(mapData.mapItems[i], GetNode(item));
            }

            if (i == 0)
            {
                CurrentPlayerMapNode = mapData.mapItems[i];
                TriggerChangeStatusMap(true);
                SceneManager.LoadSceneAsync(CurrentPlayerMapNode.mapType.ToString(), LoadSceneMode.Additive);
            }
        }
        lastNode = mapData.mapItems[mapData.mapItems.Count() - 1];
    }

    private void TriggerChangeStatusMap(bool value)
    {
        CurrentPlayerMapNode.isVisited = value;
        OnMapItemChange?.Invoke();
    }
    private void DrawLine(MapNode startNode, MapNode endNode)
    {
        GameObject line = Instantiate(linePrefab, lineContainer);
        RectTransform lineRect = line.GetComponent<RectTransform>();
        LineUI lineUI = line.GetComponent<LineUI>();
        lineUI.startNode = startNode;
        lineUI.endNode = endNode;

        Vector2 startPos = new Vector2(startNode.position.x + mapData.OffSet.x, startNode.position.y + mapData.OffSet.y);
        Vector2 endPos = new Vector2(endNode.position.x  + mapData.OffSet.x, endNode.position.y+ mapData.OffSet.y);

        Vector2 direction = endPos - startPos;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        lineRect.localPosition = startPos;

        lineRect.rotation = Quaternion.Euler(0, 0, angle);

        lineRect.sizeDelta = new Vector2(distance, 2f); // 5f is example thickness
    }


    public void MovePlayerToNode(MapNode mapNode)
    {
        if (CheckConnection(mapNode))
        {
            SceneManager.UnloadSceneAsync(CurrentPlayerMapNode.mapType.ToString());
            OnPlayerMoved?.Invoke(CurrentPlayerMapNode, mapNode);
            CurrentPlayerMapNode = mapNode;
            TriggerChangeStatusMap(true);
            SceneManager.LoadSceneAsync(mapNode.mapType.ToString(), LoadSceneMode.Additive);
            _gameManager.ChangeDungeon(false);
            Debug.Log("Moved to " + mapNode.mapNodeId + " map");
        }
        else
        {
            Debug.Log("You Cant Move Here");
        }
    }

    private bool CheckConnection(MapNode targetMapItem)
    {
        bool result = false;

        foreach (string item in CurrentPlayerMapNode.connectionId)
        {
            if (item == targetMapItem.mapNodeId)
            {
                result = true;
                break;
            }
        }

        return result;
    }

    private MapNode GetNode(String mapId)
    {
        MapNode node = null;

        foreach (MapNode item in mapData.mapItems)
        {
            if (item.mapNodeId == mapId)
            {
                node = item;
                break;
            }
        }

        return node;
    }
    public EnemySO[] GetEnemies()
    {
        return CurrentPlayerMapNode.enemies;
    }
    public DropItem[] GetDropItems()
    {
        return CurrentPlayerMapNode.DropItems;
    }
    public MapType GetMapType()
    {
        return CurrentPlayerMapNode.mapType;
    }
}
