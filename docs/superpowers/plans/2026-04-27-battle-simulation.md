# Game-Accurate Battle Simulation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update TrainingBattleSimulator to match actual game mechanics for ML-Agents DDA training, including proper action system, damage roulette, enemy variety, area progression, and persistent player stats.

**Architecture:** Simulated player/enemy/area data structures load from game's ScriptableObjects (PlayerSO, EnemySO, BaseAction). Smart AI selects from 4 actions (Punch, Sword, Gun, Defend). Training runs 12-area map sequence with rest/shop handling. Stats persist across areas, level-up auto-selects upgrades.

**Tech Stack:** Unity C#, Resources.Load for ScriptableObjects, IEnumerator coroutine for battle flow, existing DDAAgent integration

**Actions (from BaseAction assets):**
| Action | Damage | Uses | Notes |
|--------|--------|------|-------|
| Punch | 30% × baseDMG | ∞ | Unlimited basic attack |
| Sword | 80% × baseDMG | 15 | Limited mid-damage attack |
| Gun | 120% × baseDMG | 10 | Limited high-damage attack |
| Defend | 3× baseDefend ± 6 | 3 | Shield HP absorbs damage |

**Enemies (from EnemySO assets):**
| Enemy | HP | DMG Range | Type |
|--------|-----|---------|------|
| Caveman | 15 | 4-8 | Normal |
| Sabertooth | 18 | 5-11 | Normal |
| Raptor | 24 | 4-14 | Normal |
| Trex | 35 | 15-25 | Boss |
| Triceratops | 40 | 14-26 | Boss |

---

## File Structure

```
Assets/Scripts/DDA/
├── TrainingBattleSimulator.cs    # UPDATE: Full game-accurate simulation
├── TrainingUIDisplay.cs          # UPDATE: Add area progress, action stats
├── DDAAgent.cs                   # UPDATE: Add area/progression observations
├── SimPlayer.cs                  # CREATE: Simulated player state
├── SimEnemy.cs                   # CREATE: Simulated enemy data
├── SimArea.cs                    # CREATE: Simulated area data
└── SmartBattleAI.cs              # CREATE: Action selection logic
```

---

## Task 1: Create SimPlayer Data Structure

**Files:**
- Create: `Assets/Scripts/DDA/SimPlayer.cs`

**Purpose:** Simulated player state that persists across areas, mirroring PlayerStats but optimized for training.

- [ ] **Step 1: Create SimPlayer struct**

Create `Assets/Scripts/DDA/SimPlayer.cs`:

```csharp
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
```

- [ ] **Step 2: Commit SimPlayer**

```bash
git add Assets/Scripts/DDA/SimPlayer.cs
git commit -m "$(cat <<'EOF'
feat(dda): add SimPlayer for training simulation

SimPlayer mirrors PlayerStats for fast training:
- HP, Shield, Damage, Defend stats
- Action uses tracking (Gun: 10, Defend: 3)
- Damage/defend variance calculation
- Level-up system with choice selection
- Resource tracking (Coin, EXP)

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Create SimEnemy Data Structure

**Files:**
- Create: `Assets/Scripts/DDA/SimEnemy.cs`

**Purpose:** Simulated enemy from EnemySO data for training.

- [ ] **Step 1: Create SimEnemy struct**

Create `Assets/Scripts/DDA/SimEnemy.cs`:

```csharp
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
        }

        /// <summary>Apply difficulty multipliers.</summary>
        public void ApplyDifficulty(float hpMult, float dmgMult)
        {
            MaxHP = Mathf.RoundToInt(MaxHP * hpMult);
            CurrentHP = MaxHP;
            BaseDamage = Mathf.RoundToInt(BaseDamage * dmgMult);
        }

        /// <summary>Get min damage.</summary>
        public int GetMinDamage()
        {
            return BaseDamage - IntervalDamage;
        }

        /// <summary>Get max damage.</summary>
        public int GetMaxDamage()
        {
            return BaseDamage + IntervalDamage;
        }

        /// <summary>Calculate damage with variance.</summary>
        public int CalculateDamage()
        {
            int min = GetMinDamage();
            int max = GetMaxDamage();
            return Random.Range(min, max + 1);
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
            return Type == EnemyType.Boss;
        }
    }
}
```

- [ ] **Step 2: Commit SimEnemy**

```bash
git add Assets/Scripts/DDA/SimEnemy.cs
git commit -m "$(cat <<'EOF'
feat(dda): add SimEnemy for training simulation

SimEnemy wraps EnemySO data for training:
- HP, damage, interval damage from EnemySO
- ApplyDifficulty multiplies HP/damage
- Damage calculation with variance
- IsBoss check for boss-type enemies

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Create SimArea Data Structure

**Files:**
- Create: `Assets/Scripts/DDA/SimArea.cs`

**Purpose:** Simulated area/node from MapNode data for training progression.

- [ ] **Step 1: Create SimArea struct**

Create `Assets/Scripts/DDA/SimArea.cs`:

