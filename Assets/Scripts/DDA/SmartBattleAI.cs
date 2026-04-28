using System;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Action types for battle simulation.
    /// </summary>
    public enum SimAction
    {
        Punch,    // 30% base damage, unlimited
        Sword,    // 80% base damage, 15 uses
        Gun,      // 120% base damage, 10 uses
        Defend    // Gain shield HP, 3 uses
    }

    /// <summary>
    /// Battle state snapshot for AI decisions.
    /// </summary>
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

    /// <summary>
    /// Smart AI for battle action selection.
    /// Prioritizes: survival, finishing enemies, resource management.
    /// </summary>
    public static class SmartBattleAI
    {
        /// <summary>
        /// Choose next action based on battle state.
        /// </summary>
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

            // Priority 5: Weighted random selection
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
                    estimatedMaxDamage = 15; // 120% of ~12 base + variance
                    break;
                case SimAction.Sword:
                    estimatedMaxDamage = 10; // 80% of ~12 base + variance
                    break;
                default:
                    estimatedMaxDamage = 4; // Punch
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
            float swordWeight = state.SwordUsesRemaining > 0 ? 0.30f : 0f;  // Mid damage, conserve
            float gunWeight = state.GunUsesRemaining > 0 ? 0.25f : 0f;      // High damage, limited
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
        /// Calculate damage for action with variance and critical.
        /// Simulates tap-roll mechanic: higher skill = stop closer to max damage.
        /// </summary>
        public static int CalculateDamage(SimAction action, SimPlayer player, float skill = 0.5f)
        {
            int baseDamage;
            int interval;

            switch (action)
            {
                case SimAction.Punch:
                    baseDamage = Mathf.RoundToInt(player.BaseDamage * (player.PunchPercentage / 100f));
                    interval = player.PunchInterval;
                    break;

                case SimAction.Sword:
                    baseDamage = Mathf.RoundToInt(player.BaseDamage * (player.SwordPercentage / 100f));
                    interval = player.SwordInterval;
                    break;

                case SimAction.Gun:
                    baseDamage = Mathf.RoundToInt(player.BaseDamage * (player.GunPercentage / 100f));
                    interval = player.GunInterval;
                    break;

                default:
                    return 0; // Defend deals no damage
            }

            // Tap-roll simulation: skill determines position in damage range
            // Skill 1.0 = always max, skill 0.5 = 50/50, skill 0.0 = pure random
            int minDamage = baseDamage - interval;
            int maxDamage = baseDamage + interval;

            // Weighted roll: skill biases toward max
            float roll = UnityEngine.Random.value;
            float weightedRoll = skill * skill + roll * (1f - skill * skill);
            int damage = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, weightedRoll));

            // Critical hit (player-specific crit chance)
            if (UnityEngine.Random.value < player.CriticalHitChance / 100f)
            {
                damage = Mathf.RoundToInt(damage * (1 + player.CriticalHitBonus / 100f));
            }

            // Accuracy check (85% for player)
            if (UnityEngine.Random.value > 0.85f)
            {
                return 0; // Miss
            }

            return Mathf.Max(1, damage); // Minimum 1 damage
        }

        /// <summary>
        /// Calculate defend shield HP with variance.
        /// </summary>
        public static int CalculateDefend(SimPlayer player)
        {
            int baseDefendHP = player.BaseDefend * player.DefendMultiple;
            int min = baseDefendHP - player.DefendInterval;
            int max = baseDefendHP + player.DefendInterval;
            return UnityEngine.Random.Range(min, max + 1);
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
        /// </summary>
        public static int GetLevelUpBonus(SimPlayer.LevelUpChoice choice)
        {
            return choice switch
            {
                SimPlayer.LevelUpChoice.Damage => 3,   // +3 base damage
                SimPlayer.LevelUpChoice.Health => 15,   // +15 max HP
                SimPlayer.LevelUpChoice.Shield => 1,    // +1 base defend
                _ => 3
            };
        }
    }
}