# SpaceJam - Game Description and DDA Implementation

## Overview

**SpaceJam** adalah game turn-based battle 2D yang dibangun dengan Unity 2022+ dan menggunakan ML-Agents untuk Dynamic Difficulty Adjustment (DDA). Game ini dikembangkan sebagai penelitian/skripsi untuk membandingkan DDA berbasis ML dengan pendekatan baseline.

---

## Game Mechanics

### Core Loop

```
┌─────────────────────────────────────────────────────────────────┐
│                      GAME FLOW                                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  START → Map Navigation → Battle → Result → Next Area → WIN/LOSE│
│                                                                  │
│  1. Player starts with:                                          │
│     - HP: 100, Shield: 2, Base Damage: 12                       │
│     - Actions: Punch, Sword (15x), Gun (10x), Defend (3x)       │
│     - Level: 1, Coin: 0, EXP: 0                                  │
│                                                                  │
│  2. Map Progression:                                              │
│     - 12 areas: Enemy (7), Rest (2), Shop (2), Boss (1)         │
│     - Player navigates node-based map                            │
│     - Each area triggers specific encounter                      │
│                                                                  │
│  3. Battle System:                                                │
│     - Turn-based combat                                          │
│     - Player selects action → TapZone minigame → Damage dealt  │
│     - ONE random enemy attacks per turn                          │
│     - Continue until player or all enemies defeated              │
│                                                                  │
│  4. Progression:                                                 │
│     - Defeat enemies → EXP → Level Up → Choose stat bonus       │
│     - Collect coins → Buy items at shop                         │
│     - Rest areas → Heal HP                                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Battle System

### Turn Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    BATTLE TURN SEQUENCE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  PLAYER TURN:                                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ 1. Select Action (Punch/Sword/Gun/Defend)                │   │
│  │ 2. Select Target (if attack action)                      │   │
│  │ 3. DamageRouletteState - Calculate base damage           │   │
│  │ 4. CriticalAttackState - TapZone minigame                 │   │
│  │    - Success: +10% critical damage                       │   │
│  │    - Fail: Normal damage                                  │   │
│  │ 5. Apply damage to enemy                                  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│  ENEMY TURN:                                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ 1. Select ONE random alive enemy                         │   │
│  │ 2. Show attacking enemy UI (marker + panel)               │   │
│  │ 3. Calculate damage (base ± variance)                    │   │
│  │ 4. Damage applied to player (100% hit rate)               │   │
│  │ 5. Check player death                                     │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│         Repeat until all enemies dead OR player dead            │
│                              ↓                                   │
│                      RESULT STATE                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### State Machine Architecture

```
GameState (Abstract Base)
    │
    ├── PlayerTurnState
    │       └── Select action, wait for input
    │
    ├── SelectActionState
    │       └── Highlight available actions
    │
    ├── SelectEnemyState
    │       └── Choose target enemy
    │
    ├── DamageRouletteState
    │       └── Calculate base damage with variance
    │
    ├── CriticalAttackState
    │       └── TapZone minigame execution
    │       └── Attack: Apply damage to enemy
    │       └── Defend: Add shield HP
    │
    ├── EnemyTurnState
    │       └── ONE random enemy attacks
    │       └── Apply damage to player
    │
    └── ResultBattleState
            └── Victory/Defeat screen
```

---

## Player Actions

### Action System

| Action | Damage | Uses | Accuracy | Critical | TapZone |
|---------|--------|------|----------|----------|---------|
| **Punch** | 30% Base | ∞ | 100% | +10% | Easy (40%) |
| **Sword** | 90% Base | 15 | 100% | +10% | Medium (25%) |
| **Gun** | 100% Base | 10 | 100% | +10% | Hard (15%) |
| **Defend** | Shield HP | 3 | - | - | Medium (25%) |

### Damage Calculation

```csharp
// Base damage from player stats
int baseDamage = Player.BaseDamage;

// Action percentage (Punch: 30%, Sword: 90%, Gun: 100%)
int actionDamage = RoundToInt(baseDamage * ActionPercentage / 100f);

// TapZone success simulation (for AI training)
bool tapSuccess = Random.value < (skill * 0.5f + tapZoneSize);

// Critical bonus
if (tapSuccess)
    actionDamage = RoundToInt(actionDamage * 1.1f); // +10%

