using System;
using UnityEngine;
using Player;

namespace DDA
{
    [Serializable]
    public class SimEnemy
    {
        public string Name;
        public EnemyType Type;
        public int MaxHP;
        public int CurrentHP;
        public int BaseDamage;
        public int IntervalDamage;

        private int _baseMaxHP;
        private int _baseDamage;
        private int _baseIntervalDamage;

        public SimEnemy(EnemySO enemySO)
        {
            Name = enemySO.EnemyName;
            Type = enemySO.EnemyType;
            MaxHP = enemySO.MaxHealth;
            CurrentHP = MaxHP;
            BaseDamage = enemySO.BaseDamage;
            IntervalDamage = enemySO.IntervalDamage;

            // Store base values
            _baseMaxHP = MaxHP;
            _baseDamage = BaseDamage;
            _baseIntervalDamage = IntervalDamage;
        }

        public SimEnemy(string name, EnemyType type, int hp, int baseDmg, int intervalDmg)
        {
            Name = name;
            Type = type;
            MaxHP = hp;
            CurrentHP = hp;
            BaseDamage = baseDmg;
            IntervalDamage = intervalDmg;

            // Store base values
            _baseMaxHP = hp;
            _baseDamage = baseDmg;
            _baseIntervalDamage = intervalDmg;
        }

        public void ApplyDifficulty(float hpMult, float dmgMult)
        {
            // Always apply from base values to avoid compounding
            MaxHP = Mathf.RoundToInt(_baseMaxHP * hpMult);
            CurrentHP = MaxHP;
            BaseDamage = Mathf.RoundToInt(_baseDamage * dmgMult);
            IntervalDamage = Mathf.RoundToInt(_baseIntervalDamage * dmgMult);
        }

        public int GetMinDamage()
        {
            return Mathf.Max(1, BaseDamage - IntervalDamage);
        }

        public int GetMaxDamage()
        {
            return Mathf.Max(2, BaseDamage + IntervalDamage);
        }

        public int CalculateDamage()
        {
            int min = GetMinDamage();
            int max = GetMaxDamage();
            return UnityEngine.Random.Range(min, max + 1);
        }

        public void TakeDamage(int damage)
        {
            CurrentHP = Mathf.Max(0, CurrentHP - damage);
        }

        public bool IsAlive()
        {
            return CurrentHP > 0;
        }

        public float GetHPRatio()
        {
            return MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        }

        public bool IsBoss()
        {
            return Type == EnemyType.trex_basic || Type == EnemyType.triceratops_basic;
        }
    }
}