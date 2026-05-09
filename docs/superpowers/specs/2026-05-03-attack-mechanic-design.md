# Attack Mechanic Design: Absolute Damage + TapZone Critical

**Date:** 2026-05-03
**Status:** Approved
**Scope:** Battle system, training simulation

## Overview

Change player attack mechanics from interval-based damage variance to absolute damage values with TapZone-determined critical hits.

## Current System

- Damage uses `_percentageDamage` + `_interval` for variance range
- `MinDamage` to `MaxDamage` calculated from interval
- `DamageRouletteState` uses roulette to pick damage in range
- `CriticalAttackState` uses TapZone for critical hits (success = CriticalHitDamage)
- Training simulation in `SmartBattleAI.CalculateDamage` simulates variance

## New System

### Damage Calculation

- **Absolute values** calculated from percentage of base damage
- **No interval variance** - single damage value per action
- **Critical bonus:** +10% on successful TapZone

| Action | Percentage | Example (Base=12) | Critical (+10%) |
|--------|------------|-------------------|-----------------|
| Punch | 30% | 4 | 4 |
| Sword | 80% | 10 | 11 |
| Gun | 100% | 12 | 13 |

### TapZone System

- **Fixed zone size per action** (no difficulty scaling)
- **Success:** +10% damage bonus
- **Failure:** Base damage (no penalty)

| Action | Zone Size | Difficulty |
|--------|-----------|------------|
| Punch | 40% | Easy |
| Sword | 25% | Medium |
| Gun | 15% | Hard |

## Implementation

### 1. BaseAction.cs

**Remove:**
```csharp
[SerializeField] private int _interval;
public int MinDamage { get; private set; }
public int MaxDamage { get; private set; }
public bool IsIntervalDamage => _interval > 0;
```

**Add:**
```csharp
[SerializeField, Range(0, 11)] private float _tapZoneDifficulty = 5f;
[SerializeField, Range(0, 50)] private int _criticalBonusPercent = 10;

public float TapZoneDifficulty => _tapZoneDifficulty;
public int CriticalDamage => Mathf.RoundToInt(BaseDamage * (1 + _criticalBonusPercent / 100f));
```

**Modify InitializeDamage:**
```csharp
public void InitializeDamage(int baseDamagePlayer, int criticalPercentage)
{
    BaseDamage = Mathf.RoundToInt(baseDamagePlayer * (_percentageDamage / 100f));
    // Remove MinDamage/MaxDamage calculation
}
```

### 2. CriticalAttackState.cs

```csharp
// Change damage calculation
var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalDamage
                           : _battleSystem.SelectedAction.BaseDamage;

// Set TapZone difficulty from action
_battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.TapZoneDifficulty);
```

### 3. SmartBattleAI.cs

Replace `CalculateDamage` with absolute value + TapZone simulation:

```csharp
public static int CalculateDamage(SimAction action, SimPlayer player, float skill = 0.5f)
{
    int baseDamage = action switch
    {
        SimAction.Punch => Mathf.RoundToInt(player.BaseDamage * (player.PunchPercentage / 100f)),
        SimAction.Sword => Mathf.RoundToInt(player.BaseDamage * (player.SwordPercentage / 100f)),
        SimAction.Gun => Mathf.RoundToInt(player.BaseDamage * (player.GunPercentage / 100f)),
        _ => 0
    };

    // TapZone success simulation
    float tapZoneSize = action switch
    {
        SimAction.Punch => 0.4f,
        SimAction.Sword => 0.25f,
        SimAction.Gun => 0.15f,
        _ => 0.25f
    };

    bool tapSuccess = UnityEngine.Random.value < (skill * 0.5f + tapZoneSize);

    if (tapSuccess)
        baseDamage = Mathf.RoundToInt(baseDamage * 1.1f);

    // Accuracy check
    if (UnityEngine.Random.value > 0.85f)
        return 0;

    return Mathf.Max(1, baseDamage);
}
```

### 4. SimPlayer.cs

Remove interval-related properties:
- `IntervalDamage`
- `PunchInterval`
- `SwordInterval`
- `GunInterval`
- `GetPunchDamageRange()`, `GetSwordDamageRange()`, `GetGunDamageRange()`

### 5. TapZoneData.cs

Add fixed zone mode:

```csharp
[SerializeField] private bool _useFixedZoneSize = true;
[SerializeField] private float _fixedZoneSize = 0.25f;

public float GetZoneSize(float difficulty)
{
    if (_useFixedZoneSize)
        return _fixedZoneSize;
    // ... existing difficulty scaling ...
}
```

### 6. Action ScriptableObjects

Update existing action assets:
- `PunchAction.asset`: `_tapZoneDifficulty = 3`, zone size = 40%
- `SwordAction.asset`: `_tapZoneDifficulty = 5`, zone size = 25%
- `GunAction.asset`: `_tapZoneDifficulty = 8`, zone size = 15%

## Files Changed

| File | Change |
|------|--------|
| `Assets/Scripts/Player/Item/BaseAction.cs` | Remove interval, add TapZone difficulty |
| `Assets/Scripts/Manager/State/CriticalAttackState.cs` | Use CriticalDamage, set difficulty |
| `Assets/Scripts/DDA/SmartBattleAI.cs` | Absolute damage + TapZone sim |
| `Assets/Scripts/DDA/SimPlayer.cs` | Remove interval properties |
| `Assets/Scripts/Minigames/TapZone/TapZoneData.cs` | Fixed zone mode |
| `Assets/Resources/Player/Actions/*.asset` | Update TapZone difficulty |

## Testing

1. **Unit tests:** Verify damage calculations produce expected absolute values
2. **PlayMode tests:** Verify TapZone success applies +10% bonus
3. **Training verification:** Compare training stats before/after changes

## Migration Notes

- Existing action ScriptableObjects will need `_tapZoneDifficulty` set manually
- Training runs using old interval values will need reset