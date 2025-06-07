using Player;
using Player.Item;
using Player.UI;
using Roulette;
using UnityEngine;

namespace Manager
{
    public class UIManagerBattle:MonoBehaviour
    {
        [SerializeField] private EnemyStatsUIController _enemyStats;
        [SerializeField] private GameObject _actionsButton;
        [SerializeField] private RouletteController _roulette;
        [SerializeField] private GameObject _battleResult;
        [SerializeField] private GameObject PrefabDropItem;
        [SerializeField] private Transform DropItemContainer;
        public EnemyStatsUIController EnemyStatsUI => _enemyStats;

        public void SetActionsButton(bool value)
        {
            _actionsButton.SetActive(value);
        }

        public void InstantiateDropItem(Sprite icon, int value)
        {
            var controller = PrefabDropItem.GetComponent<DropItemController>();
            controller.SetDropItem(icon, value);
            Instantiate(PrefabDropItem, DropItemContainer);
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
    }
}