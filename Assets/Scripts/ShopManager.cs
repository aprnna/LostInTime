using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    public ItemSO[] itemList;
    public ItemUI itemPrefab;
    public Transform itemContainer;
    public Text itemTextDescription;

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
        //show map
        MapManager.Instance.ShowMap();
        //unload the scene
        SceneManager.UnloadSceneAsync("Shop");

    }

    public void ChangeDescription(string description)
    {
        itemTextDescription.text = description;
    }
}