```csharp
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

        /// <summary>Apply drops to player after winning.</summary>
        public void ApplyDrops(SimPlayer player)
        {
            foreach (var drop in Drops)
            {
                drop.Apply(player);
            }
        }

        /// <summary>Rest area: heal player 10-24 HP.</summary>
        public void ApplyRest(SimPlayer player)
        {
            int healAmount = Random.Range(10, 25);
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
```

- [ ] **Step 2: Commit SimArea**

```bash
git add Assets/Scripts/DDA/SimArea.cs
git commit -m "$(cat <<'EOF'
feat(dda): add SimArea for training progression

SimArea wraps MapNode data for training:
- Enemy list with difficulty scaling
- Drop items (Coin, Exp, Shield, Health)
- Rest area: 10-24 HP heal
- Shop area: smart AI purchases
- Boss area detection

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Create SmartBattleAI

**Files:**
- Create: `Assets/Scripts/DDA/SmartBattleAI.cs`

**Purpose:** Action selection logic for simulated player with 4 actions (Punch, Sword, Gun, Defend).

- [ ] **Step 1: Create SmartBattleAI class**

Create `Assets/Scripts/DDA/SmartBattleAI.cs`:

```csharp
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

            float roll = Random.value;
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
        /// </summary>
        public static int CalculateDamage(SimAction action, SimPlayer player)
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

            // Variance: base ± interval
            int damage = Random.Range(baseDamage - interval, baseDamage + interval + 1);

            // Critical hit (20% chance)
            if (Random.value < player.CriticalHitChance / 100f)
            {
                damage = Mathf.RoundToInt(damage * (1 + player.CriticalHitBonus / 100f));
            }

            // Accuracy check (85% for player)
            if (Random.value > 0.85f)
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
            return Random.Range(min, max + 1);
        }
    }
}
```

- [ ] **Step 2: Commit SmartBattleAI**

```bash
git add Assets/Scripts/DDA/SmartBattleAI.cs
git commit -m "$(cat <<'EOF'
feat(dda): add SmartBattleAI with 4 action types

SmartBattleAI chooses actions for training:
- 4 actions: Punch (30%, unlimited), Sword (80%, 15 uses), Gun (120%, 10 uses), Defend (3 uses)
- Priority: survival (defend at low HP)
- Finish enemy with Gun/Sword if kill chance > 70%
- Weighted random for normal turns
- Damage calculation with variance + critical

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Update TrainingBattleSimulator

**Files:**
- Modify: `Assets/Scripts/DDA/TrainingBattleSimulator.cs`

**Purpose:** Replace simple simulation with game-accurate mechanics.

- [ ] **Step 1: Read current TrainingBattleSimulator**

Current implementation has basic player/enemy simulation. Need to replace with SimPlayer, SimEnemy, SimArea, and SmartBattleAI.

- [ ] **Step 2: Update TrainingBattleSimulator imports and fields**

Replace the field definitions in `TrainingBattleSimulator.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player;

namespace DDA
{
    /// <summary>
    /// Simulates battles automatically for ML-Agents training.
    /// Game-accurate mechanics: actions, damage roulette, area progression.
    /// </summary>
    public class TrainingBattleSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DDAAgent _ddaAgent;
        [SerializeField] private DifficultySettings _difficultySettings;
        [SerializeField] private DifficultyApplier _difficultyApplier;

        [Header("Area Data")]
        [SerializeField] private MapData _mapData;
        [SerializeField] private bool _loadFromMapData = true;

        [Header("Player Config")]
        [SerializeField] private int _playerBaseHP = 100;
        [SerializeField] private int _playerBaseDamage = 12;
        [SerializeField] private int _playerIntervalDamage = 3;
        [SerializeField] private float _playerAccuracy = 0.85f;

        [Header("Training Configuration")]
        [SerializeField] private bool _autoTrain = true;
        [SerializeField] private float _battleDelay = 0.1f;
        [SerializeField] private float _turnDelay = 0.05f;
        [SerializeField] private int _maxTurnsPerBattle = 20;
        [SerializeField] private bool _useSmartAI = true;
        [SerializeField] [Range(0f, 1f)] private float _playerSkill = 0.5f;

        [Header("Run Configuration")]
        [SerializeField] private int _areasPerRun = 12;
        [SerializeField] private bool _resetOnRunComplete = true;

        // Simulated state
        private SimPlayer _player;
        private List<SimArea> _areas;
        private SimEnemy _currentEnemy;
        private int _currentAreaIndex;
        private int _enemyIndex; // Current enemy in area
        private int _turnCount;
        private bool _battleInProgress;
        private bool _runInProgress;

        // Training stats
        private int _episodeCount;
        private int _runCount;
        private int _winCount;
        private int _lossCount;
        private int _totalTurns;
        private float _totalReward;
        private float _lastReward;
        private int _consecutiveWins;
        private int _consecutiveLosses;

        // Events for UI
        public event Action<int, int, int> OnBattleStateChanged; // (playerHP, enemyHP, turn)
        public event Action<bool, float, int> OnBattleEnded; // (won, reward, episode)
        public event Action<int> OnDifficultyChanged; // (level)
        public event Action<int, int> OnAreaChanged; // (areaIndex, totalAreas)
        public event Action<TrainingStats> OnStatsUpdated;
        public event Action<RunResult> OnRunComplete;

        public static TrainingBattleSimulator Instance { get; private set; }

        // Public properties
        public int EpisodeCount => _episodeCount;
        public int WinCount => _winCount;
        public float WinRate => _episodeCount > 0 ? (float)_winCount / _episodeCount : 0f;
        public float AvgReward => _episodeCount > 0 ? _totalReward / _episodeCount : 0f;
        public float LastReward => _lastReward;
        public int CurrentDifficulty => _difficultySettings?.CurrentLevelIndex ?? 2;
        public string DifficultyName => _difficultySettings?.GetLevelName() ?? "Normal";
        public int CurrentArea => _currentAreaIndex + 1;
        public int TotalAreas => _areas?.Count ?? _areasPerRun;
        public string CurrentEnemyName => _currentEnemy?.Name ?? "None";
        public SimPlayer Player => _player;
```

