using System;
using UnityEngine;

namespace Player
{
    public class EnemyStats:MonoBehaviour
    {
        [SerializeField] private EnemySO _enemyData;
        private EnemyModel _enemyModel;
        public void Start()
        {
            _enemyModel = new EnemyModel(_enemyData);
        }
        public int Health
        {
            get => _enemyModel.Health;
            private set
            {
                _enemyModel.Health = value ;
            }
        }
        public int MaxHealth
        {
            get => _enemyModel.MaxHealth;
            private set
            {
                _enemyModel.MaxHealth = value;
            }
        }
        public int BaseDamage
        {
            get => _enemyModel.BaseDamage;
            private set
            {
                _enemyModel.BaseDamage = value;
            }
        }
        public int MinDamage()
        {
            return BaseDamage - _enemyModel.IntervalDamage;
        }
        public int MaxDamage()
        {
            return BaseDamage + _enemyModel.IntervalDamage;
        }
        public void GetHit(int value)
        {
            if(Health - value > 0) Health -= value;
            else
            {
                Health = 0;
                Debug.Log("You Died");
                gameObject.SetActive(false);
            }
        }

        public Sprite GetPortrait()
        {
            return _enemyData.EnemyPortrait;
        }
        public bool IsAlive()
        {
            return Health > 0;
        }
    }
}