using System;
using UnityEngine;
using Player;

namespace DDA
{
    /// <summary>
    /// Simulated player state for training.
    /// Loads from PlayerSO ScriptableObject and mirrors PlayerStats.
    /// Persists across areas during a training run.
    /// </summary>
    [Serializable]
    public class SimPlayer
    {
        // Base stats (loaded from PlayerSO)
        public int MaxHP;
        public int CurrentHP;
        public int MaxShield;
        public int CurrentShield;
        public int BaseDamage;
        public int IntervalDamage;
        public int BaseDefend;
        public int IntervalDefend;
        public int CriticalHitChance; // Percentage
        public int CriticalHitBonus;   // Percentage extra damage

        // Resources
        public int Coin;
        public int EXP;
        public int MaxEXP;
        public int Level;

        // Action limits (reset per area) - loaded from BaseAction assets
        public int PunchUses;       // Unlimited
        public int SwordUses;       // Limited: 15
        public int GunUses;         // Limited: 10
        public int DefendUses;      // Limited: 3

        public int MaxPunchUses = int.MaxValue;
        public int MaxSwordUses = 15;
        public int MaxGunUses = 10;
        public int MaxDefendUses = 3;

        // Actions config (loaded from BaseAction assets)
        public int PunchPercentage = 30;   // 30% of base damage
        public int PunchInterval = 0;
        public int SwordPercentage = 80;   // 80% of base damage
        public int SwordInterval = 0;
        public int GunPercentage = 120;    // 120% of base damage
        public int GunInterval = 0;
        public int DefendMultiple = 3;     // BaseDefend × 3
        public int DefendInterval = 6;

        /// <summary>Default constructor - uses default values.</summary>
        public SimPlayer()
        {
            LoadFromDefaults();
        }

        /// <summary>Load from PlayerSO ScriptableObject.</summary>
        public SimPlayer(PlayerSO playerSO)
        {
            LoadFromPlayerSO(playerSO);
            LoadActionsFromDefaults();
        }

        /// <summary>Load stats from PlayerSO.</summary>
        private void LoadFromPlayerSO(PlayerSO playerSO)
        {
            MaxHP = playerSO.MaxHealth;
            CurrentHP = MaxHP;
            MaxShield = playerSO.MaxShield;
            CurrentShield = MaxShield;
            BaseDamage = playerSO.BaseDamage;
            IntervalDamage = playerSO.IntervalDamage;
            BaseDefend = playerSO.BaseDefend;
            IntervalDefend = playerSO.IntervalDefend;
            CriticalHitChance = playerSO.CriticalHitPercentage;
            CriticalHitBonus = 20; // Default 20% bonus on crit

            Coin = playerSO.Coin;
            EXP = playerSO.Exp;
            MaxEXP = playerSO.MaxExp;
            Level = 1;

            ResetActionUses();
        }

        /// <summary>Load default values (fallback).</summary>
        private void LoadFromDefaults()
        {
            // Default values from CurrentPlayerData.asset
            MaxHP = 100;
            CurrentHP = MaxHP;
            MaxShield = 2;
            CurrentShield = MaxShield;
            BaseDamage = 12;
            IntervalDamage = 3;
            BaseDefend = 2;
            IntervalDefend = 2;
            CriticalHitChance = 20;
            CriticalHitBonus = 20;

            Coin = 0;
            EXP = 0;
            MaxEXP = 100;
            Level = 1;

            // Default action values from BaseAction assets
            PunchPercentage = 30;
            PunchInterval = 0;
            SwordPercentage = 80;
            SwordInterval = 0;
            GunPercentage = 120;
            GunInterval = 0;
            DefendMultiple = 3;
            DefendInterval = 6;

            ResetActionUses();
        }

