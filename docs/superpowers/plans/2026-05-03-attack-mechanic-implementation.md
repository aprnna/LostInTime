# Attack Mechanic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change player attack from interval-based damage to absolute values with TapZone-determined critical hits.

**Architecture:** Remove damage variance (MinDamage/MaxDamage), add fixed TapZone difficulty per action. Training simulation mirrors gameplay with absolute damage + TapZone success probability.

**Tech Stack:** Unity 2022+, C#, ML-Agents, UniTask

---

## Files Changed

| File | Action |
|------|--------|
| `Assets/Scripts/Player/Item/BaseAction.cs` | Modify - remove interval, add TapZone props |
| `Assets/Scripts/Manager/State/CriticalAttackState.cs` | Modify - use CriticalDamage |
| `Assets/Scripts/DDA/SmartBattleAI.cs` | Modify - absolute damage calculation |
| `Assets/Scripts/DDA/SimPlayer.cs` | Modify - remove interval properties |
| `Assets/Scripts/Minigames/TapZone/TapZoneData.cs` | Modify - add fixed zone mode |

---

### Task 1: Update BaseAction.cs - Remove Interval, Add TapZone Properties

**Files:**
- Modify: `Assets/Scripts/Player/Item/BaseAction.cs`

- [ ] **Step 1: Remove interval field and related properties**

Remove these lines from `BaseAction.cs`:

```csharp
// Line ~13 - Remove field
[SerializeField] private int _interval;

// Lines ~34-36 - Remove properties
public int MinDamage { get; private set; }
public int MaxDamage { get; private set; }
public bool IsIntervalDamage => _interval > 0;
```

- [ ] **Step 2: Add TapZone difficulty and critical bonus fields**

Add after line 24 (after `_difficultyCritical` field):

```csharp
[Header("TapZone Settings")]
[SerializeField, Range(0, 11)] private float _tapZoneDifficulty = 5f;
[SerializeField, Range(0, 50)] private int _criticalBonusPercent = 10;
```

- [ ] **Step 3: Add TapZone properties**

Add after `CriticalHitDamage` property (around line 36):

```csharp
public float TapZoneDifficulty => _tapZoneDifficulty;
public int CriticalDamage => Mathf.RoundToInt(BaseDamage * (1 + _criticalBonusPercent / 100f));
```

- [ ] **Step 4: Update InitializeDamage method**

Replace the `InitializeDamage` method (lines 93-99) with:

```csharp
public void InitializeDamage(int baseDamagePlayer, int criticalPercentage)
{
    BaseDamage = Mathf.RoundToInt(baseDamagePlayer * (_percentageDamage / 100f));
    CriticalHitDamage = BaseDamage + Mathf.RoundToInt(BaseDamage * (criticalPercentage / 100f));
    // Removed: MinDamage/MaxDamage interval calculation
}
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Player/Item/BaseAction.cs
git commit -m "refactor(action): remove interval damage, add TapZone difficulty and critical bonus"
```

---

### Task 2: Update CriticalAttackState.cs - Use CriticalDamage

**Files:**
- Modify: `Assets/Scripts/Manager/State/CriticalAttackState.cs`

- [ ] **Step 1: Update damage calculation in AttackAction**

Replace line 56 in `AttackAction` method:

```csharp
// Before:
var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalHitDamage : _battleSystem.SelectedAction.BaseDamage;

// After:
var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalDamage : _battleSystem.SelectedAction.BaseDamage;
```

- [ ] **Step 2: Set TapZone difficulty from action**

Replace line 53 in `AttackAction` method:

```csharp
// Before:
_battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.DifficultyCritical);

// After:
_battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.TapZoneDifficulty);
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Manager/State/CriticalAttackState.cs
git commit -m "refactor(battle): use CriticalDamage and action TapZone difficulty"
```

---

### Task 3: Update SimPlayer.cs - Remove Interval Properties

**Files:**
- Modify: `Assets/Scripts/DDA/SimPlayer.cs`

- [ ] **Step 1: Remove interval fields**

Remove from fields section (lines ~23-25, ~46-48):

```csharp
public int IntervalDamage;
public int IntervalDefend;
public int PunchInterval;
public int SwordInterval;
public int GunInterval;
```

- [ ] **Step 2: Remove damage range methods**

Remove these methods (lines ~208-227):

