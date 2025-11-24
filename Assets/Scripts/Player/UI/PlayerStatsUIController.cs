using System;
using UnityEngine;

namespace Player.UI
{
    public class PlayerStatsUIController:MonoBehaviour
    {
        [SerializeField] private GameObject _potraitPlayer;
        [SerializeField] private GameObject _damagePopUpPrefab;
        [SerializeField] private StatsItem _health;
        [SerializeField] private StatsItem _defend;
        [SerializeField] private StatsItem _damage;
        [SerializeField] private StatsItem _exp;
        [SerializeField] private StatsItem _coin;

        private PlayerStats _playerStats;
        private Animator _playerAnimator;
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
            _playerStats.OnDefendPlayerChange += OnChangeDefend;
            _playerStats.OnPlayerGetDamage += PlayerGetDamageVFX;
        }

        private void OnDisable()
        {
            _playerStats.OnHealthStatsChange -= OnChangeHealth;
            _playerStats.OnCoinStatsChange -= OnChangeCoin;
            _playerStats.OnExpStatsChange -= OnChangeExp;
            _playerStats.OnBaseDamageChange -= OnChangeDamage;
            _playerStats.OnDefendPlayerChange -= OnChangeDefend;
            _playerStats.OnPlayerGetDamage -= PlayerGetDamageVFX;
        }
        
        public void PlayerGetDamageVFX(int damage, bool isCritical)
        {
            _playerAnimator.SetTrigger("GetHit");
            DamagePopup.Create(_damagePopUpPrefab.transform, _potraitPlayer.transform.position, damage, isCritical, _potraitPlayer.transform.parent.parent);
        }

        private void OnChangeHealth()
        {
            _health.SetStat(_playerStats.MaxHealth, _playerStats.Health);
        }

        private void OnChangeDefend()
        {
            _defend.SetStat(_playerStats.Defend, 0,true);
        }

        private void OnChangeDamage()
        {
            _damage.SetStat(_playerStats.BaseDamage,0 ,true);
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
            _defend.SetStat(_playerStats.Defend, 0, true);
            _damage.SetStat(_playerStats.BaseDamage, 0,true);
            _exp.SetStat(_playerStats.MaxExp, _playerStats.Exp);
            _coin.SetStat(_playerStats.Coin,0,true);
            _playerAnimator = _potraitPlayer.GetComponent<Animator>();
        }
    }
}