// Final damage (no accuracy check - 100% hit rate)
return Max(1, actionDamage);
```

### Defend Mechanics

```csharp
// Shield HP calculation with variance
int baseDefendHP = BaseDefend * DefendMultiple; // Base: 2 × 3 = 6
int min = baseDefendHP - DefendInterval;         // 6 - 6 = 0
int max = baseDefendHP + DefendInterval;         // 6 + 6 = 12
int shieldHP = Random.Range(min, max + 1);      // 0-12

// Shield absorbs damage first
if (CurrentShield > 0)
{
    if (CurrentShield >= damage)
        CurrentShield -= damage;
    else
    {
        damage -= CurrentShield;
        CurrentShield = 0;
        CurrentHP -= damage;
    }
}
```

---

## Enemy System

### Enemy Types

| Enemy | HP | Damage Range | Variance |
|-------|-----|--------------|----------|
| Caveman | 15 | 6 | ±2 |
| Sabertooth | 18 | 8 | ±3 |
| Raptor | 24 | 9 | ±5 |
| Trex (Boss) | 35 | 20 | ±5 |

### Multi-Enemy Battle

```
┌─────────────────────────────────────────────────────────────────┐
│               MULTI-ENEMY BATTLE FLOW                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Area contains: [Enemy A, Enemy B, Enemy C]                     │
│                                                                  │
│  TURN 1:                                                         │
│  ├── Player selects target (Enemy A, B, or C)                   │
│  ├── Player deals damage to ONE enemy                           │
│  ├── ONE random alive enemy attacks player                      │
│  └── (NOT all enemies attack)                                   │
│                                                                  │
│  TURN 2:                                                         │
│  ├── Player selects target                                      │
│  ├── Different random enemy may attack                          │
│  └── Continue until all enemies defeated                        │
│                                                                  │
│  Player must defeat ALL enemies in area to progress.            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Map Progression

### Area Types

| Type | Count | Description |
|------|-------|-------------|
| **Enemy** | 7 | Standard battle, 1-3 enemies |
| **Rest** | 2 | Heal 10-25 HP |
| **Shop** | 2 | Buy shield upgrades, potions |
| **Boss** | 1 | Final boss (Trex) |

### Default Map Sequence

```
Area 1:  Enemy (Caveman)
Area 2:  Enemy (Sabertooth + Caveman)
Area 3:  Rest
Area 4:  Enemy (Sabertooth + Caveman)
Area 5:  Enemy (Raptor x2)
Area 6:  Enemy (Raptor x2)
Area 7:  Shop
Area 8:  Enemy (Raptor + Sabertooth + Caveman)
Area 9:  Enemy (Raptor + Sabertooth + Caveman)
Area 10: Rest
Area 11: Shop
Area 12: Boss (Trex)
```

---

## DDA System Architecture

### Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    DDA SYSTEM ARCHITECTURE                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────┐       ┌─────────────────────┐          │
│  │   TRAINING SIM      │       │   PRODUCTION GAME   │          │
│  │   (ML-Agents)       │       │   (Real Player)     │          │
│  │                     │       │                     │          │
│  │  TrainingBattle     │       │  BattleSystem       │          │
│  │  Simulator          │       │  EnemyTurnState     │          │
│  │  SmartBattleAI      │       │  CriticalAttackState│          │
│  └─────────┬───────────┘       └─────────┬───────────┘          │
│            │                              │                       │
│            │        ┌─────────────────────┴──────────────┐       │
│            │        │                                     │       │
│            └───────►│        DDAAgent (Agent)            │       │
│                     │        - 12 observations           │       │
│                     │        - 3 actions                 │       │
│                     │        - Reward calculation        │       │
│                     │                                     │       │
│                     └───────┬─────────────────────────────┘       │
│                             │                                     │
│                             ▼                                     │
│                     ┌─────────────────────┐                       │
│                     │ DifficultySettings   │                       │
│                     │ - Very Easy (0.75x)  │                       │
│                     │ - Easy (0.875x)      │                       │
│                     │ - Normal (1.0x)      │                       │
│                     │ - Hard (1.125x)      │                       │
│                     │ - Very Hard (1.25x)  │                       │
│                     └───────┬─────────────┘                       │
│                             │                                     │
│                             ▼                                     │
│                     ┌─────────────────────┐                       │
│                     │ DifficultyApplier   │                       │
│                     │ - HP multiplier     │                       │
│                     │ - Damage multiplier │                       │
│                     └─────────────────────┘                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### DDQN Algorithm

