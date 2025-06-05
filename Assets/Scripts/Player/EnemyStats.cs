using System;
using UnityEngine;

namespace Player
{
    public class EnemyStats:MonoBehaviour
    {
        [SerializeField] private EnemySO _enemyData;
        private EnemyModel _enemyModel;
        
        private int Health
        {
            get => _enemyModel.Health;
            set
            {
                _enemyModel.Health = value ;
            }
        }
        private int MaxHealth
        {
            get => _enemyModel.MaxHealth;
            set
            {
                _enemyModel.MaxHealth = value;
            }
        }
        private int BaseDamage
        {
            get => _enemyModel.BaseDamage;
            set
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
        public void Start()
        {
            _enemyModel = new EnemyModel(_enemyData);
        }
        public void GetHit(int value)
        {
            if(Health - value > 0) Health -= value;
            else
            {
                Health = 0;
                Debug.Log("You Died");
            }
        }
    }
}