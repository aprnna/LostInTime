using UnityEngine;

namespace Player
{
    public class PlayerStats:MonoBehaviour
    {
        [SerializeField] private PlayerSO _playerData;

        private string Name
        {
            get => _playerData.PlayerName;
            set
            {
                _playerData.SetName(value);
            }
        }
        private int Health
        {
            get => _playerData.Health;
            set
            {
                _playerData.SetHealth(value);
            }
        }
        private int MaxHealth
        {
            get => _playerData.MaxHealth;
            set
            {
                _playerData.SetMaxHealth(value);
            }
        }
        private int BaseDamage
        {
            get => _playerData.BaseDamage;
            set
            {
                _playerData.SetBaseDamage(value);
            }
        }
        private int MaxExp
        {
            get => _playerData.MaxExp;
            set
            {
                _playerData.SetMaxExp(value);
            }
        }
        private int Exp
        {
            get => _playerData.Exp;
            set
            {
                _playerData.SetExp(value);
            }
        }

        private int Coin
        {
            get => _playerData.Coin;
            set
            {
                _playerData.SetCoin(value);
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

        private void GetHit(int takeDamage)
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

        public void InitializeStats(string playerName, int health,int maxHealth, int baseDamage, int exp, int maxExp,int coin)
        {
            _playerData.InitializePlayerData(playerName, health, maxHealth,baseDamage, exp, maxExp,coin);
        }

    }
}