- [ ] **Step 3: Add Awake and Start with initialization**

```csharp
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Load references
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }

            if (_ddaAgent == null)
            {
                _ddaAgent = FindObjectOfType<DDAAgent>();
            }

            // Initialize areas
            InitializeAreas();

            // Initialize player
            _player = new SimPlayer();

            // Start training
            if (_autoTrain)
            {
                StartCoroutine(TrainingLoop());
            }
        }

        private void InitializeAreas()
        {
            _areas = new List<SimArea>();

            if (_loadFromMapData && _mapData != null)
            {
                // Load from MapData ScriptableObject
                foreach (var node in _mapData.mapItems)
                {
                    if (node != null)
                    {
                        _areas.Add(new SimArea(node));
                    }
                }
                Debug.Log($"[TrainingSim] Loaded {_areas.Count} areas from MapData");
            }
            else
            {
                // Generate default 12-area sequence
                GenerateDefaultAreas();
            }
        }

        private void GenerateDefaultAreas()
        {
            // Default sequence based on design spec
            // 12 areas: Enemy x5, Rest x2, Shop x2, Boss x1
            _areas.Add(CreateEnemyArea("Caveman", EnemyType.Normal, 15, 6, 2)); // Enemy1
            _areas.Add(CreateEnemyArea("Sabertooth+Caveman", EnemyType.Normal, 18, 8, 3, addSecondEnemy: true)); // Enemy2
            _areas.Add(CreateRestArea()); // Rest1
            _areas.Add(CreateEnemyArea("Sabertooth+Caveman", EnemyType.Normal, 18, 8, 3, addSecondEnemy: true)); // Enemy3
            _areas.Add(CreateEnemyArea("Raptor x2", EnemyType.Normal, 24, 9, 5, addSecondEnemy: true)); // Enemy4
            _areas.Add(CreateEnemyArea("Raptor x2", EnemyType.Normal, 24, 9, 5, addSecondEnemy: true)); // Enemy5
            _areas.Add(CreateShopArea()); // Shop1
            _areas.Add(CreateEnemyArea("Raptor+Sabertooth+Caveman", EnemyType.Normal, 24, 9, 5, addSecondEnemy: true, addThirdEnemy: true)); // Enemy6
            _areas.Add(CreateEnemyArea("Raptor+Sabertooth+Caveman", EnemyType.Normal, 24, 9, 5, addSecondEnemy: true, addThirdEnemy: true)); // Enemy7
            _areas.Add(CreateRestArea()); // Rest2
            _areas.Add(CreateShopArea()); // Shop2
            _areas.Add(CreateBossArea("Trex", 35, 20, 5)); // Boss

            Debug.Log($"[TrainingSim] Generated {_areas.Count} default areas");
        }

        private SimArea CreateEnemyArea(string name, EnemyType type, int hp, int baseDmg, int interval,
            bool addSecondEnemy = false, bool addThirdEnemy = false)
        {
            var area = new SimArea
            {
                AreaType = MapType.Enemy,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>
                {
                    new SimDropItem(ConsumableType.Coin, 30),
                    new SimDropItem(ConsumableType.Exp, 15)
                }
            };

            area.Enemies.Add(new SimEnemy(name, type, hp, baseDmg, interval));

            if (addSecondEnemy)
            {
                area.Enemies.Add(new SimEnemy("Sabertooth", EnemyType.Normal, 18, 8, 3));
            }

            if (addThirdEnemy)
            {
                area.Enemies.Add(new SimEnemy("Caveman", EnemyType.Normal, 15, 6, 2));
            }

            return area;
        }

        private SimArea CreateRestArea()
        {
            return new SimArea
            {
                AreaType = MapType.Rest,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>() // Rest heals 10-24 HP, not drops
            };
        }

        private SimArea CreateShopArea()
        {
            return new SimArea
            {
                AreaType = MapType.Shop,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>()
            };
        }

        private SimArea CreateBossArea(string name, int hp, int baseDmg, int interval)
        {
            var area = new SimArea
            {
                AreaType = MapType.Boss,
                IsBossArea = true,
                Enemies = new List<SimEnemy>(),
                Drops = new List<SimDropItem>
                {
                    new SimDropItem(ConsumableType.Coin, 50),
                    new SimDropItem(ConsumableType.Exp, 20)
                }
            };

            area.Enemies.Add(new SimEnemy(name, EnemyType.Boss, hp, baseDmg, interval));

            return area;
        }
```

