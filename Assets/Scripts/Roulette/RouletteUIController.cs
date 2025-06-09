using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roulette
{
    public class RouletteUIController : MonoBehaviour
    {
        public ScrollRect scrollRect;
        public RectTransform content;
        public float scrollSpeed = 200f;
        private Button stopButton;
        private int stopAtIndex = 3;
        public bool autoStop = false;
        private bool isScrolling = true;
        private float itemWidth;
        public GameObject itemPrefab;
        public System.Action<int> onRouletteStopped;
        private int itemCount;
        public int minValue;
        public int maxValue;
        private float timer = 5;

        void Start()
        {
            itemCount = content.childCount;
            while (true)
            {
                if (itemCount > 7)
                {
                    break;
                }

                for (var i = minValue; i <= maxValue; i++)
                {
                    GameObject gameObject = Instantiate(itemPrefab, content);
                    TextMeshProUGUI text = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                    text.text = i.ToString();
                }

                itemCount = content.childCount;
            }

            itemWidth = ((RectTransform)content.GetChild(0)).rect.width + 1;
            GameObject buttonStop = GameObject.Find("Stop Button");
            if (buttonStop != null)
            {
                stopButton = buttonStop.GetComponent<Button>();
                stopButton.onClick.AddListener(StopScrolling);
            }

            timer = Random.Range(2, 6);
        }

        void Update()
        {
            if (!isScrolling) return;

            // Move content to the left
            content.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;

            // Check if first item is out of view and move it to the end
            RectTransform firstItem = content.GetChild(0) as RectTransform;
            float leftEdge = content.anchoredPosition.x + firstItem.anchoredPosition.x + itemWidth;

            if (leftEdge < 0)
            {
                firstItem.SetAsLastSibling();
                content.anchoredPosition += new Vector2(itemWidth, 0);
            }

            if (autoStop)
            {
                if (timer > 0)
                {
                    timer -= Time.deltaTime;
                }
                else
                {
                    StopScrolling();
                    autoStop = false;
                }

            }
        }

        void StopScrolling()
        {
            isScrolling = false;
            autoStop = false;
            StartCoroutine(SnapToIndex(stopAtIndex));
        }

        IEnumerator SnapToIndex(int index)
        {
            // Recalculate index based on current order
            RectTransform targetItem = content.GetChild(index) as RectTransform;
            // Calculate target position to bring item to front (or center)
            float targetX = -targetItem.anchoredPosition.x;
            float duration = 1f;
            float elapsed = 0f;
            float startX = content.anchoredPosition.x;


            while (elapsed < duration)
            {
                float newX = Mathf.Lerp(startX, targetX, elapsed / duration);
                content.anchoredPosition = new Vector2(newX, content.anchoredPosition.y);
                elapsed += Time.deltaTime;
                yield return null;
            }

            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
            GetData();
        }

        public void GetData()
        {

            RectTransform resultItem = content.GetChild(stopAtIndex + 2) as RectTransform;
            string resultText = resultItem.GetComponentInChildren<TextMeshProUGUI>().text;

            if (int.TryParse(resultText, out int resultValue))
                onRouletteStopped?.Invoke(resultValue);
            else
                Debug.LogWarning("Gagal mengonversi hasil ke integer: " + resultText);
        }
    }
}
