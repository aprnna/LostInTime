using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Roulette
{
    public class RouletteUIController : MonoBehaviour
    {
        public ScrollRect scrollRect;
        public RectTransform content;
        public float scrollSpeed = 200f;
        private Button stopButton;
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
                if (itemCount > 7) break;

                for (var i = minValue; i <= maxValue; i++)
                {
                    GameObject go = Instantiate(itemPrefab, content);
                    go.GetComponentInChildren<TextMeshProUGUI>().text = i.ToString();
                }
                itemCount = content.childCount;
            }

            ShuffleItems();

            // +1 compensates for the 1-px spacing added by the Horizontal Layout Group,
            // so each recycle step matches the actual slot width.
            itemWidth = ((RectTransform)content.GetChild(0)).rect.width + 1f;

            GameObject buttonStop = GameObject.Find("Stop Button");
            if (buttonStop != null)
            {
                stopButton = buttonStop.GetComponent<Button>();
                stopButton.onClick.AddListener(StopScrolling);
            }

            timer = Random.Range(2f, 6f);
        }

        private void ShuffleItems()
        {
            int count = content.childCount;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                content.GetChild(j).SetSiblingIndex(i);
                content.GetChild(i).SetSiblingIndex(j);
            }
        }

        void Update()
        {
            if (!isScrolling) return;

            content.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;

            RectTransform firstItem = content.GetChild(0) as RectTransform;
            float leftEdge = content.anchoredPosition.x + firstItem.anchoredPosition.x + itemWidth;
            if (leftEdge < 0)
            {
                firstItem.SetAsLastSibling();
                content.anchoredPosition += new Vector2(itemWidth, 0);
            }

            if (autoStop)
            {
                if (timer > 0) timer -= Time.deltaTime;
                else { StopScrolling(); autoStop = false; }
            }
        }

        void StopScrolling()
        {
            if (!isScrolling) return;
            isScrolling = false;
            autoStop = false;
            StartCoroutine(SnapToNearest());
        }

        /// <summary>
        /// Grid-based snap: derives targetX algebraically so that an item centre
        /// always lands exactly at the indicator (viewport centre), regardless of
        /// how many items are visible or what the current scroll offset is.
        ///
        /// Math:
        ///   Item centres in content-local space: halfStep, 3·halfStep, 5·halfStep, …
        ///   We want: content.x + n·itemStep + halfStep = viewportHalfWidth
        ///   => content.x = viewportHalfWidth − halfStep − n·itemStep  [= reference − n·itemStep]
        ///   Choose n = round((reference − currentX) / itemStep) for the nearest grid position.
        /// </summary>
        IEnumerator SnapToNearest()
        {
            // Wait one frame so the Layout Group rebuilds anchoredPositions
            // after the last SetAsLastSibling() call in Update().
            yield return null;

            // Measure the real slot width from adjacent items (includes Layout Group spacing).
            float itemStep = itemWidth;
            if (content.childCount >= 2)
            {
                var rt0 = content.GetChild(0) as RectTransform;
                var rt1 = content.GetChild(1) as RectTransform;
                if (rt0 != null && rt1 != null)
                {
                    float measured = rt1.anchoredPosition.x - rt0.anchoredPosition.x;
                    if (measured > 0f) itemStep = measured;
                }
            }

            float halfStep           = itemStep * 0.5f;
            float viewportHalfWidth  = scrollRect.viewport.rect.width * 0.5f;
            float currentX           = content.anchoredPosition.x;

            // Grid-snap: find the content.x nearest to currentX that places an
            // item centre exactly at the indicator (viewportHalfWidth).
            float reference = viewportHalfWidth - halfStep;
            float n         = Mathf.Round((reference - currentX) / itemStep);
            float targetX   = reference - n * itemStep;

            // Smooth deceleration into the snapped position
            float duration = 0.5f;
            float elapsed  = 0f;
            float startX   = currentX;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                content.anchoredPosition = new Vector2(
                    Mathf.Lerp(startX, targetX, t),
                    content.anchoredPosition.y);
                elapsed += Time.deltaTime;
                yield return null;
            }
            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);

            // Read the value from whichever item is now centred under the indicator
            float pointerInContent = -targetX + viewportHalfWidth;
            RectTransform best    = null;
            float bestDist        = float.MaxValue;
            for (int i = 0; i < content.childCount; i++)
            {
                var rt = content.GetChild(i) as RectTransform;
                if (rt == null) continue;
                float dist = Mathf.Abs((rt.anchoredPosition.x + halfStep) - pointerInContent);
                if (dist < bestDist) { bestDist = dist; best = rt; }
            }

            if (best != null)
            {
                var tmp = best.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null && int.TryParse(tmp.text, out int result))
                {
                    Debug.Log($"[Roulette] Stopped. Result = {result}");
                    onRouletteStopped?.Invoke(result);
                    yield break;
                }
            }
            Debug.LogWarning("[Roulette] Could not read value from centred item.");
        }
    }
}
