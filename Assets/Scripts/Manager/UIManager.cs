using Player;
using Player.UI;
using Roulette;
using UnityEngine;

namespace Manager
{
    public class UIManager:MonoBehaviour
    {
        [SerializeField] private PlayerStatsUIController _playerStats;
        [SerializeField] private GameObject _map;
        public PlayerStatsUIController PlayerStatsUI => _playerStats;
        public void SetMap(bool value)
        {
            _map.SetActive(value);
        }

    }
}