        /// <summary>Load action defaults.</summary>
        private void LoadActionsFromDefaults()
        {
            // Action percentages from BaseAction assets
            PunchPercentage = 30;
            PunchInterval = 0;
            SwordPercentage = 80;
            SwordInterval = 0;
            GunPercentage = 120;
            GunInterval = 0;
            DefendMultiple = 3;
            DefendInterval = 6;

            ResetActionUses();
        }

        /// <summary>Reset to starting state for new training run.</summary>
        public void Reset()
        {
            LoadFromDefaults();
        }

        /// <summary>Reset action uses for new area.</summary>
        public void ResetActionUses()
        {
            PunchUses = MaxPunchUses;
            SwordUses = MaxSwordUses;
            GunUses = MaxGunUses;
            DefendUses = MaxDefendUses;
        }

        /// <summary>Take damage, shield absorbs first.</summary>
        public void TakeDamage(int damage)
        {
            if (CurrentShield > 0)
            {
                if (CurrentShield >= damage)
                {
                    CurrentShield -= damage;
                    return;
                }
                else
                {
                    damage -= CurrentShield;
                    CurrentShield = 0;
                }
            }

            CurrentHP = Mathf.Max(0, CurrentHP - damage);
        }

        /// <summary>Heal HP.</summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>Add shield.</summary>
        public void AddShield(int amount)
        {
            CurrentShield = Mathf.Min(MaxShield, CurrentShield + amount);
        }

        /// <summary>Add coins.</summary>
        public void AddCoin(int amount)
        {
            Coin += amount;
        }

        /// <summary>Add EXP, handle level-up.</summary>
        /// <returns>True if level-up occurred.</returns>
        public bool AddExp(int amount)
        {
            EXP += amount;
            if (EXP >= MaxEXP)
            {
                // Level up
                int overflow = EXP - MaxEXP;
                Level++;
                EXP = overflow;
                MaxEXP += 25; // +25 per level
                return true;
            }
            return false;
        }

        /// <summary>Calculate punch damage with variance.</summary>
        public (int min, int max) GetPunchDamageRange()
        {
            int baseDmg = Mathf.RoundToInt(BaseDamage * (PunchPercentage / 100f));
            return (baseDmg - PunchInterval, baseDmg + PunchInterval);
        }

        /// <summary>Calculate sword damage with variance.</summary>
        public (int min, int max) GetSwordDamageRange()
        {
            int baseDmg = Mathf.RoundToInt(BaseDamage * (SwordPercentage / 100f));
            return (baseDmg - SwordInterval, baseDmg + SwordInterval);
        }

        /// <summary>Calculate gun damage with variance.</summary>
        public (int min, int max) GetGunDamageRange()
        {
            int baseDmg = Mathf.RoundToInt(BaseDamage * (GunPercentage / 100f));
            return (baseDmg - GunInterval, baseDmg + GunInterval);
        }

        /// <summary>Calculate defend shield HP with variance.</summary>
        public (int min, int max) GetDefendRange()
        {
            int baseDefendHP = BaseDefend * DefendMultiple;
            return (baseDefendHP - DefendInterval, baseDefendHP + DefendInterval);
        }

        /// <summary>Get HP ratio (0-1).</summary>
        public float GetHPRatio()
        {
            return MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        }

        /// <summary>Check if player is alive.</summary>
        public bool IsAlive()
        {
            return CurrentHP > 0;
        }

        /// <summary>Level-up choices.</summary>
        public enum LevelUpChoice
        {
            Damage,   // +BaseDamage
            Health,   // +MaxHP
            Shield   // +BaseDefend
        }

        /// <summary>Apply level-up bonus.</summary>
        public void ApplyLevelUp(LevelUpChoice choice, int bonus)
        {
            switch (choice)
            {
                case LevelUpChoice.Damage:
                    BaseDamage += bonus;
                    break;
                case LevelUpChoice.Health:
                    MaxHP += bonus;
                    CurrentHP += bonus; // Also heal
                    break;
                case LevelUpChoice.Shield:
                    BaseDefend += bonus;
                    break;
            }
        }
    }
}