```csharp
public (int min, int max) GetPunchDamageRange() { ... }
public (int min, int max) GetSwordDamageRange() { ... }
public (int min, int max) GetGunDamageRange() { ... }
```

- [ ] **Step 3: Update LoadFromDefaults to remove interval values**

In `LoadFromDefaults` method, remove interval assignments (lines ~111-113):

```csharp
// Remove these lines:
PunchInterval = 0;
SwordInterval = 2;
GunInterval = 3;
```

Also update default values at lines ~98-100:
```csharp
// Remove:
IntervalDamage = 3;
IntervalDefend = 2;
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/DDA/SimPlayer.cs
git commit -m "refactor(sim): remove interval damage from SimPlayer"
```

---

### Task 4: Update SmartBattleAI.cs - Absolute Damage Calculation

**Files:**
- Modify: `Assets/Scripts/DDA/SmartBattleAI.cs`

- [ ] **Step 1: Replace CalculateDamage method**

Replace the entire `CalculateDamage` method (lines ~145-194) with:

```csharp
/// <summary>
/// Calculate damage for action with absolute value and TapZone simulation.
/// </summary>
public static int CalculateDamage(SimAction action, SimPlayer player, float skill = 0.5f)
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
    float tapZoneSize = action switch
    {
        SimAction.Punch => 0.4f,   // 40% zone = easy
        SimAction.Sword => 0.25f,  // 25% zone = medium
        SimAction.Gun => 0.15f,    // 15% zone = hard
        _ => 0.25f
    };

    // Success probability: skill influence + zone size
    bool tapSuccess = UnityEngine.Random.value < (skill * 0.5f + tapZoneSize);

    if (tapSuccess)
    {
        baseDamage = Mathf.RoundToInt(baseDamage * 1.1f); // +10% critical
    }

    // Accuracy check (85% for player)
    if (UnityEngine.Random.value > 0.85f)
    {
        return 0; // Miss
    }

    return Mathf.Max(1, baseDamage);
}
```

- [ ] **Step 2: Update EstimateKillChance for new damage values**

Replace `EstimateKillChance` method (lines ~84-109) with updated max damage estimates:

```csharp
private static float EstimateKillChance(BattleState state, SimAction action)
{
    int estimatedMaxDamage;
    switch (action)
    {
        case SimAction.Gun:
            estimatedMaxDamage = 13; // 100% of 12 + 10% crit
            break;
        case SimAction.Sword:
            estimatedMaxDamage = 11; // 80% of 12 + 10% crit
            break;
        default:
            estimatedMaxDamage = 4; // Punch + crit
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
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/DDA/SmartBattleAI.cs
git commit -m "refactor(ai): absolute damage calculation with TapZone simulation"
```

---

### Task 5: Update TapZoneData.cs - Add Fixed Zone Mode

**Files:**
- Modify: `Assets/Scripts/Minigames/TapZone/TapZoneData.cs`

- [ ] **Step 1: Add fixed zone fields**

Add after line 11 (after `randomizeZone` field):

```csharp
[Header("Fixed Zone Mode")]
[Tooltip("Use fixed zone size instead of difficulty scaling")]
[SerializeField] private bool _useFixedZoneSize = false;
[SerializeField, Range(0.05f, 0.95f)] private float _fixedZoneSize = 0.25f;
```

- [ ] **Step 2: Update GetZoneSize method**

Replace `GetZoneSize` method (lines 34-37) with:

```csharp
public float GetZoneSize(float difficulty)
{
    if (_useFixedZoneSize)
    {
        return _fixedZoneSize;
    }
    return zoneSize.Get(difficulty);
}
```

- [ ] **Step 3: Add public property for fixed zone mode**

Add after `ZoneCenterClamp` property (line 27):

```csharp
public bool UseFixedZoneSize => _useFixedZoneSize;
public float FixedZoneSize => _fixedZoneSize;
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Minigames/TapZone/TapZoneData.cs
git commit -m "feat(tapzone): add fixed zone size mode for per-action difficulty"
```

---

### Task 6: Update TapZone.cs - Support Fixed Zone from MinigameManager

**Files:**
- Modify: `Assets/Scripts/Minigames/TapZone/TapZone.cs`

- [ ] **Step 1: Add field for override zone size**

Add after line 35 (after `currentZoneSizeFraction`):

```csharp
private float? _overrideZoneSize = null;
```

- [ ] **Step 2: Add method to set override zone size**

