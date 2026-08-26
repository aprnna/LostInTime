using System;
using UnityEngine;
using Player;

namespace DDA
{
    [Serializable]
    public class SimPlayer
    {
        // Base stats (loaded from PlayerSO)
        public int MaxHP;
        public int CurrentHP;
        public int BaseDamage;
        public int BaseDefend;
        public int Defend;          
        public int CriticalHitChance; 
        public int CriticalHitBonus;  

        // Resources
        public int Coin;
        public int EXP;
        public int MaxEXP;
        public int Level;

        // Action limits (reset per area) - loaded from BaseAction assets
        public int PunchUses;      
        public int SwordUses;       
        public int GunUses;        
        public int DefendUses;      

        public int MaxPunchUses = int.MaxValue;
        public int MaxSwordUses = 15;
        public int MaxGunUses = 10;
        public int MaxDefendUses = 2; 

        // Actions config (loaded from BaseAction assets)
        public int PunchPercentage = 30;   
        public int SwordPercentage = 75;   
        public int GunPercentage = 100;    

        public SimPlayer()
        {
            LoadFromDefaults();
        }

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
            BaseDamage = playerSO.BaseDamage;
            BaseDefend = playerSO.BaseDefend;
            Defend = 0;
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
            BaseDamage = 12;
            BaseDefend = 5;
            Defend = 0;
            CriticalHitChance = 20;
            CriticalHitBonus = 20;

            Coin = 0;
            EXP = 0;
            MaxEXP = 100;
            Level = 1;

            // Default action values from BaseAction assets
            PunchPercentage = 30;
            SwordPercentage = 75;
            GunPercentage = 100;

            ResetActionUses();
        }

        /// <summary>Load action defaults.</summary>
        private void LoadActionsFromDefaults()
        {
            // Action percentages from BaseAction assets
            PunchPercentage = 30;
            SwordPercentage = 75;
            GunPercentage = 100;

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

        /// <summary>Take damage, Defend absorbs first then resets to 0.
        /// Matches PlayerStats.GetHit(): Defend absorbs damage, remaining spills to HP, Defend = 0.</summary>
        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;

            // Defend absorbs damage first (matches real game)
            if (Defend > 0)
            {
                Defend -= damage;
                if (Defend < 0)
                {
                    int remainingDamage = Mathf.Abs(Defend);
                    Defend = 0;
                    CurrentHP = Mathf.Max(0, CurrentHP - remainingDamage);
                }
                else
                {
                    Defend = 0; // Reset after absorbing (matches real game)
                }
                return;
            }

            CurrentHP = Mathf.Max(0, CurrentHP - damage);
        }

        /// <summary>Heal HP.</summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>Use a shield charge. Returns true if successful.</summary>
        public bool UseShield()
        {
            if (DefendUses > 0)
            {
                DefendUses--;
                return true;
            }
            return false;
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

        /// <summary>Calculate defend shield HP with variance.</summary>
        public int GetDefendValue()
        {
            return BaseDefend;
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