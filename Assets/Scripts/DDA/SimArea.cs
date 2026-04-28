using System;
using System.Collections.Generic;
using UnityEngine;
using Player;

namespace DDA
{
    /// <summary>
    /// Drop item for simulated rewards.
    /// </summary>
    [Serializable]
    public struct SimDropItem
    {
        public ConsumableType Type;
        public int Amount;

        public SimDropItem(ConsumableType type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        /// <summary>Apply drop to player.</summary>
        public void Apply(SimPlayer player)
        {
            switch (Type)
            {
                case ConsumableType.Health:
                    player.Heal(Amount);
                    break;
                case ConsumableType.Coin:
                    player.AddCoin(Amount);
                    break;
                case ConsumableType.Exp:
                    player.AddExp(Amount);
                    break;
                case ConsumableType.Shield:
                    player.AddShield(Amount);
                    break;
            }
        }
    }

    /// <summary>
    /// Simulated area from MapNode for training progression.
    /// 12 areas per run: Enemy, Rest, Shop, Boss.
    /// </summary>
    [Serializable]
    public class SimArea
    {
        public MapType AreaType;
        public List<SimEnemy> Enemies;
        public List<SimDropItem> Drops;
        public bool IsBossArea;

        /// <summary>Default constructor.</summary>
        public SimArea()
        {
            Enemies = new List<SimEnemy>();
            Drops = new List<SimDropItem>();
        }

        /// <summary>Create from MapNode.</summary>
        public SimArea(MapNode node)
        {
            AreaType = node.mapType;
            Enemies = new List<SimEnemy>();
            Drops = new List<SimDropItem>();
            IsBossArea = node.mapType == MapType.Boss;

            // Load enemies
            if (node.enemies != null)
            {
                foreach (var enemySO in node.enemies)
                {
                    if (enemySO != null)
                    {
                        Enemies.Add(new SimEnemy(enemySO));
                    }
                }
            }

            // Load drops
            if (node.DropItems != null)
            {
                foreach (var drop in node.DropItems)
                {
                    if (drop != null)
                    {
                        Drops.Add(new SimDropItem(drop.Type, drop.Amount));
                    }
                }
            }
        }

        /// <summary>Apply difficulty multipliers to all enemies.</summary>
        public void ApplyDifficulty(float hpMult, float dmgMult)
        {
            foreach (var enemy in Enemies)
            {
                enemy.ApplyDifficulty(hpMult, dmgMult);
            }
        }

        /// <summary>Apply drops to player after winning. Handles level-up.</summary>
        public void ApplyDrops(SimPlayer player)
        {
            foreach (var drop in Drops)
            {
                if (drop.Type == ConsumableType.Exp)
                {
                    // Check for level-up and apply bonus
                    if (player.AddExp(drop.Amount))
                    {
                        var choice = SmartBattleAI.ChooseLevelUp(player);
                        int bonus = SmartBattleAI.GetLevelUpBonus(choice);
                        player.ApplyLevelUp(choice, bonus);
                        Debug.Log($"[SimArea] Level up! Level {player.Level}, Choice: {choice}, Bonus: +{bonus}");
                    }
                }
                else
                {
                    drop.Apply(player);
                }
            }
        }

        /// <summary>Rest area: heal player 10-24 HP.</summary>
        public void ApplyRest(SimPlayer player)
        {
            int healAmount = UnityEngine.Random.Range(10, 25);
            player.Heal(healAmount);
        }

        /// <summary>Shop logic: buy items if smart.</summary>
        public void ApplyShop(SimPlayer player, bool smartBuy = true)
        {
            if (!smartBuy) return;

            // Smart AI: Buy Chicken (heal) if HP < 50% and have coins
            if (player.GetHPRatio() < 0.5f && player.Coin >= 15)
            {
                player.Coin -= 15;
                player.Heal(20);
            }

            // Buy Shield if have extra coins and shield not maxed
            if (player.Coin >= 20 && player.CurrentShield < player.MaxShield)
            {
                player.Coin -= 20;
                player.CurrentShield = Mathf.Min(player.MaxShield, player.CurrentShield + 2);
            }
        }
    }
}