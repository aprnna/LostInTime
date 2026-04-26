# Battle Simulation Design - Game-Accurate Training

Date: 2026-04-27

## Overview

Update `TrainingBattleSimulator` to match actual game mechanics for ML-Agents DDA training. Includes proper action system, damage roulette, enemy variety, area progression, and persistent player stats.

## Enemy Data

| Enemy | HP | DMG Min | DMG Max | Type |
|-------|-----|---------|---------|------|
| Caveman | 15 | 4 | 8 | Normal |
| Sabertooth | 18 | 5 | 11 | Normal |
| Raptor | 24 | 4 | 14 | Normal |
| Trex | 35 | 15 | 25 | Boss |
| Triceratops | 40 | 14 | 26 | Boss |

**Damage calculation:** `DMG = baseDamage ± intervalDamage`
- Caveman: 6 ± 2 → 4-8
- Sabertooth: 8 ± 3 → 5-11
- Raptor: 9 ± 5 → 4-14
- Trex: 20 ± 5 → 15-25
- Triceratops: 20 ± 6 → 14-26

## Player System

### Base Stats (from CurrentPlayerData.asset)
- HP: 100/100
- Shield: 2/2
- BaseDamage: 12
- IntervalDamage: 3 → DMG range: 9-15
- BaseDefend: 2
- IntervalDefend: 2 → Defend range: 0-4
- CriticalHit: 20%
- EXP: 0/100

### Actions

| Action | Damage Calc | Limit | Notes |
|--------|-------------|-------|-------|
| Punch | 30% × baseDMG | ∞ | Low damage, always available |
| Gun | 120% × baseDMG | 10 | High damage, limited uses |
| Defend | BaseDefend × 3 ± 6 | 3 | Shield HP absorbs damage |

**Damage formula:**
```csharp
int baseActionDamage = Mathf.RoundToInt(playerStats.BaseDamage * (percentageDamage / 100f));
int minDamage = baseActionDamage - interval;
int maxDamage = baseActionDamage + interval;
int damage = Random.Range(minDamage, maxDamage + 1);

// Critical hit (20% chance)
if (Random.value < criticalChance)
    damage = Mathf.RoundToInt(damage * (1 + criticalPercentage / 100f));
```

**Defend calculation:**
```csharp
int baseDefend = playerStats.BaseDefend * multipleDefend; // × 3
int minDefend = baseDefend - intervalDefend; // - 6
int maxDefend = baseDefend + intervalDefend; // + 6
int shieldHP = Random.Range(minDefend, maxDefend + 1);
```

**Hit mechanic:** 85% accuracy for player, 80% for enemy.

## Area Progression

### Map Structure (12 Areas from MapData.asset)

| Node | Type | Enemies | Drops |
|------|------|---------|-------|
| Enemy1 | Enemy | Caveman ×1 | Coin 15, Exp 5, SparePart 10 |
| Enemy2 | Enemy | Sabertooth + Caveman | SparePart 10, Coin 35, Exp 15 |
| Rest1 | Rest | - | Heal 10-24 HP |
| Enemy3 | Enemy | Sabertooth + Caveman | SparePart 10, Coin 35, Exp 15 |
| Enemy4 | Enemy | Raptor ×2 | Coin 60, Exp 20 |
| Enemy5 | Enemy | Raptor ×2 | Coin 20, Exp 20, SparePart 10 |
| Shop1 | Shop | - | Buy items |
| Enemy6 | Enemy | Raptor + Sabertooth + Caveman | Coin 65, Exp 20, SparePart 10 |
| Enemy7 | Enemy | Raptor + Sabertooth + Caveman | Coin 65, Exp 20, SparePart 10 |
| Rest2 | Rest | - | Heal 10-24 HP |
| Shop2 | Shop | - | Buy items |
| Boss1 | Boss | Trex | SparePart 20, Exp 20, Coin 50 |

### Area Types

**Enemy Area:**
- Fight all enemies in sequence
- Win → collect drops (Coin, Exp, SparePart)
- Continue to next area

**Rest Area:**
- Heal HP via roulette: `Random.Range(10, 25)`
- No combat

**Shop Area:**
- Buy Chicken: 15 Coin → +20 HP
- Buy Shield: 20 Coin → +2 Shield
- Smart AI: Buy if HP < 50% and have coins

**Boss Area:**
- Single boss enemy
- Higher rewards
- Triggers biome transition

## Persistent Stats

Stats persist across areas:
- HP carries over (heal at Rest/Shop)
- Shield carries over
- Coin accumulates from drops
- EXP accumulates, triggers level-up
- Gun/Defend uses tracked and reset per run

### Level-Up System

```csharp
if (EXP + value >= MaxExp)
{
    // Level up
    EXP = remainingOverflow;
    MaxExp += 25; // +25 per level
    IsLevelUp = true;
}
```

**Level-up choices (auto-select for training):**
1. `LevelUpDamage(int value)` - Increase BaseDamage
2. `LevelUpHealth(int value)` - Increase MaxHealth
3. `LevelUpShield(int value)` - Increase BaseDefend

