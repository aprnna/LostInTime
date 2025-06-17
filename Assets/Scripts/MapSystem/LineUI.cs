using UnityEngine;
using UnityEngine.UI;

public class LineUI : MonoBehaviour
{
    public MapNode startNode;
    public MapNode endNode;

    private Image image;
    private MapSystem _mapSystem;
    void Start()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _mapSystem = MapSystem.Instance;
        _mapSystem.OnPlayerMoved += ChangeState;
    }

    private void OnDisable()
    {
        _mapSystem.OnPlayerMoved -= ChangeState;
    }
    
    private void ChangeState(MapNode startNode, MapNode endNode)
    {
        if (startNode == this.startNode && endNode == this.endNode)
        {
            Color newColor = new Color(88f / 255f, 84f / 255f, 74f / 255f);
            image.color = newColor;
        }
    }
}
