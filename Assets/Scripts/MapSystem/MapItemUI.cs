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

    private void Start()
    {
        image = GetComponent<Image>();

        transform.localPosition = mapItemData.position;

        switch (mapItemData.mapType)
        {
            case MapType.Enemy:
                image.sprite = enemySprite;
                break;
            case MapType.Shop:
                image.sprite = shopSprite;
                break;
            case MapType.Rest:
                image.sprite = restSprite;
                break;
            case MapType.Boss:
                image.sprite = bossSprite;
                break;
            default:
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        MapManager.Instance.MovePlayerToNode(mapItemData);
    }

    private void OnEnable()
    {
        MapManager.OnPlayerMoved += ChangeState;
    }

    private void OnDisable()
    {
        MapManager.OnPlayerMoved -= ChangeState;
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
