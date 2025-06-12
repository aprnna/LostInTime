using UnityEngine;
using UnityEngine.SceneManagement;

public class TestAdditive : MonoBehaviour
{
    public void changeScene()
    {
        if (!SceneManager.GetSceneByName("ShopScene").isLoaded)
        {
            SceneManager.LoadSceneAsync("ShopScene", LoadSceneMode.Additive);
        }
        else
        {
            Debug.Log("ShopScene sudah dimuat.");
        }
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
