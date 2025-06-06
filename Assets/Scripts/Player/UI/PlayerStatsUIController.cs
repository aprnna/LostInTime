using System;
using UnityEngine;

namespace Player.UI
{
    public class PlayerStatsUIController:MonoBehaviour
    {
        [SerializeField] private StatsItem _health;
        [SerializeField] private StatsItem _shield;
        [SerializeField] private StatsItem _damage;
        [SerializeField] private StatsItem _exp;
        [SerializeField] private StatsItem _coin;

        private PlayerStats _playerStats;
        private void Start()
        {
            InitializeStats();
        }

        private void OnEnable()
        {
            _playerStats = PlayerStats.Instance;
            _playerStats.OnHealthStatsChange += OnChangeHealth;
            _playerStats.OnCoinStatsChange += OnChangeCoin;
            _playerStats.OnExpStatsChange += OnChangeExp;
            _playerStats.OnBaseDamageChange += OnChangeDamage;
            _playerStats.OnShieldStatsChange += OnChangeShield;
        }

        private void OnDisable()
        {
            _playerStats.OnHealthStatsChange -= OnChangeHealth;
            _playerStats.OnCoinStatsChange -= OnChangeCoin;
            _playerStats.OnExpStatsChange -= OnChangeExp;
            _playerStats.OnBaseDamageChange -= OnChangeDamage;
            _playerStats.OnShieldStatsChange -= OnChangeShield;
        }

        private void OnChangeHealth()
        {
            _health.SetStat(_playerStats.MaxHealth, _playerStats.Health);
        }

        private void OnChangeShield()
        {
            _shield.SetStat(_playerStats.MaxShield, _playerStats.Shield);
        }

        private void OnChangeDamage()
        {
            _damage.SetStat(_playerStats.MaxDamage(), _playerStats.MinDamage());
        }

        private void OnChangeExp()
        {
            _exp.SetStat(_playerStats.MaxExp, _playerStats.Exp);
        }

        private void OnChangeCoin()
        {
            _coin.SetStat(_playerStats.Coin,0,true);
        }
        private void InitializeStats()
        {
            _health.SetStat(_playerStats.MaxHealth, _playerStats.Health);
            _shield.SetStat(_playerStats.MaxShield, _playerStats.Shield);
            _damage.SetStat(_playerStats.MaxDamage(), _playerStats.MinDamage());
            _exp.SetStat(_playerStats.MaxExp, _playerStats.Exp);
            _coin.SetStat(_playerStats.Coin,0,true);
        }
    }
}