## Smart AI Logic

```csharp
public PlayerAction ChooseAction(SimBattleState state)
{
    // Priority 1: Defend if low HP and defend available
    if (state.PlayerHP < state.PlayerMaxHP * 0.3f && state.DefendUsesRemaining > 0)
        return PlayerAction.Defend;

    // Priority 2: Gun to finish low HP enemy
    if (state.EnemyHP < state.PlayerGunDamage * 1.2f && state.GunUsesRemaining > 0)
        return PlayerAction.Gun;

    // Priority 3: Weighted random
    float gunWeight = state.GunUsesRemaining > 0 ? 0.4f : 0f;
    float defendWeight = state.DefendUsesRemaining > 0 && state.PlayerHP < state.PlayerMaxHP * 0.5f ? 0.3f : 0f;
    float punchWeight = 1f - gunWeight - defendWeight;

    float roll = Random.value;
    if (roll < punchWeight) return PlayerAction.Punch;
    if (roll < punchWeight + gunWeight) return PlayerAction.Gun;
    return PlayerAction.Defend;
}
```

## DDA Integration

### Difficulty Settings (DifficultySettings)

Current levels (0-4):
- 0: Very Easy (HP × 0.7, DMG × 0.7)
- 1: Easy (HP × 0.85, DMG × 0.85)
- 2: Normal (HP × 1.0, DMG × 1.0)
- 3: Hard (HP × 1.2, DMG × 1.2)
- 4: Very Hard (HP × 1.5, DMG × 1.5)

### Reward Shaping

```csharp
float CalculateReward(bool playerWon, int playerEndHP, int playerStartHP, int turns)
{
    float reward = 0f;

    if (playerWon)
    {
        reward += 1.0f; // Base win
        float hpRatio = (float)playerEndHP / playerStartHP;

        // Flow state bonus (40-60% HP remaining)
        if (hpRatio >= 0.4f && hpRatio <= 0.6f)
        {
            float distanceFromOptimal = Mathf.Abs(hpRatio - 0.5f);
            reward += 0.5f * (1f - distanceFromOptimal * 2f);
        }
        else if (hpRatio > 0.6f)
        {
            reward -= 0.1f * (hpRatio - 0.6f); // Too easy
        }
        else if (hpRatio < 0.4f)
        {
            reward -= 0.1f * (0.4f - hpRatio); // Too hard
        }
    }
    else
    {
        reward -= 1.0f; // Loss penalty
    }

    // Efficiency penalty
    if (turns > 10)
        reward -= 0.01f * (turns - 10);

    return Mathf.Clamp(reward, -2f, 2f);
}
```

## Architecture

```
TrainingBattleSimulator (updated)
├── SimPlayer
│   ├── HP, MaxHP, Shield, MaxShield
│   ├── BaseDamage, IntervalDamage
│   ├── BaseDefend, IntervalDefend
│   ├── GunUses, DefendUses
│   ├── Coin, EXP, MaxEXP
│   └── ChooseAction() - Smart AI
├── SimEnemy
│   ├── HP, MaxHP
│   ├── MinDamage, MaxDamage
│   └── EnemyType
├── SimArea
│   ├── MapType (Enemy/Rest/Shop/Boss)
│   ├── EnemySO[] enemies
│   └── DropItem[] drops
├── AreaSequence
│   ├── Load from MapData or generate
│   └── 12 areas per run
├── BattleFlow
│   ├── PlayerTurn: ChooseAction → Execute → ApplyDamage
│   ├── EnemyTurn: Random damage → ApplyDamage
│   └── CheckEnd → Reward → DDAAgent.OnBattleEnd()
└── AreaProgression
    ├── Apply drops (Coin, EXP, SparePart)
    ├── Rest: Heal roulette
    ├── Shop: Buy items (smart)
    └── Level-up: Auto-upgrade
```

## Implementation Plan

1. Create `SimPlayer` struct/class with full stats
2. Create `SimEnemy` struct/class from EnemySO data
3. Create `SimArea` struct/class from MapNode data
4. Update `TrainingBattleSimulator` with:
   - Area sequence loading
   - Smart AI action selection
   - Damage/Defend roulette
   - Rest/Shop area handling
   - Level-up system
   - Persistent stats across areas
5. Update `DDAAgent` observations for new state
6. Update `TrainingUIDisplay` for new metrics

## Files to Modify

- `Assets/Scripts/DDA/TrainingBattleSimulator.cs` - Main simulation logic
- `Assets/Scripts/DDA/DDAAgent.cs` - Add area/progression observations
- `Assets/Scripts/DDA/TrainingUIDisplay.cs` - Add area progress, action stats

## Files to Create

- `Assets/Scripts/DDA/SimPlayer.cs` - Simulated player state
- `Assets/Scripts/DDA/SimEnemy.cs` - Simulated enemy data
- `Assets/Scripts/DDA/SimArea.cs` - Simulated area data
- `Assets/Scripts/DDA/SmartBattleAI.cs` - Action selection logic