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
        public event Action OnPlayerLevelUp;
        public bool IsLevelUp { get; private set; }
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
            set
            {
                _playerData.SetExp(value);
                OnExpStatsChange?.Invoke();
            }
        }

        public int Coin
        {
            get => _playerData.Coin;
            set
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

        public void AddShield(int value)
        {
            if (Shield + value <= MaxShield) Shield += value;
            else
            {
                Shield = MaxShield;
                Debug.Log("Max Shield");
                
            }
        }
        public void AddExp(int value)
        {
            if (Exp + value <= MaxExp)
            {
                Exp += value;
            }
            else
            {
                var remaining = Exp + value - MaxExp;
                LevelUp(remaining, 25);
            };
        }

        public void LevelUp(int exp,int maxExp)
        {
            Debug.Log("You Level Up");
            Exp += exp;
            MaxExp += maxExp;
            IsLevelUp = true;
            OnPlayerLevelUp?.Invoke();
        }

        public void LevelUpShield(int value)
        {
            MaxShield += value;
        }
        public void LevelUpHealth(int value)
        {
            MaxHealth += value;
        }
        public void LevelUpDamage(int value)
        {
            BaseDamage += value;
        }

        public void ResetLevelUpStatus()
        {
            IsLevelUp = false;
        }
        public void ResetStats()
        {
            _playerData.ResetData();
        }

        public void InitializeStats(string playerName, int health,int maxHealth,int shield,int maxShield, int baseDamage, int exp, int maxExp,int coin, int interval)
        {
            _playerData.InitializePlayerData(playerName, health, maxHealth,shield, maxShield,baseDamage, exp, maxExp,coin, interval);
            OnHealthStatsChange?.Invoke();
            OnCoinStatsChange?.Invoke();
            OnExpStatsChange?.Invoke();
            OnBaseDamageChange?.Invoke();
            OnShieldStatsChange?.Invoke();
        }

    }
}