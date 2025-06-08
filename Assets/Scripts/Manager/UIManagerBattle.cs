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
        [SerializeField] private GameObject _enemyPortrait;
        [SerializeField] private EnemyStatsUIController _enemyStats;
        [SerializeField] private GameObject _actionsPanel;
        [SerializeField] private GameObject _prefabDropItem;
        [SerializeField] private GameObject _dropItemPanel;
        [SerializeField] private Transform _dropItemContainer;
        [SerializeField] private TMP_Text _actionDescription;
        public EnemyStatsUIController EnemyStatsUI => _enemyStats;

  
        public void SetEnemyPanel(bool value)
        {
            _enemyStats.transform.parent.gameObject.SetActive(value);
        }
        public void SetEnemyPortrait(Sprite image)
        {
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
        public void SetEnemyStats(EnemyStats enemyStats)
        {
            _enemyStats.InitializeStats(enemyStats);
        }


        public void SetActionDescription(string value)
        {
            _actionDescription.text = value;
        }

        public void SetMainCanvas(bool value)
        {
            _mainCanvas.SetActive(value);
        }
    }
}