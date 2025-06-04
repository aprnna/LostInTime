using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapItemUI : MonoBehaviour, IPointerClickHandler
{

    private Image image;

    public Sprite spriteVisited;
    public Sprite defaultSprite;

    public MapNode mapItemData;

    private void Start()
    {
        image = GetComponent<Image>();

        transform.localPosition = mapItemData.position;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        MapManager.Instance.MovePlayerToNode(mapItemData);
    }

    private void OnEnable() {
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
            image.sprite = defaultSprite;
        }
        else
        {
            image.sprite = spriteVisited;
        }
    }
}
