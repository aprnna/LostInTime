using System;
using UnityEngine;

namespace Player
{
    public class PlayerStats:PersistentSingleton<PlayerStats>
    {
        [SerializeField] private PlayerSO _playerData;
        public event Action OnHealthStatsChange;
        public event Action OnShieldStatsChange;
        public event Action OnBaseDamageChange;
        public event Action OnExpStatsChange;
        public event Action OnCoinStatsChange;

        public string Name
        {
            get => _playerData.PlayerName;
            private set
            {
                _playerData.SetName(value);
            }
        }
        public int Health
        {
            get => _playerData.Health;
            private set
            {
                _playerData.SetHealth(value);
                OnHealthStatsChange?.Invoke();
            }
        }
        public int MaxHealth
        {
            get => _playerData.MaxHealth;
            private set
            {
                _playerData.SetMaxHealth(value);
                OnHealthStatsChange?.Invoke();
            }
        }
        public int Shield
        {
            get => _playerData.Shield;
            private set
            {
                _playerData.SetShield(value);
                OnShieldStatsChange?.Invoke();
            }
        }
        public int MaxShield
        {
            get => _playerData.MaxShield;
            private set
            {
                _playerData.SetMaxShield(value);
                OnShieldStatsChange?.Invoke();
            }
        }
        public int BaseDamage
        {
            get => _playerData.BaseDamage;
            private set
            {
                _playerData.SetBaseDamage(value);
                OnBaseDamageChange?.Invoke();
            }
        }
        public int MaxExp
        {
            get => _playerData.MaxExp;
            private set
            {
                _playerData.SetMaxExp(value);
                OnExpStatsChange?.Invoke();
            }
        }
        public int Exp
        {
            get => _playerData.Exp;
            private set
            {
                _playerData.SetExp(value);
                OnExpStatsChange?.Invoke();
            }
        }

        public int Coin
        {
            get => _playerData.Coin;
            private set
            {
                _playerData.SetCoin(value);
                OnCoinStatsChange?.Invoke();
            }
        }

        public int MinDamage()
        {
            return BaseDamage - _playerData.IntervalDamage;
        }

        public int MaxDamage()
        {
            return BaseDamage + _playerData.IntervalDamage;
        }

        public void GetHit(int takeDamage)
        {
            if (Health - takeDamage > 0)
            {
                Health -= takeDamage;
                Debug.Log("You take as much damage as " + Health);
            }
            else
            {
                Health = 0;
                Debug.Log("You Died");
            }
            
        }

        public bool IsAlive()
        {
            return Health > 0;
        }

        public void Heal(int value)
        {
            if (Health + value <= MaxHealth) Health += value;
            else Health = MaxHealth;
        }

        public void PaymentItem(int value)
        {
            Coin -= value;
        }

        public void CollectCoin(int value)
        {
            Coin += value;
        }

        public void AddExp(int value)
        {
            if (Exp + value <= MaxExp) Exp += value;
            else
            {
                var remaining = Exp - value - MaxExp;
                LevelUp(10, 50, remaining, 50, 10);
            };
        }

        public void LevelUp(int health,int maxHealth, int exp,int maxExp, int baseDamage)
        {
            Health += health;
            MaxHealth += maxHealth;
            Exp += exp;
            MaxExp += maxExp;
            BaseDamage += baseDamage;
        }
        public void ResetStats()
        {
            _playerData.ResetData();
        }

        public void InitializeStats(string playerName, int health,int maxHealth,int shield,int maxShield, int baseDamage, int exp, int maxExp,int coin)
        {
            _playerData.InitializePlayerData(playerName, health, maxHealth,shield, maxShield,baseDamage, exp, maxExp,coin);
        }

    }
}