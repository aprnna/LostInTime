using UnityEngine;
using UnityEngine.SceneManagement;

public class TestAdditive : MonoBehaviour
{
    public void changeScene()
    {
        SceneManager.LoadSceneAsync("ShopScene", LoadSceneMode.Additive);
    }

    public void testToko()
    {
        Debug.Log("Ini Toko");
    }

    public void backScene()
    {
        SceneManager.UnloadSceneAsync("ShopScene");
    }
}