- [ ] **Step 4: Add training loop coroutine**

```csharp
        private IEnumerator TrainingLoop()
        {
            while (true)
            {
                if (!_runInProgress)
                {
                    yield return StartCoroutine(RunTrainingRun());
                }
                yield return new WaitForSeconds(_battleDelay);
            }
        }

        private IEnumerator RunTrainingRun()
        {
            _runInProgress = true;
            _currentAreaIndex = 0;
            _runCount++;

            // Reset player for new run
            _player.Reset();

            Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

            // Process each area
            while (_currentAreaIndex < _areas.Count && _player.IsAlive())
            {
                yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));

                if (!_player.IsAlive())
                {
                    break; // Player died, end run
                }

                _currentAreaIndex++;
                OnAreaChanged?.Invoke(_currentAreaIndex, _areas.Count);
            }

            // Run complete
            bool runWon = _player.IsAlive() && _currentAreaIndex >= _areas.Count;

            var runResult = new RunResult
            {
                RunNumber = _runCount,
                AreasCompleted = _currentAreaIndex,
                TotalAreas = _areas.Count,
                Won = runWon,
                FinalHP = _player.CurrentHP,
                Coin = _player.Coin,
                Level = _player.Level
            };

            OnRunComplete?.Invoke(runResult);

            Debug.Log($"[TrainingSim] Run {_runCount} complete. Won: {runWon}, " +
                      $"Areas: {_currentAreaIndex}/{_areas.Count}, HP: {_player.CurrentHP}");

            if (_resetOnRunComplete)
            {
                _player.Reset();
            }

            _runInProgress = false;
        }

        private IEnumerator ProcessArea(SimArea area)
        {
            // Apply difficulty to enemies
            float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
            float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
            area.ApplyDifficulty(hpMult, dmgMult);

            switch (area.AreaType)
            {
                case MapType.Enemy:
                case MapType.Boss:
                    yield return StartCoroutine(ProcessBattleArea(area));
                    break;

                case MapType.Rest:
                    ProcessRestArea(area);
                    break;

                case MapType.Shop:
                    ProcessShopArea(area);
                    break;
            }
        }

        private IEnumerator ProcessBattleArea(SimArea area)
        {
            // Fight all enemies in sequence
            foreach (var enemy in area.Enemies)
            {
                if (!_player.IsAlive())
                {
                    yield break;
                }

                _currentEnemy = enemy;
                yield return StartCoroutine(RunBattleEpisode());

                if (!_player.IsAlive())
                {
                    yield break; // Lost, don't process drops
                }
            }

            // Apply drops after winning
            area.ApplyDrops(_player);

            // Reset action uses for next area
            _player.ResetActionUses();
        }

        private void ProcessRestArea(SimArea area)
        {
            int healAmount = Random.Range(10, 25);
            _player.Heal(healAmount);

            Debug.Log($"[TrainingSim] Rest area: Healed {healAmount} HP. " +
                      $"HP: {_player.CurrentHP}/{_player.MaxHP}");
        }

        private void ProcessShopArea(SimArea area)
        {
            // Smart AI shopping
            area.ApplyShop(_player, _useSmartAI);

            Debug.Log($"[TrainingSim] Shop area: Coin={_player.Coin}, " +
                      $"Shield={_player.CurrentShield}/{_player.MaxShield}");
        }
```

- [ ] **Step 5: Add battle episode coroutine**