```
┌─────────────────────────────────────────────────────────────────┐
│                    DDQN TRAINING FLOW                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. OBSERVATIONS (12 values normalized 0-1):                   │
│     ├── HP ratio (current/max)                                  │
│     ├── Win rate (rolling 20 battles)                           │
│     ├── Turn count normalized                                    │
│     ├── Difficulty normalized (0-4 → 0-1)                       │
│     ├── Area progress (area/total)                              │
│     ├── Player level normalized                                  │
│     ├── Area type (Rest=0, Enemy=0.33, Shop=0.67, Boss=1)       │
│     ├── Damage dealt ratio                                       │
│     ├── **Current HP ratio (real-time)**                        │
│     ├── **Resource depletion (actions used)**                   │
│     ├── **Enemy HP ratio (remaining)**                          │
│     └── **Critical phase flag (HP < 30%)**                      │
│                                                                  │
│  2. ACTIONS (3 discrete choices):                                 │
│     ├── 0: Maintain difficulty                                   │
│     ├── 1: Increase difficulty                                   │
│     └── 2: Decrease difficulty                                   │
│                                                                  │
│  3. REWARD STRUCTURE:                                            │
│     ├── Battle reward: -0.3 to +0.5 (based on HP ratio)          │
│     ├── Flow state bonus: -0.2 to +0.3 (win rate ~60%)          │
│     ├── Run bonus: +0.2 (win), -0.1 (loss)                      │
│     └── Target: Win rate around 60% (flow state)                │
│                                                                  │
│  4. TRAINING:                                                    │
│     ├── Online network: Q(s,a) - action selection               │
│     ├── Target network: Q_target(s',a*) - value evaluation      │
│     ├── Soft update: θ_target = τ·θ_online + (1-τ)·θ_target    │
│     └── τ = 0.005, γ = 0.95, lr = 0.0001                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Battle Phase Features

```
┌─────────────────────────────────────────────────────────────────┐
│              BATTLE PHASE AWARENESS                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Phase features help DDA distinguish battle states:             │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Early Battle (Turn 1-3)                                   │   │
│  │ - HP: 100%                                                │   │
│  │ - Resources: Full (Sword 15, Gun 10, Defend 3)          │   │
│  │ - Enemy HP: 100%                                         │   │
│  │ - Strategy: Aggressive, use limited actions             │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Mid Battle (Turn 4-7)                                     │   │
│  │ - HP: 40-70%                                              │   │
│  │ - Resources: Partial (Sword 8, Gun 5, Defend 1)         │   │
│  │ - Enemy HP: 30-60%                                       │   │
│  │ - Strategy: Balanced, conserve resources                │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Late Battle (Turn 8+)                                     │   │
│  │ - HP: <30% (Critical!)                                    │   │
│  │ - Resources: Low (Sword 2, Gun 0, Defend 0)              │   │
│  │ - Enemy HP: <20%                                         │   │
│  │ - Strategy: Finish with basic attacks or defend          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Phase Features:                                                 │
│  ├── Current HP Ratio: Real-time health status                 │
│  ├── Resource Depletion: (1 - actions_remaining/max)          │
│  ├── Enemy HP Ratio: Total enemy HP remaining                  │
│  └── Critical Flag: 1 if HP < 30%, else 0                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Why Phase Awareness Matters

| Scenario | HP | Turn | Resources | Enemy HP | Correct DDA Decision |
|----------|-----|------|-----------|----------|----------------------|
| A: Early, struggling | 30% | 2 | Full | 90% | Decrease difficulty |
| B: Late, finishing | 30% | 8 | Low | 10% | Maintain or increase |
| C: Early, dominating | 100% | 2 | Full | 20% | Increase difficulty |
| D: Mid, balanced | 60% | 5 | Medium | 50% | Maintain difficulty |

**Without phase features**: A and B look identical (HP 30%)
**With phase features**: Agent can distinguish and make correct decision

---

## Observation Space (12 Features)

| # | Observation | Type | Description |
|---|-------------|------|-------------|
| 1 | HP Ratio | Battle | Current HP / Max HP (after battle) |
| 2 | Win Rate | Global | Rolling 20-battle win rate |
| 3 | Turn Count | Battle | Turns in current battle / expected |
| 4 | Difficulty | Global | Current difficulty (0-4) normalized |
| 5 | Area Progress | Global | Current area / total areas |
| 6 | Player Level | Global | Player level / 10 |
| 7 | Area Type | Global | Rest=0, Enemy=0.33, Shop=0.67, Boss=1 |
| 8 | Damage Ratio | Battle | Damage dealt / (MaxHP × 2) |
| 9 | **Current HP Ratio** | **Phase** | **Real-time HP during battle** |
| 10 | **Resource Depletion** | **Phase** | **Actions used / max actions** |
| 11 | **Enemy HP Ratio** | **Phase** | **Total enemy HP remaining** |
| 12 | **Critical Flag** | **Phase** | **1 if HP < 30%, else 0** |

