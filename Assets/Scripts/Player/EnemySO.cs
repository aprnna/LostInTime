using UnityEngine;

namespace Player
{
    [CreateAssetMenu(menuName = "Stats/Enemy", fileName = "Enemy Data")]
    public class EnemySO:ScriptableObject
    {
        [SerializeField] private string _name;
        [SerializeField] private Sprite _enemyPotrait;
        [SerializeField] private int _health;
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _baseDamage;
        [SerializeField] private int _intervalDamage;
        [SerializeField] private GameObject _prefab;
        public string EnemyName => _name;
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public int BaseDamage => _baseDamage;
        public int IntervalDamage => _intervalDamage;
        public GameObject Prefab => _prefab;
    }
}