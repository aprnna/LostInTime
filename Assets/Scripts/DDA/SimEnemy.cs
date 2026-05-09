using System;
using UnityEngine;
using Player;

namespace DDA
{
    /// <summary>
    /// Simulated enemy data for training.
    /// Created from EnemySO ScriptableObject data.
    /// </summary>
    [Serializable]
    public class SimEnemy
    {
        public string Name;
        public EnemyType Type;
        public int MaxHP;
        public int CurrentHP;
        public int BaseDamage;
        public int IntervalDamage;
        public float Accuracy = 0.80f; // Enemy default

        // Base values (original, unmodified) for difficulty reset
        private int _baseMaxHP;
        private int _baseDamage;
        private int _baseIntervalDamage;

        /// <summary>Default constructor.</summary>
        public SimEnemy()
        {
        }

        /// <summary>Create from EnemySO.</summary>
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

        /// <summary>Create with specific values.</summary>
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

        /// <summary>Apply difficulty multipliers from base values.</summary>
        public void ApplyDifficulty(float hpMult, float dmgMult)
        {
            // Always apply from base values to avoid compounding
            MaxHP = Mathf.RoundToInt(_baseMaxHP * hpMult);
            CurrentHP = MaxHP;
            BaseDamage = Mathf.RoundToInt(_baseDamage * dmgMult);
            IntervalDamage = Mathf.RoundToInt(_baseIntervalDamage * dmgMult);
        }

        /// <summary>Get min damage (clamped to 1).</summary>
        public int GetMinDamage()
        {
            return Mathf.Max(1, BaseDamage - IntervalDamage);
        }

        /// <summary>Get max damage.</summary>
        public int GetMaxDamage()
        {
            return Mathf.Max(2, BaseDamage + IntervalDamage);
        }

        /// <summary>Calculate damage with variance.</summary>
        public int CalculateDamage()
        {
            int min = GetMinDamage();
            int max = GetMaxDamage();
            return UnityEngine.Random.Range(min, max + 1);
        }

        /// <summary>Take damage.</summary>
        public void TakeDamage(int damage)
        {
            CurrentHP = Mathf.Max(0, CurrentHP - damage);
        }

        /// <summary>Check if enemy is alive.</summary>
        public bool IsAlive()
        {
            return CurrentHP > 0;
        }

        /// <summary>Get HP ratio (0-1).</summary>
        public float GetHPRatio()
        {
            return MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        }

        /// <summary>Check if enemy is boss type.</summary>
        public bool IsBoss()
        {
            return Type == EnemyType.trex_basic || Type == EnemyType.triceratops_basic;
        }
    }
}