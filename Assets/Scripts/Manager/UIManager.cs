using Player;
using Player.UI;
using Roulette;
using UnityEngine;

namespace Manager
{
    public class UIManager:MonoBehaviour
    {
        [SerializeField] private PlayerStatsUIController _playerStats;
        [SerializeField] private EnemyStatsUIController _enemyStats;
        [SerializeField] private GameObject _actionsButton;
        [SerializeField] private RouletteController _roulette;
        [SerializeField] private GameObject _map;
        [SerializeField] private GameObject _battleResult;
        public PlayerStatsUIController PlayerStatsUI => _playerStats;
        public EnemyStatsUIController EnemyStatsUI => _enemyStats;

        public void SetActionsButton(bool value)
        {
            _actionsButton.SetActive(value);
        }

        public void SetEnemyStats(EnemyStats enemyStats, bool active)
        {
            _enemyStats.gameObject.SetActive(active);
            _enemyStats.InitializeStats(enemyStats);
        }

        public void SetMap(bool value)
        {
            _map.SetActive(value);
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