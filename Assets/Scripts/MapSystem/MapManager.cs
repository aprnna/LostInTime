using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public MapData mapData;

    public MapItemUI mapItemUIPrefab;
    public GameObject linePrefab;
    public Transform mapContainer;
    public Transform lineContainer;

    [HideInInspector]
    public MapNode CurrentPlayerMapNode;


    public static event Action<MapNode, MapNode> OnPlayerMoved;

    public static void TriggerPlayerMoved(MapNode startNode, MapNode endNode)
    {
        OnPlayerMoved?.Invoke(startNode, endNode);
    }

    void Start()
    {
        for (int i = 0; i < mapData.mapItems.Count(); i++)
        {
            mapItemUIPrefab = Instantiate(mapItemUIPrefab, mapContainer);
            mapItemUIPrefab.mapItemData = mapData.mapItems[i];

            foreach (string item in mapData.mapItems[i].connectionId)
            {
                DrawLine(mapData.mapItems[i], getNode(item));
            }

            if (i == 0)
            {
                CurrentPlayerMapNode = mapData.mapItems[i];
            }
        }


    }

    private void DrawLine(MapNode startNode, MapNode endNode)
    {
        GameObject line = Instantiate(linePrefab, lineContainer);
        RectTransform lineRect = line.GetComponent<RectTransform>();
        LineUI lineUI = line.GetComponent<LineUI>();
        lineUI.startNode = startNode;
        lineUI.endNode = endNode;

        Vector2 startPos = new Vector2(startNode.position.x, startNode.position.y);
        Vector2 endPos = new Vector2(endNode.position.x, endNode.position.y);

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
            CurrentPlayerMapNode.isVisited = true;
            OnPlayerMoved.Invoke(CurrentPlayerMapNode, mapNode);
            CurrentPlayerMapNode = mapNode;
            switch (mapNode.mapType)
            {
                case MapType.Shop:
                    SceneManager.LoadSceneAsync("ShopScene", LoadSceneMode.Additive);
                    break;
                default:
                    break;
            }
            HideMap();
            Debug.Log("Moved to " + mapNode.mapNodeId + " map");
        }
        else
        {
            Debug.Log("You Cant Move Here");
        }
    }

    public bool CheckConnection(MapNode targetMapItem)
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

    public MapNode getNode(String mapId)
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

    public void ShowMap()
    {
        mapContainer.gameObject.SetActive(true);
        lineContainer.gameObject.SetActive(true);
    }

    public void HideMap()
    {
        mapContainer.gameObject.SetActive(false);
        lineContainer.gameObject.SetActive(false);
    }
}