### Feature Categories

| Category | Features | Purpose |
|----------|----------|---------|
| **Global** | Win Rate, Difficulty, Area Progress, Player Level, Area Type | Long-term difficulty adjustment |
| **Battle** | HP Ratio, Turn Count, Damage Ratio | Current battle state |
| **Phase** | Current HP, Resource Depletion, Enemy HP, Critical Flag | **Battle phase detection** |

---

## Difficulty Multipliers

### 5-Level System

| Level | Name | HP Mult | DMG Mult | Use Case |
|-------|------|---------|----------|----------|
| 0 | Very Easy | 0.75x | 0.75x | Player struggling |
| 1 | Easy | 0.875x | 0.875x | Below target win rate |
| 2 | Normal | 1.0x | 1.0x | Target difficulty |
| 3 | Hard | 1.125x | 1.125x | Above target win rate |
| 4 | Very Hard | 1.25x | 1.25x | Player dominating |

### Application

```csharp
// Applied when entering new area
void ApplyDifficulty(EnemyStats enemy)
{
    enemy.MaxHP = Mathf.RoundToInt(enemy.BaseHP * difficultySettings.HPMultiplier);
    enemy.BaseDamage = Mathf.RoundToInt(enemy.BaseDamage * difficultySettings.DamageMultiplier);
}
```

---

## Reward Calculation (Training Agent)

### Overview

DDA agent trained with shaped rewards targeting **flow state** (60% win rate, 40-60% HP remaining). Rewards accumulated per area, applied at area completion.

```
┌─────────────────────────────────────────────────────────────────┐
│                    REWARD STRUCTURE                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  TOTAL REWARD = Battle Reward + Flow State Bonus + Run Bonus    │
│                                                                  │
│  Components:                                                     │
│  ├── Battle Reward: [-0.3, +0.55] per battle                   │
│  ├── Flow State Bonus: [-0.2, +0.3] per area                   │
│  └── Run Bonus: +0.2 (win) / -0.1 (loss)                       │
│                                                                  │
│  Target Metrics:                                                 │
│  ├── Win Rate: ~60% (flow state engagement)                    │
│  └── HP Remaining: 40-60% (challenging but achievable)         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Battle Reward

**Range: [-0.3, +0.55]**

```csharp
float CalculateBattleReward(bool won, int endHP, int startHP, int turns)
{
    // Loss: fixed penalty (not too harsh to avoid discouraging exploration)
    if (!won) return -0.3f;

    float hpRatio = (float)endHP / startHP;

    // Target HP zone: 40-60% (flow state)
    // Peak reward at 50% HP remaining
    float target = 0.50f;   // Peak at 50% HP
    float width = 0.10f;    // Zone: 40-60% HP

    float dist = Mathf.Abs(hpRatio - target);

    float hpScore;
    if (dist <= width)
    {
        // In flow zone: positive reward, max at 50% HP
        hpScore = 0.5f * (1f - dist / width);  // +0.0 to +0.5
    }
    else
    {
        // Outside flow zone: small penalty
        hpScore = -0.1f * ((dist - width) / (1f - width));
    }

    // Efficiency bonus: reward completing battle in reasonable time
    float efficiencyBonus = turns <= expectedTurns * 1.5f ? 0.05f : 0f;

    return Mathf.Clamp(hpScore + efficiencyBonus, -0.3f, 0.55f);
}
```

**HP Score Graph:**
```
Reward
  +0.5 │        ╱╲
       │       ╱  ╲
  +0.25│      ╱    ╲
       │     ╱      ╲
    0  │────╱────────╲────
       │              ╲
  -0.1 │               ╲____
       └─────────────────────► HP Ratio
        0%   40%  50%  60%  100%
             ↑         ↑
             Flow Zone (peak reward)
