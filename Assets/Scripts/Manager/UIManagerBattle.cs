using Player;
using Player.Item;
using Player.UI;
using Roulette;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class UIManagerBattle:MonoBehaviour
    {
        [SerializeField] private EnemyStatsUIController _enemyStats;
        [SerializeField] private GameObject _actionsPanel;
        [SerializeField] private RouletteController _roulette;
        [SerializeField] private GameObject _battleResult;
        [SerializeField] private GameObject _prefabDropItem;
        [SerializeField] private GameObject _dropItemPanel;
        [SerializeField] private Transform _dropItemContainer;
        [SerializeField] private TMP_Text _actionDescription;
        public EnemyStatsUIController EnemyStatsUI => _enemyStats;

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
        public void SetEnemyStats(EnemyStats enemyStats, bool active)
        {
            _enemyStats.gameObject.SetActive(active);
            _enemyStats.InitializeStats(enemyStats);
        }

        public void SetBattleResult(bool value)
        {
            _battleResult.SetActive(value);
        }

        public void SetRouletteButton(bool value, System.Action callback)
        {
            _roulette.gameObject.SetActive(value);
            _roulette.ButtonStart.onClick.RemoveAllListeners();
            _roulette.ButtonStart.onClick.AddListener(()=> callback());
        }

        public void SetActionDescription(string value)
        {
            _actionDescription.text = value;
        }
    }
}