```csharp
        private IEnumerator RunBattleEpisode()
        {
            _battleInProgress = true;
            _episodeCount++;
            _turnCount = 0;

            // Notify DDA agent battle starting
            _ddaAgent?.OnBattleStart(_player.CurrentHP);

            // Difficulty changed callback
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged += HandleDifficultyChanged;
            }

            Debug.Log($"[TrainingSim] Episode {_episodeCount} started. " +
                      $"Enemy: {_currentEnemy.Name} (HP: {_currentEnemy.MaxHP}), " +
                      $"Difficulty: {DifficultyName}");

            // Run battle turns
            while (_player.IsAlive() && _currentEnemy.IsAlive() && _turnCount < _maxTurnsPerBattle)
            {
                yield return StartCoroutine(RunTurn());
                yield return new WaitForSeconds(_turnDelay);
            }

            // Determine outcome
            bool playerWon = _currentEnemy.CurrentHP <= 0 && _player.IsAlive();
            int playerEndHP = _player.CurrentHP;

            // Calculate reward
            _lastReward = CalculateReward(playerWon, playerEndHP, _player.MaxHP, _turnCount);

            // Update stats
            if (playerWon)
            {
                _winCount++;
                _consecutiveWins++;
                _consecutiveLosses = 0;
            }
            else
            {
                _lossCount++;
                _consecutiveLosses++;
                _consecutiveWins = 0;
            }

            _totalTurns += _turnCount;
            _totalReward += _lastReward;

            // Notify DDA agent battle ended
            _ddaAgent?.OnBattleEnd(playerWon, playerEndHP);

            // Unsubscribe
            if (_ddaAgent != null)
            {
                _ddaAgent.OnDifficultyChanged -= HandleDifficultyChanged;
            }

            // Fire events for UI
            OnBattleEnded?.Invoke(playerWon, _lastReward, _episodeCount);
            OnStatsUpdated?.Invoke(GetStats());

            Debug.Log($"[TrainingSim] Episode {_episodeCount} ended. " +
                      $"Won: {playerWon}, HP: {playerEndHP}/{_player.MaxHP}, " +
                      $"Turns: {_turnCount}, Reward: {_lastReward:F3}, " +
                      $"WinRate: {WinRate:P1}");

            _battleInProgress = false;
        }

        private IEnumerator RunTurn()
        {
            _turnCount++;

            // --- Player Turn ---
            int playerDamage = ExecutePlayerTurn();
            if (playerDamage > 0)
            {
                _currentEnemy.TakeDamage(playerDamage);
            }

            OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

            if (!_currentEnemy.IsAlive())
            {
                yield break; // Enemy defeated
            }

            yield return new WaitForSeconds(_turnDelay / 2f);

            // --- Enemy Turn ---
            int enemyDamage = ExecuteEnemyTurn();
            if (enemyDamage > 0)
            {
                _player.TakeDamage(enemyDamage);
            }

            OnBattleStateChanged?.Invoke(_player.CurrentHP, _currentEnemy.CurrentHP, _turnCount);

            // Notify DDA agent turn ended
            _ddaAgent?.OnTurnEnd(playerDamage);
        }

        private int ExecutePlayerTurn()
        {
            // Build battle state for AI
            var state = new BattleState
            {
                PlayerHP = _player.CurrentHP,
                PlayerMaxHP = _player.MaxHP,
                PlayerShield = _player.CurrentShield,
                EnemyHP = _currentEnemy.CurrentHP,
                EnemyMaxHP = _currentEnemy.MaxHP,
                SwordUsesRemaining = _player.SwordUses,
                GunUsesRemaining = _player.GunUses,
                DefendUsesRemaining = _player.DefendUses,
                TurnCount = _turnCount
            };

            // Choose action
            SimAction action;
            if (_useSmartAI)
            {
                action = SmartBattleAI.ChooseAction(state);
            }
            else
            {
                // Random action
                action = GetRandomAction(state);
            }

            // Execute action
            int damage = 0;
            switch (action)
            {
                case SimAction.Punch:
                    damage = SmartBattleAI.CalculateDamage(SimAction.Punch, _player);
                    break;

                case SimAction.Sword:
                    if (_player.SwordUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Sword, _player);
                        _player.SwordUses--;
                    }
                    break;

                case SimAction.Gun:
                    if (_player.GunUses > 0)
                    {
                        damage = SmartBattleAI.CalculateDamage(SimAction.Gun, _player);
                        _player.GunUses--;
                    }
                    break;

                case SimAction.Defend:
                    if (_player.DefendUses > 0)
                    {
                        int shieldHP = SmartBattleAI.CalculateDefend(_player);
                        _player.CurrentShield = Mathf.Min(_player.MaxShield, _player.CurrentShield + shieldHP);
                        _player.DefendUses--;
                    }
                    break;
            }

            return damage;
        }

        private SimAction GetRandomAction(BattleState state)
        {
            // Weighted random with skill influence
            float skill = _playerSkill;
            float punchWeight = 0.35f - skill * 0.15f;  // Less punch at high skill
            float swordWeight = state.SwordUsesRemaining > 0 ? 0.25f + skill * 0.1f : 0f;
            float gunWeight = state.GunUsesRemaining > 0 ? 0.25f + skill * 0.1f : 0f;
            float defendWeight = state.DefendUsesRemaining > 0 ? 0.15f : 0f;

            // Normalize
            float total = punchWeight + swordWeight + gunWeight + defendWeight;
            if (total > 0)
            {
                punchWeight /= total;
                swordWeight /= total;
                gunWeight /= total;
                defendWeight /= total;
            }

            float roll = Random.value;
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

        private int ExecuteEnemyTurn()
        {
            // Accuracy check (80% for enemy)
            if (Random.value > 0.80f)
            {
                return 0; // Miss
            }

            // Calculate damage with variance
            int damage = _currentEnemy.CalculateDamage();
            return Mathf.Max(1, damage);
        }
```

- [ ] **Step 6: Add reward calculation and public methods**

