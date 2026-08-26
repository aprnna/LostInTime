using System;
using UnityEngine;

namespace DDA
{
    public enum SimAction
    {
        Punch,    
        Sword,    
        Gun,      
        Defend    
    }

    public struct BattleState
    {
        public int PlayerHP;
        public int PlayerMaxHP;
        public int PlayerShield;
        public int EnemyHP;
        public int EnemyMaxHP;
        public int SwordUsesRemaining;
        public int GunUsesRemaining;
        public int DefendUsesRemaining;
        public int TurnCount;
    }

    public struct DamageResult
    {
        public int Damage;
        public bool QTESuccess; 
    }

    public static class SmartBattleAI
    {
        public static SimAction ChooseAction(BattleState state)
        {
            // Priority 1: Defend if low HP and defend available
            if (state.PlayerHP < state.PlayerMaxHP * 0.3f && state.DefendUsesRemaining > 0)
            {
                // Critical HP - defend to survive
                return SimAction.Defend;
            }

            // Priority 2: Gun to finish low HP enemy
            if (state.GunUsesRemaining > 0)
            {
                float killChance = EstimateKillChance(state, SimAction.Gun);
                if (killChance > 0.7f)
                {
                    return SimAction.Gun;
                }
            }

            // Priority 3: Sword to finish medium HP enemy
            if (state.SwordUsesRemaining > 0)
            {
                float killChance = EstimateKillChance(state, SimAction.Sword);
                if (killChance > 0.7f)
                {
                    return SimAction.Sword;
                }
            }

            // Priority 4: Defend if medium HP and defend available
            if (state.PlayerHP < state.PlayerMaxHP * 0.5f && state.DefendUsesRemaining > 0)
            {
                return SimAction.Defend;
            }

            return WeightedRandomAction(state);
        }

        /// <summary>
        /// Estimate probability of killing enemy with specific action.
        /// </summary>
        private static float EstimateKillChance(BattleState state, SimAction action)
        {
            int estimatedMaxDamage;
            switch (action)
            {
                case SimAction.Gun:
                    estimatedMaxDamage = 13; 
                    break;
                case SimAction.Sword:
                    estimatedMaxDamage = 11;
                    break;
                default:
                    estimatedMaxDamage = 4; 
                    break;
            }

            if (state.EnemyHP <= estimatedMaxDamage)
            {
                return 0.9f;
            }
            else if (state.EnemyHP <= estimatedMaxDamage * 2)
            {
                return 0.6f;
            }
            return 0.2f;
        }

        /// <summary>
        /// Weighted random action selection.
        /// </summary>
        private static SimAction WeightedRandomAction(BattleState state)
        {
            // Weights based on damage and resource conservation
            float swordWeight = state.SwordUsesRemaining > 0 ? 0.30f : 0f;  
            float gunWeight = state.GunUsesRemaining > 0 ? 0.25f : 0f;      
            float defendWeight = (state.DefendUsesRemaining > 0 && state.PlayerHP < state.PlayerMaxHP * 0.7f) ? 0.15f : 0f;
            float punchWeight = 1f - swordWeight - gunWeight - defendWeight;

            float roll = UnityEngine.Random.value;
            if (roll < punchWeight)
            {
                return SimAction.Punch;
            }
            else if (roll < punchWeight + swordWeight)
            {
                return SimAction.Sword;
            }
            else if (roll < punchWeight + swordWeight + gunWeight)
            {
                return SimAction.Gun;
            }
            else
            {
                return SimAction.Defend;
            }
        }

        /// <summary>
        /// Calculate damage for action with absolute value and TapZone simulation.
        /// </summary>
        public static DamageResult CalculateDamageResult(SimAction action, SimPlayer player, float skill = 0.5f)
        {
            // Calculate absolute base damage from percentage
            int baseDamage = action switch
            {
                SimAction.Punch => Mathf.RoundToInt(player.BaseDamage * (player.PunchPercentage / 100f)),
                SimAction.Sword => Mathf.RoundToInt(player.BaseDamage * (player.SwordPercentage / 100f)),
                SimAction.Gun => Mathf.RoundToInt(player.BaseDamage * (player.GunPercentage / 100f)),
                _ => 0
            };

            // TapZone success simulation based on action difficulty + skill
            // Zone widths match actual game BaseAction assets (ZoneWidthPercent)
            float tapZoneSize = action switch
            {
                SimAction.Punch => 0.3f,   // 30% zone = easy (matches Punch.asset)
                SimAction.Sword => 0.2f,   // 20% zone = medium (matches Sword.asset)
                SimAction.Gun => 0.1f,     // 10% zone = hard (matches Gun.asset)
                _ => 0.2f
            };

            // Success probability: skill influence + zone size
            bool tapSuccess = UnityEngine.Random.value < (skill * 0.5f + tapZoneSize);

            if (tapSuccess)
            {
                // +20% critical bonus — matches actual game CriticalBonusPercent (BaseAction)
                baseDamage = Mathf.RoundToInt(baseDamage * 1.2f);
            }

            // No accuracy check - matches actual game (100% hit rate)
            return new DamageResult
            {
                Damage = Mathf.Max(1, baseDamage),
                QTESuccess = tapSuccess
            };
        }

        /// <summary>
        /// Backward-compatible overload returning damage only.
        /// </summary>
        public static int CalculateDamage(SimAction action, SimPlayer player, float skill = 0.5f)
        {
            return CalculateDamageResult(action, player, skill).Damage;
        }

        /// <summary>
        /// Calculate defend value. Defend is flat (no range).
        /// </summary>
        public static int CalculateDefend(SimPlayer player)
        {
            return player.GetDefendValue();
        }

        /// <summary>
        /// Choose level-up bonus based on current state.
        /// Strategy: Prioritize HP if low, else damage for faster clears.
        /// </summary>
        public static SimPlayer.LevelUpChoice ChooseLevelUp(SimPlayer player)
        {
            float hpRatio = player.GetHPRatio();

            // Low HP - prioritize health for survival
            if (hpRatio < 0.4f)
            {
                return SimPlayer.LevelUpChoice.Health;
            }

            // Medium HP - balance between damage and health
            if (hpRatio < 0.6f)
            {
                // 50/50 chance
                return UnityEngine.Random.value < 0.5f
                    ? SimPlayer.LevelUpChoice.Health
                    : SimPlayer.LevelUpChoice.Damage;
            }

            // High HP - prioritize damage for faster clears
            return SimPlayer.LevelUpChoice.Damage;
        }

        /// <summary>
        /// Get bonus amount for level-up choice.
        /// Must match actual game values from LevelUpUIController (ButtonAction._amount in BaseScene).
        /// </summary>
        public static int GetLevelUpBonus(SimPlayer.LevelUpChoice choice)
        {
            return choice switch
            {
                SimPlayer.LevelUpChoice.Damage => 10,  
                SimPlayer.LevelUpChoice.Health => 20, 
                SimPlayer.LevelUpChoice.Shield => 5,   
                _ => 10
            };
        }
    }
}