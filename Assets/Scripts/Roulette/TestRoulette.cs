using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roulette
{
    public class TestRoulette : MonoBehaviour
    {
        public GameObject roulettePrefab;
        public Transform canvas;

        void Start()
        {
            GameObject gameObject = Instantiate(roulettePrefab, canvas);
            RouletteUIController rouletteUI = gameObject.GetComponent<RouletteUIController>();

            rouletteUI.minValue = 10;
            rouletteUI.maxValue = 15;
            rouletteUI.autoStop = true;

            rouletteUI.onRouletteStopped = (result) =>
            {
                Debug.Log($"The roulette has stopped! The result is: {result}");

                // Destroy(gameObject, 1f);
            };
        }
    }
}