```csharp
        private float CalculateReward(bool playerWon, int playerEndHP, int playerStartHP, int turns)
        {
            float reward = 0f;

            if (playerWon)
            {
                // Base win reward
                reward += 1.0f;

                // Calculate HP ratio
                float hpRatio = (float)playerEndHP / playerStartHP;

                // HP Target bonus (flow state: 40-60% HP remaining)
                if (hpRatio >= 0.4f && hpRatio <= 0.6f)
                {
                    float distanceFromOptimal = Mathf.Abs(hpRatio - 0.5f);
                    float hpBonus = 0.5f * (1.0f - distanceFromOptimal * 2f);
                    reward += hpBonus;
                }
                else if (hpRatio > 0.6f)
                {
                    // Too easy - penalty
                    float excess = hpRatio - 0.6f;
                    reward -= 0.1f * excess;
                }
                else if (hpRatio < 0.4f)
                {
                    // Too hard - penalty
                    float deficit = 0.4f - hpRatio;
                    reward -= 0.1f * deficit;
                }
            }
            else
            {
                // Loss penalty
                reward -= 1.0f;
            }

            // Efficiency penalty (turns over expected ~10)
            int expectedTurns = 10;
            if (turns > expectedTurns)
            {
                float efficiencyPenalty = 0.01f * (turns - expectedTurns);
                reward -= efficiencyPenalty;
            }

            return Mathf.Clamp(reward, -2f, 2f);
        }

        private void HandleDifficultyChanged(int newLevel)
        {
            OnDifficultyChanged?.Invoke(newLevel);
            Debug.Log($"[TrainingSim] Difficulty changed to: {DifficultyName}");
        }

        /// <summary>
        /// Gets current training statistics.
        /// </summary>
        public TrainingStats GetStats()
        {
            return new TrainingStats
            {
                EpisodeCount = _episodeCount,
                WinCount = _winCount,
                WinRate = WinRate,
                AvgReward = AvgReward,
                LastReward = _lastReward,
                TotalReward = _totalReward,
                CurrentDifficulty = CurrentDifficulty,
                DifficultyName = DifficultyName,
                ConsecutiveWins = _consecutiveWins,
                ConsecutiveLosses = _consecutiveLosses,
                AvgTurnsPerBattle = _episodeCount > 0 ? (float)_totalTurns / _episodeCount : 0f,
            };
        }

        /// <summary>
        /// Resets all training statistics.
        /// </summary>
        public void ResetStats()
        {
            _episodeCount = 0;
            _winCount = 0;
            _lossCount = 0;
            _runCount = 0;
            _totalTurns = 0;
            _totalReward = 0;
            _lastReward = 0;
            _consecutiveWins = 0;
            _consecutiveLosses = 0;
            _currentAreaIndex = 0;
        }

        /// <summary>
        /// Sets player skill level for simulation.
        /// </summary>
        public void SetPlayerSkill(float skill)
        {
            _playerSkill = Mathf.Clamp01(skill);
        }

        /// <summary>
        /// Enables or disables auto training.
        /// </summary>
        public void SetAutoTrain(bool enabled)
        {
            _autoTrain = enabled;
        }

        /// <summary>
        /// Sets use smart AI flag.
        /// </summary>
        public void SetUseSmartAI(bool useSmart)
        {
            _useSmartAI = useSmart;
        }
    }

    /// <summary>
    /// Training statistics data structure for UI display.
    /// </summary>
    [Serializable]
    public struct TrainingStats
    {
        public int EpisodeCount;
        public int WinCount;
        public float WinRate;
        public float AvgReward;
        public float LastReward;
        public float TotalReward;
        public int CurrentDifficulty;
        public string DifficultyName;
        public int ConsecutiveWins;
        public int ConsecutiveLosses;
        public float AvgTurnsPerBattle;
    }

    /// <summary>
    /// Result of a training run (12 areas).
    /// </summary>
    [Serializable]
    public struct RunResult
    {
        public int RunNumber;
        public int AreasCompleted;
        public int TotalAreas;
        public bool Won;
        public int FinalHP;
        public int Coin;
        public int Level;
    }
}
```

- [ ] **Step 7: Commit updated TrainingBattleSimulator**

```bash
git add Assets/Scripts/DDA/TrainingBattleSimulator.cs
git commit -m "$(cat <<'EOF'
feat(dda): update TrainingBattleSimulator with game-accurate mechanics

Replaces simple simulation with full game mechanics:
- SimPlayer/SimEnemy/SimArea data structures
- SmartBattleAI for 4 action types (Punch, Sword, Gun, Defend)
- 12-area map progression (Enemy, Rest, Shop, Boss)
- Damage roulette with variance and critical hits
- Action limits (Sword: 15, Gun: 10, Defend: 3)
- Persistent stats across areas
- Level-up system
- Rest (10-24 HP heal) and Shop (smart buying)
- DDA integration for difficulty scaling

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Update DDAAgent Observations

**Files:**
- Modify: `Assets/Scripts/DDA/DDAAgent.cs`

**Purpose:** Add area/progression observations for better DDA decisions.

- [ ] **Step 1: Add area tracking fields to DDAAgent**

Add after existing fields in `DDAAgent.cs`:

```csharp
        // Area progression tracking
        private int _currentArea;
        private int _totalAreas;
        private int _areasWon;
