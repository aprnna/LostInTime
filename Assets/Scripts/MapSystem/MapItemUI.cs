using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapItemUI : MonoBehaviour, IPointerClickHandler
{

    private Image image;

    public Sprite enemySprite;
    public Sprite enemySpriteActive;
    public Sprite shopSprite;
    public Sprite shopSpriteActive;
    public Sprite restSprite;
    public Sprite restSpriteActive;
    public Sprite bossSprite;
    public Sprite bossSpriteActive;

    [HideInInspector]
    public MapNode mapItemData;

    private MapManager _mapManager;
    public void Init(MapNode node, MapManager manager)
    {
        mapItemData = node;
        _mapManager = manager;

        image = GetComponent<Image>();
        transform.localPosition = mapItemData.position + _mapManager.mapData.OffSet;

        switch (mapItemData.mapType)
        {
            case MapType.Enemy: image.sprite = enemySprite; break;
            case MapType.Shop: image.sprite = shopSprite; break;
            case MapType.Rest: image.sprite = restSprite; break;
            case MapType.Boss: image.sprite = bossSprite; break;
        }

        _mapManager.OnPlayerMoved += ChangeState;
        _mapManager.OnMapItemChange += ChangeMapStatus;
    }
       
    private void OnDestroy()
    {
        if (_mapManager != null)
        {
            _mapManager.OnPlayerMoved -= ChangeState;
            _mapManager.OnMapItemChange -= ChangeMapStatus;
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        MapManager.Instance.MovePlayerToNode(mapItemData);
    }


    private void ChangeMapStatus()
    {
        if (mapItemData.isVisited)
        {
            switch (mapItemData.mapType)
            {
                case MapType.Enemy:
                    image.sprite = enemySpriteActive;
                    break;
                case MapType.Shop:
                    image.sprite = shopSpriteActive;
                    break;
                case MapType.Rest:
                    image.sprite = restSpriteActive;
                    break;
                case MapType.Boss:
                    image.sprite = bossSpriteActive;
                    break;
                default:
                    break;
            }
        }
    }
    private void ChangeState(MapNode startNode, MapNode endNode)
    {
        if (mapItemData.isVisited)
        {
            switch (mapItemData.mapType)
            {
                case MapType.Enemy:
                    image.sprite = enemySpriteActive;
                    break;
                case MapType.Shop:
                    image.sprite = shopSpriteActive;
                    break;
                case MapType.Rest:
                    image.sprite = restSpriteActive;
                    break;
                case MapType.Boss:
                    image.sprite = bossSpriteActive;
                    break;
                default:
                    break;
            }
        }
    }
}