```

### Flow State Bonus

**Range: [-0.2, +0.3]**

```csharp
float CalculateFlowStateBonus()
{
    // Need at least 5 battles for meaningful win rate
    if (_winHistoryCount < 5) return 0f;

    float winRate = GetRunningWinRate();  // Rolling 20 battles
    float targetWinRate = 0.60f;          // Target 60% win rate
    float dist = Mathf.Abs(winRate - targetWinRate);

    // Bell curve: peak at 60% win rate
    if (dist < 0.10f)
    {
        // Within 50-70% win rate: bonus
        return 0.3f * (1f - dist / 0.10f);  // 0 to +0.3
    }
    else
    {
        // Outside target: penalty
        return -0.2f * Mathf.Clamp01((dist - 0.10f) / 0.30f);  // 0 to -0.2
    }
}
```

**Win Rate Bonus Graph:**
```
Bonus
  +0.3 │        ╱╲
       │       ╱  ╲
  +0.15│      ╱    ╲
       │     ╱      ╲
    0  │────╱────────╲────
       │              ╲
  -0.2 │               ╲____
       └─────────────────────► Win Rate
        0%   50%  60%  70%  100%
             ↑         ↑
             Target Zone (peak bonus at 60%)
```

### Run Bonus

**Range: [-0.1, +0.2]**

```csharp
void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
{
    // Small bonus/penalty for full run completion
    float runBonus = runWon ? 0.2f : -0.1f;
    AddReward(runBonus);
    EndEpisode();
}
```

### Reward Timing

```
┌─────────────────────────────────────────────────────────────────┐
│                    REWARD APPLICATION TIMELINE                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  EPISODE = ONE AREA (1-3 battles in enemy area)                 │
│                                                                  │
│  Timeline:                                                       │
│                                                                  │
│  1. OnAreaEnter(areaIndex, ...)                                 │
│     └── EndEpisode() for previous area                          │
│     └── Reset accumulated reward                                │
│                                                                  │
│  2. OnBattleStart(playerHP)                                     │
│     └── Track starting HP for reward calc                       │
│                                                                  │
│  3. OnTurnEnd(damageDealt)                                      │
│     └── Track damage dealt (observation)                        │
│                                                                  │
│  4. OnBattleEnd(won, endHP)                                     │
│     └── Calculate battle reward                                 │
│     └── Accumulate: _areaReward += battleReward                 │
│     └── Update win history (rolling 20)                         │
│                                                                  │
│  5. OnAreaComplete(areaWon)                                     │
│     └── finalReward = _areaReward + FlowStateBonus()            │
│     └── AddReward(finalReward)  ← Agent learns here             │
│     └── RequestDecision()  → DDA action for NEXT area           │
│                                                                  │
│  6. OnRunEnd(runWon, ...)                                       │
│     └── AddReward(runBonus)                                     │
│     └── EndEpisode()                                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Why This Reward Structure?

| Aspect | Reason |
|--------|--------|
| **HP-based, not win/lose binary** | Encourages challenging battles, not just wins |
| **Flow zone (40-60% HP)** | Neither too easy (boring) nor too hard (frustrating) |
| **60% win rate target** | Optimal engagement per flow theory research |
| **Accumulated per area** | Learns from multi-battle areas, not just single battles |
| **Small loss penalty (-0.3)** | Avoids discouraging exploration |
| **Flow state bonus** | Rewards consistent difficulty adjustment |

---

## Training vs Production

### Simulation Accuracy

| Aspect | Training | Production | Status |
|--------|----------|------------|--------|
| Enemy turn | ONE random enemy | ONE random enemy | ✅ Match |
| Enemy damage | baseDamage ± variance | baseDamage ± variance | ✅ Match |
| Player accuracy | 100% hit | 100% hit | ✅ Match |
| Enemy accuracy | 100% hit | 100% hit | ✅ Match |
| Action percentages | Punch 30%, Sword 90%, Gun 100% | Load from SO | ✅ Match |
| Defend values | BaseDefend × 3 ± 6 | Load from SO | ✅ Match |
| TapZone | Simulated with skill param | Real minigame | ⚠️ Different |

### Key Differences

1. **TapZone Minigame**
   - Training: Probability-based (`skill × 0.5 + zoneSize`)
   - Production: Real-time timing minigame
   - Impact: Skill parameter controls success rate

2. **Enemy AI**
   - Training: SmartBattleAI (heuristic-based)
   - Production: Same SmartBattleAI in simulation
   - Production game: No enemy AI (random roulette)

---

## Implementation Plan

### Phase 1: Core System (Complete)

- [x] Battle system FSM
- [x] Player actions (Punch, Sword, Gun, Defend)
- [x] Enemy turn (ONE random enemy)
- [x] Multi-enemy battles
- [x] Map progression (12 areas)
- [x] Level-up system