```

- [ ] **Step 2: Add observation for area progress**

Update `CollectObservations` method:

```csharp
        public override void CollectObservations(VectorSensor sensor)
        {
            // State 1: HP Ratio (0-1)
            float hpRatio = GetLastBattleHPRatio();
            sensor.AddObservation(Mathf.Clamp01(hpRatio));

            // State 2: Damage Ratio (0-2 typically)
            float damageRatio = GetDamageRatio();
            sensor.AddObservation(Mathf.Clamp(damageRatio, 0f, 2f));

            // State 3: Turn count normalized (0-1)
            int expectedTurns = Mathf.Max(1, _expectedTurnsPerBattle);
            float turnNormalized = _turnCount / (float)expectedTurns;
            sensor.AddObservation(Mathf.Clamp01(turnNormalized));

            // State 4: Current difficulty normalized (0-1)
            float difficultyNormalized = _difficultySettings != null
                ? _difficultySettings.GetNormalizedDifficulty()
                : 0.5f;
            sensor.AddObservation(difficultyNormalized);

            // State 5: Area progress normalized (0-1)
            float areaProgress = _totalAreas > 0 ? (float)_currentArea / _totalAreas : 0f;
            sensor.AddObservation(Mathf.Clamp01(areaProgress));

            // State 6: Win streak normalized (-1 to 1)
            float winStreak = Mathf.Clamp((_areasWon - 1) / 5f, -1f, 1f);
            sensor.AddObservation(winStreak);
        }
```

- [ ] **Step 3: Add area notification methods**

```csharp
        /// <summary>
        /// Called when entering new area.
        /// </summary>
        public void OnAreaEnter(int areaIndex, int totalAreas)
        {
            _currentArea = areaIndex;
            _totalAreas = totalAreas;
        }

        /// <summary>
        /// Called when area completed (won or lost).
        /// </summary>
        public void OnAreaComplete(bool won)
        {
            if (won)
            {
                _areasWon++;
            }
            else
            {
                _areasWon = 0; // Reset streak on loss
            }
        }

        /// <summary>
        /// Called when training run starts.
        /// </summary>
        public void OnRunStart()
        {
            _currentArea = 0;
            _areasWon = 0;
        }
```

- [ ] **Step 4: Update OnEpisodeBegin**

```csharp
        public override void OnEpisodeBegin()
        {
            // Reset state for new episode
            _battleStartHP = 0;
            _damageDealt = 0;
            _turnCount = 0;
            _battleInProgress = false;
            _currentArea = 0;
            _areasWon = 0;

            // Reset difficulty to normal for new episode
            if (_difficultySettings != null)
            {
                _difficultySettings.ResetToNormal();
            }
        }
```

- [ ] **Step 5: Update observation size in config**

The config in `config/ddqn.yaml` needs observation size update:

```yaml
behaviors:
  ddqn_dda:
    network_settings:
      normalize: true
      hidden_units: 128
      num_layers: 2
      # Updated observation size: 6 (was 4)
      # HP ratio, damage ratio, turn normalized, difficulty, area progress, win streak
```

- [ ] **Step 6: Commit DDAAgent observations**

```bash
git add Assets/Scripts/DDA/DDAAgent.cs
git commit -m "$(cat <<'EOF'
feat(dda): add area progression observations to DDAAgent

New observations for better DDA decisions:
- Area progress (0-1): current area / total areas
- Win streak (-1 to 1): consecutive wins normalized

Added methods:
- OnAreaEnter: track area progression
- OnAreaComplete: track win/loss streaks
- OnRunStart: reset for new run

Observation size: 6 (was 4)

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Update TrainingUIDisplay

**Files:**
- Modify: `Assets/Scripts/DDA/TrainingUIDisplay.cs`

**Purpose:** Add area progress, action stats, and run results display.

- [ ] **Step 1: Add area and run tracking fields**

Add to `TrainingUIDisplay.cs`:

```csharp
        [Header("Area Progress")]
        [SerializeField] private TextMeshProUGUI _areaText;
        [SerializeField] private TextMeshProUGUI _enemyText;
        [SerializeField] private Slider _areaProgressSlider;

        [Header("Run Stats")]
        [SerializeField] private TextMeshProUGUI _runText;
        [SerializeField] private TextMeshProUGUI _playerHPText;
        [SerializeField] private TextMeshProUGUI _playerStatsText;
        [SerializeField] private TextMeshProUGUI _actionText;

        [Header("Run Result")]
        [SerializeField] private GameObject _runResultPanel;
        [SerializeField] private TextMeshProUGUI _runResultText;
        [SerializeField] private float _runResultDisplayTime = 2f;

        private int _lastArea = -1;
        private int _lastRun = -1;
```

- [ ] **Step 2: Update Start to subscribe to new events**

```csharp
        private void Start()
        {
            if (_simulator == null)
            {
                _simulator = TrainingBattleSimulator.Instance;
            }

            if (_agent == null)
            {
                _agent = FindObjectOfType<DDAAgent>();
            }

            // Initialize history
            _rewardHistory = new float[_graphHistorySize];

            // Subscribe to events
            if (_simulator != null)
            {
                _simulator.OnBattleStateChanged += UpdateBattleState;
                _simulator.OnBattleEnded += HandleBattleEnd;
                _simulator.OnDifficultyChanged += HandleDifficultyChange;
                _simulator.OnStatsUpdated += UpdateStats;
                _simulator.OnAreaChanged += HandleAreaChange;
                _simulator.OnRunComplete += HandleRunComplete;
            }

            // Initialize UI
            UpdateUI();
        }
```

- [ ] **Step 3: Add area change handler**

```csharp
        private void HandleAreaChange(int areaIndex, int totalAreas)
        {
            if (_areaText != null)
            {
                _areaText.text = $"Area: {areaIndex}/{totalAreas}";
            }

            if (_areaProgressSlider != null)
            {
                _areaProgressSlider.value = (float)areaIndex / totalAreas;
            }

            if (_enemyText != null)
            {
                _enemyText.text = $"Enemy: {_simulator.CurrentEnemyName}";
            }
        }
```

