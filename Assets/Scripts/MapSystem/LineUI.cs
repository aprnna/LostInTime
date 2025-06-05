using UnityEngine;
using UnityEngine.UI;

public class LineUI : MonoBehaviour
{
    public MapNode startNode;
    public MapNode endNode;

    private Image image;

    void Start()
    {
        image = GetComponent<Image>();
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
        if (startNode == this.startNode && endNode == this.endNode)
        {
            image.color = Color.blue;
        }
    }
}
