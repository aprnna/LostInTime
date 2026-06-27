using System;
using UnityEngine;

namespace Player
{
    [CreateAssetMenu(menuName = "Stats/Enemy", fileName = "Enemy Data")]
    public class EnemySO:ScriptableObject
    {
        [SerializeField] private string _name;
        [SerializeField] private Sprite _enemyPotrait;
        [SerializeField] private EnemyType _enemyType;
        [SerializeField] private int _health;
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _baseDamage;
        [SerializeField] private int _intervalDamage;
        [SerializeField] private GameObject _prefab;
        // Gameplay reward granted when this enemy is defeated (exp + coin).
        // Set per enemy asset in Inspector. 0 = grants nothing.
        [SerializeField] private int _expReward;
        [SerializeField] private int _coinReward;
        [SerializeField] private Sprite _rewardIcon;
        public string EnemyName => _name;
        public Sprite EnemyPortrait => _enemyPotrait;
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public int BaseDamage => _baseDamage;
        public int IntervalDamage => _intervalDamage;
        public GameObject Prefab => _prefab;
        public EnemyType EnemyType => _enemyType;
        public int ExpReward => _expReward;
        public int CoinReward => _coinReward;
        public Sprite RewardIcon => _rewardIcon;
    }
}