- [ ] **Step 4: Add run complete handler**

```csharp
        private void HandleRunComplete(RunResult result)
        {
            if (_runText != null)
            {
                _runText.text = $"Run: {result.RunNumber}";
            }

            if (_playerHPText != null)
            {
                _playerHPText.text = $"HP: {result.FinalHP}";
            }

            if (_playerStatsText != null)
            {
                _playerStatsText.text = $"Lv.{result.Level} | Coin: {result.Coin}";
            }

            // Show run result
            if (_runResultPanel != null && _runResultText != null)
            {
                _runResultPanel.SetActive(true);
                string resultStr = result.Won ? "🎉 RUN COMPLETE!" : "💀 GAME OVER";
                _runResultText.text = $"{resultStr}\n" +
                                      $"Areas: {result.AreasCompleted}/{result.TotalAreas}\n" +
                                      $"HP: {result.FinalHP} | Lv.{result.Level}";
                _runResultText.color = result.Won ? _easyColor : _hardColor;

                StartCoroutine(HideRunResultAfterDelay(_runResultDisplayTime));
            }

            Debug.Log($"[TrainingUI] Run {result.RunNumber}: Won={result.Won}, " +
                      $"Areas={result.AreasCompleted}/{result.TotalAreas}");
        }

        private System.Collections.IEnumerator HideRunResultAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_runResultPanel != null)
            {
                _runResultPanel.SetActive(false);
            }
        }
```

- [ ] **Step 5: Update OnDestroy for new subscriptions**

```csharp
        private void OnDestroy()
        {
            if (_simulator != null)
            {
                _simulator.OnBattleStateChanged -= UpdateBattleState;
                _simulator.OnBattleEnded -= HandleBattleEnd;
                _simulator.OnDifficultyChanged -= HandleDifficultyChange;
                _simulator.OnStatsUpdated -= UpdateStats;
                _simulator.OnAreaChanged -= HandleAreaChange;
                _simulator.OnRunComplete -= HandleRunComplete;
            }
        }
```

- [ ] **Step 6: Update UpdateUI for action tracking**

```csharp
        private void UpdateUI()
        {
            if (_simulator == null) return;

            // Episode count
            if (_episodeText != null)
            {
                _episodeText.text = $"Episode: {_simulator.EpisodeCount}";
            }

            // Difficulty
            if (_difficultyText != null)
            {
                _difficultyText.text = $"Difficulty: {_simulator.DifficultyName}";
            }

            // Win rate
            if (_winRateText != null)
            {
                _winRateText.text = $"Win Rate: {_simulator.WinRate:P1}";
            }

            // Reward
            if (_rewardText != null)
            {
                _rewardText.text = $"Reward: {_simulator.LastReward:F3} (Avg: {_simulator.AvgReward:F3})";
            }

            // Difficulty indicator color
            UpdateDifficultyIndicator(_simulator.CurrentDifficulty);

            // Player stats
            var player = _simulator.Player;
            if (_playerHPText != null && player != null)
            {
                _playerHPText.text = $"HP: {player.CurrentHP}/{player.MaxHP}";
            }

            if (_actionText != null && player != null)
            {
                _actionText.text = $"Gun: {player.GunUses}/{player.MaxGunUses} | Defend: {player.DefendUses}/{player.MaxDefendUses}";
            }
        }
```

- [ ] **Step 7: Commit TrainingUIDisplay update**

```bash
git add Assets/Scripts/DDA/TrainingUIDisplay.cs
git commit -m "$(cat <<'EOF'
feat(dda): update TrainingUIDisplay with area and run tracking

New UI elements:
- Area progress (current/total)
- Enemy name display
- Player HP and action uses
- Run result popup (win/loss)
- Run stats (level, coin)

Subscribed to new simulator events:
- OnAreaChanged
- OnRunComplete

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Summary

**Files Created:**
- `Assets/Scripts/DDA/SimPlayer.cs` - Simulated player state
- `Assets/Scripts/DDA/SimEnemy.cs` - Simulated enemy data
- `Assets/Scripts/DDA/SimArea.cs` - Simulated area/node data
- `Assets/Scripts/DDA/SmartBattleAI.cs` - Action selection logic

**Files Modified:**
- `Assets/Scripts/DDA/TrainingBattleSimulator.cs` - Game-accurate simulation
- `Assets/Scripts/DDA/DDAAgent.cs` - Area/progression observations
- `Assets/Scripts/DDA/TrainingUIDisplay.cs` - Area and run tracking UI

**Implementation Order:**
1. Task 1: SimPlayer (data structure)
2. Task 2: SimEnemy (data structure)
3. Task 3: SimArea (data structure)
4. Task 4: SmartBattleAI (action selection)
5. Task 5: TrainingBattleSimulator (core simulation)
6. Task 6: DDAAgent (observations)
7. Task 7: TrainingUIDisplay (UI updates)

---

Plan complete and saved to `docs/superpowers/plans/2026-04-27-battle-simulation.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?