### Phase 2: DDA Integration (Complete)

- [x] DifficultySettings (5 levels)
- [x] DifficultyApplier (HP/Damage multipliers)
- [x] DDAIntegration (hooks to BattleSystem)
- [x] DDAAgent (ML-Agents integration)
- [x] TrainingBattleSimulator (fast training)
- [x] SmartBattleAI (action selection)

### Phase 3: ML Training (Complete)

- [x] DDQN custom trainer plugin
- [x] Training configuration (ddqn.yaml)
- [x] Observation space (8 values)
- [x] Action space (3 discrete)
- [x] Reward calculation

### Phase 4: Polish (In Progress)

- [ ] Training UI improvements
- [ ] Real game DDA integration
- [ ] Win rate calibration
- [ ] Playtesting and tuning

### Phase 5: Evaluation

- [ ] Compare DDA vs baseline approaches
- [ ] Collect metrics (win rate, engagement, difficulty)
- [ ] Thesis documentation

---

## File Structure

```
Assets/
├── Scripts/
│   ├── DDA/
│   │   ├── DDAAgent.cs              # ML-Agents agent
│   │   ├── DDQNTrainer.cs          # Training logic
│   │   ├── DDAIntegration.cs       # Battle system hooks
│   │   ├── DifficultySettings.cs   # Difficulty levels
│   │   ├── DifficultyApplier.cs    # Apply multipliers
│   │   ├── TrainingBattleSimulator.cs  # Fast training
│   │   ├── SmartBattleAI.cs        # AI action selection
│   │   ├── SimPlayer.cs            # Simulated player
│   │   └── TrainingUIDisplay.cs   # Training visualization
│   │
│   ├── Manager/
│   │   ├── BattleSystem.cs         # Battle FSM controller
│   │   └── State/
│   │       ├── PlayerTurnState.cs
│   │       ├── EnemyTurnState.cs   # ONE enemy attack
│   │       ├── CriticalAttackState.cs
│   │       └── ...
│   │
│   └── Player/
│       ├── PlayerStats.cs
│       └── Item/
│           └── BaseAction.cs       # Action ScriptableObjects
│
├── Scenes/
│   ├── BattleSimulationTraining.unity  # ML training scene
│   └── ...
│
config/
└── ddqn.yaml                        # Training configuration

ml_agents_plugin/
└── mlagents_plugin_ddqn/
    ├── ddqn_trainer.py
    ├── ddqn_optimizer.py
    └── ddqn_policy.py
```

---

## Training Command

```bash
# Install dependencies
pip install -r requirements.txt

# Install DDQN plugin
cd ml_agents_plugin && pip install -e .

# Start training
mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train

# Monitor training
tensorboard --logdir results/
```

---

## Configuration

```yaml
# config/ddqn.yaml
behaviors:
  ddqn_dda:
    trainer_type: ddqn
    
    hyperparameters:
      learning_rate: 0.0001
      learning_rate_schedule: linear
      batch_size: 64
      buffer_size: 50000
      tau: 0.005
      exploration_initial_eps: 1.0
      exploration_final_eps: 0.05
      exploration_decay_steps: 100000
      gamma: 0.95
    
    network_settings:
      normalize: true
      hidden_units: 64
      num_layers: 2
    
    reward_signals:
      extrinsic:
        gamma: 0.95
        strength: 1.0
    
    max_steps: 300000
```

---

## Key Design Decisions

### Why DDQN over DQN?

1. **Overestimation Bias**: Standard DQN tends to overestimate Q-values due to max operator
2. **Stability**: DDQN separates action selection (online network) from value evaluation (target network)
3. **Convergence**: Faster convergence with more stable learning

### Why Flow State Target (60% Win Rate)?

1. **Player Engagement**: Neither too easy (boring) nor too hard (frustrating)
2. **Challenge**: Maintains sense of achievement
3. **Retention**: Players stay engaged longer

### Why Soft Update (τ=0.005)?

1. **Stability**: Gradual target network updates prevent oscillation
2. **Continuity**: Target values change smoothly
3. **Proven**: Standard practice in DQN literature

---

## References

1. **DDQN Paper**: van Hasselt et al. (2016). "Deep Reinforcement Learning with Double Q-learning"
2. **ML-Agents**: Unity Technologies ML-Agents Toolkit
3. **Flow Theory**: Csikszentmihalyi (1990). "Flow: The Psychology of Optimal Experience"

---

*Last Updated: 2026-05-05*