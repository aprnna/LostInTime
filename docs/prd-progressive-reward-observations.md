# PRD: Progressive Reward Weighting and Extended Observations

## Problem Statement

DDA agent saat ini menggunakan reward uniform untuk semua area dalam run (12 areas). Hal ini menyebabkan agent tidak memiliki insentif yang lebih besar untuk mempertahankan performa di area-area akhir. Selain itu, agent hanya memiliki 5 observasi, yang mungkin tidak cukup untuk memahami konteks progres dalam run dan resource state.

## Solution

Menambahkan:
1. **5 observasi baru** ke agent: Area Progress Ratio, Current Difficulty, dan 3 Remaining Resources (Sword, Gun, Defend)
2. **Progressive Reward Weighting** dengan base weight 0.5, sehingga agent mendapat insentif lebih besar untuk performa baik di area akhir

## User Stories

1. As a DDA researcher, I want the agent to observe area progress ratio, so that the agent understands its position within the run trajectory
2. As a DDA researcher, I want the agent to observe current difficulty level, so that the agent can correlate difficulty with outcomes
3. As a DDA researcher, I want progressive reward weighting with base 0.5, so that early areas still contribute meaningfully to learning
4. As a DDA researcher, I want the weight formula to be `progressWeight = 0.5 + 0.5 * progress`, so that rewards scale from 0.5 to 1.0
5. As a DDA researcher, I want the progressive weight applied to both wins and losses, so that penalty is also weighted consistently
6. As a DDA researcher, I want difficulty encoded as `index / 4` (normalized 0-1), so that the neural network can process it effectively
7. As a DDA researcher, I want network hidden units increased to 128, so that the network has sufficient capacity for 10 observations
8. As a DDA researcher, I want progress weight logged in debug output, so that I can monitor training progress
9. As a DDA researcher, I want progress weight logged in TrainingLogger, so that training logs capture the weighting factor
10. As a DDA researcher, I want remaining resources as separate observations, so that the agent knows how many uses it has left
11. As a thesis student, I want clear separation between baseline observations and new observations, so that I can analyze the impact of each feature

## Implementation Decisions

### Modules to Modify

1. **DDAAgent.cs** — Core changes:
   - Add `_areaProgressRatio` field (float)
   - Add `_currentDifficultyNorm` field (float)
   - Add `_swordRemaining`, `_gunRemaining`, `_defendRemaining` fields (float)
   - Extend `CollectObservations()` from 5 to 10 observations
   - Add `progressWeight` calculation in `OnAreaComplete()` with base 0.5
   - Apply `progressWeight` to both positive and negative rewards
   - Update `GetDebugState()` to include all new observations
   - Extend `UpdateBattlePhase()` to accept remaining resources

2. **TrainingLogger.cs** — Logging changes:
   - Add `progressWeight` parameter to `LogAreaComplete()` signature
   - Update log format to include ProgressWeight

3. **TrainingBattleSimulator.cs** — Simulator changes:
   - Update `UpdateAgentBattlePhase()` to calculate and pass remaining resources
   - Update post-area observation update to include remaining resources
   - Update progress weight formula to include base 0.5

4. **config/ddqn.yaml** — Network configuration:
   - Change `hidden_units` from 64 to 128
   - Add documentation comments for observation count

### Observation Encoding

**10 Observations (normalized to [0, 1]):**
1. HP Ratio — existing
2. Turn Count (normalized, cap 20) — existing
3. Player Level (normalized, cap 10) — existing
4. Damage Ratio (dealt / startHP × enemyCount) — existing
5. Resource Depletion — existing
6. **Area Progress Ratio** (NEW) — `_areasCompleted / _totalAreas`
7. **Current Difficulty** (NEW) — `_difficultySettings.CurrentLevelIndex / 4`
8. **Sword Remaining** (NEW) — `SwordUses / MaxSwordUses`
9. **Gun Remaining** (NEW) — `GunUses / MaxGunUses`
10. **Defend Remaining** (NEW) — `DefendUses / MaxDefendUses`

### Progressive Reward Formula

```csharp
// In OnAreaComplete(), after _areasCompleted++
// Base weight 0.5 ensures early areas still matter
float progressWeight = 0.5f + 0.5f * ((float)_areasCompleted / _totalAreas);

if (won) {
    float baseReward = CalculateReward(true, endHP, startHP);
    float weightedReward = baseReward * progressWeight;
    AddReward(weightedReward);
} else {
    float weightedPenalty = -1.0f * progressWeight;
    AddReward(weightedPenalty);
}
```

**Weight progression example (12 areas):**
- Area 2 (first rewarded): weight = 0.5 + 0.5 × (2/12) ≈ 0.58
- Area 6: weight = 0.5 + 0.5 × (6/12) = 0.75
- Area 12 (last): weight = 0.5 + 0.5 × (12/12) = 1.0

### Logging Updates

**GetDebugState():**
```
HPRatio | Turns | Level | DmgRatio | ResDepl | Diff | Prog | DiffNorm | Sword | Gun | Defend | ProgWeight | AreasCompleted | WinRate
```

**LogAreaComplete():**
```
[AREA COMPLETE] Area {i} | Won: {won} | HP: {end}/{start} | AreaReward: {r:F3} | ProgressWeight: {w:F2} | Cumulative: {c:F3}
```

## Testing Decisions

### Unit Tests

1. **Test observation count** — verify `CollectObservations()` returns exactly 10 values
2. **Test difficulty encoding** — verify index 0-4 maps to 0.0, 0.25, 0.5, 0.75, 1.0
3. **Test progress weight calculation** — verify weight = 0.5 + 0.5 × progress
4. **Test progressive reward application** — verify reward is multiplied by progressWeight
5. **Test progressive penalty application** — verify loss penalty is also weighted
6. **Test remaining resources** — verify they update correctly

### Integration Tests

1. **Test full episode flow** — verify progressive weights increase across 12 areas
2. **Test first area skip** — verify first area still skips reward (baseline behavior preserved)

### Prior Art

- Existing `DDAAgent.CalculateReward()` tests (if any)
- Existing observation collection tests in training validation

## Out of Scope

1. Modifying the parabolic reward function itself — only adding weighting
2. Changing the baseline skip logic for first area
3. Modifying TrainingBattleSimulator battle logic
4. Adding new difficulty levels
5. Changing episode structure (12 areas per run)

## Further Notes

### Design Rationale

**Why base weight 0.5?**
- Without base, early areas had near-zero weight (0.17 for area 2)
- With base 0.5, early areas have meaningful weight (0.58 for area 2)
- Ensures agent learns from all areas, not just late ones

**Why remaining resources as separate observations?**
- Agent needs to know how many uses it has left
- Depletion only shows what's been used, not what's available
- Remaining = 1 - depletion, but explicit observation helps learning

**Why linear weighting instead of exponential?**
- Linear is simpler and more interpretable
- Easier to tune and debug
- Avoids reward explosion in late game

**Why include difficulty as observation?**
- Agent needs to correlate difficulty decisions with outcomes
- Without difficulty observation, agent cannot learn "what difficulty did I choose" → "what was the result"

### Related Documents

- `docs/DDQN_Implementation_Guide.md` — DDQN architecture overview
- `docs/2026-04-27-ddqn-dda-implementation.md` — Original implementation notes
- `CLAUDE.md` — Project architecture reference