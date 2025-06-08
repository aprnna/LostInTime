using Manager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }
    public GameObject MainCanvas;
    public ItemSO[] itemList;
    public ItemUI itemPrefab;
    public Transform itemContainer;
    public Text itemTextDescription;
    private GameManager _gameManager;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        ShowItem();
        _gameManager = GameManager.Instance;
    }

    public void ShowItem()
    {
        foreach (ItemSO item in itemList)
        {
            ItemUI itemSpawned = Instantiate(itemPrefab, itemContainer);
            itemSpawned.itemSO = item;
        }
    }

    public void Leave()
    {
        MainCanvas.SetActive(false);
        _gameManager.UIManager.SetMap(true);
        _gameManager.ChangeStatusMap(true);
    }

    public void ChangeDescription(string description)
    {
        itemTextDescription.text = description;
    }
}
