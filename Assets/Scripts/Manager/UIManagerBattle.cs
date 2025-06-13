using Player;
using Player.Item;
using Player.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public class UIManagerBattle:MonoBehaviour
    {
        [SerializeField] private GameObject _mainCanvas;
        [Header("EnemySide")]
        [SerializeField] private GameObject _enemyPortrait;
        [SerializeField] private EnemyStatsUIController _enemyStats;
        [SerializeField] private GameObject _actionsPanel;
        [SerializeField] private GameObject _prefabDropItem;
        [SerializeField] private GameObject _dropItemPanel;
        [SerializeField] private Transform _dropItemContainer;
        [SerializeField] private Button _buttonCollectReward;
        public EnemyStatsUIController EnemyStatsUI => _enemyStats;
        public Button ButtonCollectReward => _buttonCollectReward;

        public void SetEnemyPanel(EnemyStats enemyStats, bool active)
        {
            _enemyStats.InitializeStats(enemyStats);   
            SetEnemyPortrait(enemyStats.GetPortrait(), active);
        }
        public void SetEnemyPortrait(Sprite image, bool active)
        {
            _enemyStats.transform.parent.gameObject.SetActive(active);

            var imageComp = _enemyPortrait.GetComponent<Image>();
            imageComp.sprite = image;
            imageComp.type = Image.Type.Simple;
            imageComp.preserveAspect = true;
        }
        public void SetActionsButton(bool value)
        {
            _actionsPanel.SetActive(value);
        }

        public void SetDropItemPanel(bool value)
        {
            _dropItemPanel.SetActive(value);
        }

        public void InstantiateDropItem(Sprite icon, int value)
        {
            var controller = _prefabDropItem.GetComponent<DropItemController>();
            controller.SetDropItem(icon, value);
            Instantiate(_prefabDropItem, _dropItemContainer);
        }

        public void ClearDropItem()
        {
            foreach (Transform child in _dropItemContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }
        public void SetActionPanel(bool value)
        {
            _actionsPanel.SetActive(value);
        }
        public void SetMainCanvas(bool value)
        {
            _mainCanvas.SetActive(value);
        }
    }
}