Add new public method after `PressStop` method:

```csharp
public void SetOverrideZoneSize(float? zoneSize)
{
    _overrideZoneSize = zoneSize;
}
```

- [ ] **Step 3: Update SetupZone to use override**

In `SetupZone` method, replace line 214:

```csharp
// Before:
currentZoneSizeFraction = _tapZoneData.GetZoneSize(difficulty);

// After:
currentZoneSizeFraction = _overrideZoneSize ?? _tapZoneData.GetZoneSize(difficulty);
```

- [ ] **Step 4: Clear override in OnCleanUp**

Add in `OnCleanUp` method after line 266:

```csharp
_overrideZoneSize = null;
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Minigames/TapZone/TapZone.cs
git commit -m "feat(tapzone): support override zone size from action"
```

---

### Task 7: Update MinigameManager.cs - Pass Zone Size from Action

**Files:**
- Modify: `Assets/Scripts/Minigames/MinigameManager.cs`

- [ ] **Step 1: Add method to set zone size for TapZone**

Add new method after `SetDifficulty` method:

```csharp
public void SetTapZoneSize(float? zoneSize)
{
    _tapZone.SetOverrideZoneSize(zoneSize);
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Minigames/MinigameManager.cs
git commit -m "feat(minigame): add TapZone size override method"
```

---

### Task 8: Final Integration - CriticalAttackState Pass Zone Size

**Files:**
- Modify: `Assets/Scripts/Manager/State/CriticalAttackState.cs`

- [ ] **Step 1: Add zone size constants mapping**

Add at top of class after line 10:

```csharp
// Zone sizes per action type (matching SmartBattleAI)
private static readonly float[] ActionZoneSizes = new float[]
{
    0.4f,   // Punch = 40%
    0.25f,  // Sword = 25%
    0.15f   // Gun = 15%
};
```

- [ ] **Step 2: Map PlayerActionType to zone size**

Add helper method to get zone size:

```csharp
private float GetActionZoneSize()
{
    return _battleSystem.SelectedAction.ActionType switch
    {
        PlayerActionType.Punch => 0.4f,
        PlayerActionType.Sword => 0.25f,
        PlayerActionType.Gun => 0.15f,
        _ => 0.25f
    };
}
```

- [ ] **Step 3: Pass zone size in AttackAction**

In `AttackAction` method, add before `PlayTapZone` call:

```csharp
_battleSystem.MinigameManager.SetTapZoneSize(GetActionZoneSize());
_battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.TapZoneDifficulty);
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Manager/State/CriticalAttackState.cs
git commit -m "feat(battle): pass fixed zone size based on action type"
```

---

### Task 9: Verification - Run Training Simulation

**Files:**
- None (verification)

- [ ] **Step 1: Open Unity Editor**

Open project in Unity 2022+.

- [ ] **Step 2: Open DDA Training Scene**

Navigate to `Assets/Scenes/` and open DDA ML Agents training scene.

- [ ] **Step 3: Run training simulation**

Press Play in Unity Editor. Verify:
- Battles execute without errors
- Damage values are absolute (no variance)
- TapZone shows with correct zone sizes per action
- Critical hits apply +10% damage

- [ ] **Step 4: Check console for errors**

Verify no compilation errors or runtime exceptions.

---

### Task 10: Final Commit

- [ ] **Step 1: Stage all remaining changes**

```bash
git status
git add -A
```

- [ ] **Step 2: Final commit**

```bash
git commit -m "feat(attack): absolute damage with TapZone critical system

- Remove interval-based damage variance
- Add fixed TapZone zone size per action (Punch=40%, Sword=25%, Gun=15%)
- Critical hit bonus fixed at +10% on successful TapZone
- Training simulation mirrors gameplay mechanics"
```

---

## Summary

| Task | Description |
|------|-------------|
| 1 | BaseAction.cs - remove interval, add TapZone props |
| 2 | CriticalAttackState.cs - use CriticalDamage |
| 3 | SimPlayer.cs - remove interval properties |
| 4 | SmartBattleAI.cs - absolute damage calculation |
| 5 | TapZoneData.cs - fixed zone mode |
| 6 | TapZone.cs - override zone size |
| 7 | MinigameManager.cs - pass zone size |
| 8 | CriticalAttackState.cs - pass zone size |
| 9 | Verification - run training |
| 10 | Final commit |