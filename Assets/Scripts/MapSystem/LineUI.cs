using UnityEngine;
using UnityEngine.UI;

public class LineUI : MonoBehaviour
{
    public MapNode startNode;
    public MapNode endNode;

    private Image image;
    private MapManager _mapManager;
    void Start()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _mapManager = MapManager.Instance;
        _mapManager.OnPlayerMoved += ChangeState;
    }

    private void OnDisable()
    {
        _mapManager.OnPlayerMoved -= ChangeState;
    }
    
    private void ChangeState(MapNode startNode, MapNode endNode)
    {
        if (startNode == this.startNode && endNode == this.endNode)
        {
            image.color = Color.blue;
        }
    }
}
