using UnityEngine;

namespace Player
{
    [CreateAssetMenu(menuName = "Player Data", fileName = "Player/Stats")]
    public class PlayerSO:ScriptableObject
    {
        [SerializeField] private string _name;
        [SerializeField] private Sprite _playerPotrait;
        [SerializeField] private int _health;
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _baseDamage;
        [SerializeField] private int _exp;
        [SerializeField] private int _maxExp;
        [SerializeField] private int _coin;
        [SerializeField] private int _intervalDamage;
        public string PlayerName => _name;
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public int BaseDamage => _baseDamage;
        public int Exp => _exp;
        public int MaxExp => _maxExp;
        public int Coin => _coin;
        public int IntervalDamage => _intervalDamage;
        public void InitializePlayerData(string playerName, int health,int maxHealth, int baseDamage, int exp, int maxExp, int coin)
        {
            _name = playerName;
            _health = health;
            _maxHealth = maxHealth;
            _baseDamage = baseDamage;
            _exp = exp;
            _maxExp = maxExp;
            _coin = coin;
        }

        public void ResetData()
        {
            _name = "";
            _health = 0;
            _maxExp = 0;
            _maxHealth = 0;
            _baseDamage = 0;
            _exp = 0;
            _coin = 0;
            _intervalDamage = 0;
        }

        public void SetName(string playerName)
        {
            _name = PlayerName;
        }

        public void SetHealth(int value)
        {
            _health = value;
        }

        public void SetMaxHealth(int value)
        {
            _maxHealth = value;
        }
        public void SetBaseDamage(int value)
        {
            _baseDamage = value;
        }
        public void SetExp(int value)
        {
            _exp = value;
        }

        public void SetMaxExp(int value)
        {
            _maxExp = value;
        }
        
        public void SetCoin(int value)
        {
            _coin = value;
        }
    }
}