# Episode Definition and Difficulty Timing Fix

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix episode definition so 1 episode = 1 area, and difficulty applies to NEXT area (not current).

**Architecture:**
- DDAAgent: Move EndEpisode from OnRunEnd to OnAreaComplete
- TrainingBattleSimulator: Apply difficulty to next area, call EndEpisode after each area
- Episode = single area battle sequence

**Tech Stack:** Unity C#, ML-Agents

---

## File Structure

**Files to modify:**
- `Assets/Scripts/DDA/DDAAgent.cs` - Episode lifecycle management
- `Assets/Scripts/DDA/TrainingBattleSimulator.cs` - Difficulty timing, episode end

---

## Task 1: Fix DDAAgent Episode Lifecycle

**Files:**
- Modify: `Assets/Scripts/DDA/DDAAgent.cs`

### Current Behavior (Wrong)
- `OnAreaEnter()` → `RequestDecision()` for current area
- `OnBattleEnd()` → accumulates reward, no episode end
- `OnRunEnd()` → `EndEpisode()` (wrong: should end episode per area)

### Target Behavior (Correct)
- `OnAreaEnter()` → `RequestDecision()` for NEXT area (difficulty for upcoming area)
- `OnAreaComplete()` → `EndEpisode()` (1 episode = 1 area)
- Remove `OnRunEnd()` or repurpose it

- [ ] **Step 1: Modify OnAreaEnter to not request decision**

Current code (line 90-105):
```csharp
public void OnAreaEnter(int areaIndex, int totalAreas)
{
    _currentArea = areaIndex;
    _totalAreas = totalAreas;

    // Request difficulty decision ONCE per area (not per battle)
    RequestDecision();

    if (_difficultySettings != null &&
        _difficultySettings.CurrentLevelIndex != _lastDifficultyLevel)
    {
        OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
        Debug.Log($"[DDAAgent] Area {areaIndex}: Difficulty = {_difficultySettings.GetLevelName()}");
        _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
    }
}
```

Change to:
```csharp
/// <summary>
/// Called when entering new area.
/// Does NOT request decision - difficulty was already set for this area.
/// </summary>
public void OnAreaEnter(int areaIndex, int totalAreas)
{
    _currentArea = areaIndex;
    _totalAreas = totalAreas;

    // DO NOT request decision here - difficulty was set at end of previous area
    // First area uses default difficulty

    if (_difficultySettings != null &&
        _difficultySettings.CurrentLevelIndex != _lastDifficultyLevel)
    {
        OnDifficultyChanged?.Invoke(_difficultySettings.CurrentLevelIndex);
        Debug.Log($"[DDAAgent] Area {areaIndex}: Difficulty = {_difficultySettings.GetLevelName()}");
        _lastDifficultyLevel = _difficultySettings.CurrentLevelIndex;
    }
}
```

- [ ] **Step 2: Modify OnAreaComplete to end episode**

Current code (line 111-121):
```csharp
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
```

Change to:
```csharp
/// <summary>
/// Called when area completed (won or lost).
/// This ends the episode (1 episode = 1 area).
/// Requests difficulty decision for NEXT area.
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

    if (!_isTrainingMode)
    {
        return;
    }

    // Calculate reward for this area
    float reward = CalculateRewardForArea(won);
    AddReward(reward);

    Debug.Log($"[DDAAgent] Area complete. Won: {won}, Areas won streak: {_areasWon}, " +
              $"Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");

    // Request difficulty decision for NEXT area
    RequestDecision();

    // End episode (1 episode = 1 area)
    EndEpisode();
}
```

- [ ] **Step 3: Add CalculateRewardForArea helper method**

Add after `CalculateReward` method (around line 350):
```csharp
/// <summary>
/// Calculates reward based on area outcome.
/// </summary>
private float CalculateRewardForArea(bool won)
{
    float reward = 0f;

    if (won)
    {
        // Base win reward
        reward += 1.0f;

        // Win streak bonus
        reward += 0.1f * Mathf.Min(_areasWon, 5);

        // HP ratio consideration (from last battle)
        float hpRatio = GetLastBattleHPRatio();
        if (hpRatio >= 0.4f && hpRatio <= 0.6f)
        {
            // Flow state bonus
            reward += 0.3f;
        }
        else if (hpRatio > 0.6f)
        {
            // Too easy - small penalty
            reward -= 0.05f * (hpRatio - 0.6f);
        }
        else if (hpRatio < 0.4f)
        {
            // Too hard - small penalty
            reward -= 0.05f * (0.4f - hpRatio);
        }
    }
    else
    {
        // Loss penalty
        reward -= 1.0f;
    }

    return Mathf.Clamp(reward, -2f, 2f);
}
```

- [ ] **Step 4: Modify OnBattleEnd to accumulate reward without ending episode**

Current code (line 250-266):
```csharp
public void OnBattleEnd(bool playerWon, int playerEndHP)
{
    _battleInProgress = false;

    if (!_isTrainingMode)
    {
        return;
    }

    // Calculate reward for this battle and accumulate
    float reward = CalculateReward(playerWon, playerEndHP, _battleStartHP, _turnCount);
    AddReward(reward);

    Debug.Log($"[DDAAgent] Battle end. Won: {playerWon}, HP: {playerEndHP}/{_battleStartHP}, " +
              $"Turns: {_turnCount}, Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");
}
```

Change to:
```csharp
/// <summary>
/// Called when battle ends. Accumulates reward.
/// Does NOT end episode - episode ends at OnAreaComplete.
/// </summary>
public void OnBattleEnd(bool playerWon, int playerEndHP)
{
    _battleInProgress = false;

    if (!_isTrainingMode)
    {
        return;
    }

    // Calculate reward for this battle and accumulate
    // Reward is accumulated per battle within an area
    float reward = CalculateReward(playerWon, playerEndHP, _battleStartHP, _turnCount);
    AddReward(reward);

    Debug.Log($"[DDAAgent] Battle end. Won: {playerWon}, HP: {playerEndHP}/{_battleStartHP}, " +
              $"Turns: {_turnCount}, Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");
}
```

- [ ] **Step 5: Update OnRunEnd to only log (no EndEpisode)**

Current code (line 272-288):
```csharp
public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
{
    if (!_isTrainingMode)
    {
        return;
    }

    // Bonus/penalty for run outcome
    float runReward = runWon ? 0.5f : -0.5f;
    AddReward(runReward);

    Debug.Log($"[DDAAgent] Run end. Won: {runWon}, Areas: {areasCompleted}/{totalAreas}, " +
              $"Total Reward: {GetCumulativeReward():F3}");

    // NOW end the episode
    EndEpisode();
}
```

Change to:
```csharp
/// <summary>
/// Called when training run ends (all areas or player died).
/// Note: Episodes are already ended per-area in OnAreaComplete.
/// This is for logging only.
/// </summary>
public void OnRunEnd(bool runWon, int areasCompleted, int totalAreas)
{
    // Episodes are already ended per-area
    // This method is for run-level logging only
    Debug.Log($"[DDAAgent] Run end. Won: {runWon}, Areas: {areasCompleted}/{totalAreas}");
}
```

- [ ] **Step 6: Commit DDAAgent changes**

```bash
git add Assets/Scripts/DDA/DDAAgent.cs
git commit -m "fix(dda): episode = 1 area, difficulty applies to next area

- Move EndEpisode from OnRunEnd to OnAreaComplete
- Remove RequestDecision from OnAreaEnter
- Add RequestDecision to OnAreaComplete (for next area)
- OnRunEnd now logs only, no longer ends episode"
```

---

## Task 2: Fix TrainingBattleSimulator Difficulty Timing

**Files:**
- Modify: `Assets/Scripts/DDA/TrainingBattleSimulator.cs`

### Current Behavior (Wrong)
- Line 268: `OnAreaEnter` then immediately applies difficulty to current area
- Line 306: `OnRunEnd` ends episode (wrong)

### Target Behavior (Correct)
- Difficulty decided at end of previous area, applied to current area
- Episode ends after each area completes
- First area uses default difficulty

- [ ] **Step 1: Modify RunTrainingRun to apply difficulty from previous area**

Current code (line 250-317):
```csharp
private IEnumerator RunTrainingRun()
{
    _runInProgress = true;
    _currentAreaIndex = 0;
    _runCount++;

    // Reset player for new run
    _player.Reset();

    // Notify agent that run is starting (new episode)
    _ddaAgent?.OnRunStart();

    Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

    // Process each area
    while (_currentAreaIndex < _areas.Count && _player.IsAlive())
    {
        // Notify agent we're entering a new area (requests difficulty decision)
        _ddaAgent?.OnAreaEnter(_currentAreaIndex, _areas.Count);

        // Apply difficulty to enemies in this area
        float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
        float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
        _areas[_currentAreaIndex].ApplyDifficulty(hpMult, dmgMult);

        yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));

        if (!_player.IsAlive())
        {
            break; // Player died, end run
        }

        // Notify agent that area is complete
        _ddaAgent?.OnAreaComplete(won: true);

        _currentAreaIndex++;
        OnAreaChanged?.Invoke(_currentAreaIndex, _areas.Count);
    }

    // Run complete
    bool runWon = _player.IsAlive() && _currentAreaIndex >= _areas.Count;
    // ... rest of method
}
```

Change to:
```csharp
private IEnumerator RunTrainingRun()
{
    _runInProgress = true;
    _currentAreaIndex = 0;
    _runCount++;

    // Reset player for new run
    _player.Reset();

    // Notify agent that run is starting
    _ddaAgent?.OnRunStart();

    // Reset difficulty to default at start of run
    _difficultySettings?.ResetToNormal();

    Debug.Log($"[TrainingSim] Starting run {_runCount} with {_areas.Count} areas");

    // Process each area
    while (_currentAreaIndex < _areas.Count && _player.IsAlive())
    {
        // Apply difficulty that was set at end of PREVIOUS area
        // (or default difficulty for first area)
        float hpMult = _difficultySettings?.HPMultiplier ?? 1.0f;
        float dmgMult = _difficultySettings?.DamageMultiplier ?? 1.0f;
        _areas[_currentAreaIndex].ApplyDifficulty(hpMult, dmgMult);

        // Notify agent we're entering this area (no decision request, just state update)
        _ddaAgent?.OnAreaEnter(_currentAreaIndex, _areas.Count);

        yield return StartCoroutine(ProcessArea(_areas[_currentAreaIndex]));

        // Notify agent that area is complete (ends episode, requests difficulty for next)
        bool areaWon = _player.IsAlive();
        _ddaAgent?.OnAreaComplete(areaWon);

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

    // Notify agent that run ended (for logging only, episode already ended in OnAreaComplete)
    _ddaAgent?.OnRunEnd(runWon, _currentAreaIndex, _areas.Count);

    Debug.Log($"[TrainingSim] Run {_runCount} complete. Won: {runWon}, " +
              $"Areas: {_currentAreaIndex}/{_areas.Count}, HP: {_player.CurrentHP}");

    if (_resetOnRunComplete)
    {
        _player.Reset();
    }

    _runInProgress = false;
}
```

- [ ] **Step 2: Update EpisodeCount property name for clarity (optional)**

The property `EpisodeCount` currently returns `_runCount`. This is technically correct now since we're tracking runs separately. But for clarity, we could add an `AreaCount` property:

Add after line 72:
```csharp
public int AreaCount => _battleCount;  // Total areas completed (episodes)
public int RunCount => _runCount;       // Full runs completed
```

And change line 72:
```csharp
public int EpisodeCount => _battleCount;  // Episodes = areas
```

Wait, this is confusing. Let me reconsider...

Actually, the naming should be:
- `_battleCount` → counts battles (individual enemy fights)
- `_areaCount` → counts areas completed (this equals episodes)
- `_runCount` → counts full runs (12 areas)

Let me update this properly.

- [ ] **Step 3: Rename battle count to area count for episodes**

Find `_battleCount` (line ~50) and rename to `_episodeCount`:
```csharp
// Training stats
private int _episodeCount;     // Counts areas completed (1 episode = 1 area)
private int _runCount;         // Counts full runs
private int _battleCount;      // Counts individual battles
private int _winCount;
private int _lossCount;
```

Update all references:
- Line 71: `public int EpisodeCount => _episodeCount;`
- Line 74: `public float WinRate => _battleCount > 0 ? (float)_winCount / _battleCount : 0f;`
- Line 75: `public float AvgReward => _battleCount > 0 ? _totalReward / _battleCount : 0f;`

Actually, let me look at the file more carefully. The `EpisodeCount` is used for ML-Agents stats. Let me check what makes sense.

- `EpisodeCount` in ML-Agents = number of times EndEpisode() was called
- If 1 episode = 1 area, then `EpisodeCount` should equal number of areas completed
- `_battleCount` tracks individual battles (can be multiple per area if area has multiple enemies)

So:
- `_battleCount` = individual battles
- `_episodeCount` = areas completed = episodes (NEW, add this)
- `_runCount` = full runs

- [ ] **Step 4: Add episode tracking variable**

Add after line 49:
```csharp
private int _episodeCount;     // Counts episodes (1 episode = 1 area)
```

- [ ] **Step 5: Update EpisodeCount property**

Change line 72:
```csharp
public int EpisodeCount => _episodeCount;  // Episodes = areas
```

- [ ] **Step 6: Increment episode count when area completes**

In `RunTrainingRun`, after `OnAreaComplete`:
```csharp
// Notify agent that area is complete (ends episode, requests difficulty for next)
bool areaWon = _player.IsAlive();
_ddaAgent?.OnAreaComplete(areaWon);
_episodeCount++;  // Increment episode count
```

- [ ] **Step 7: Update GetStats to use episode count**

Change TrainingStats struct (line 726):
```csharp
public int EpisodeCount;         // Areas completed (1 episode = 1 area)
```

- [ ] **Step 8: Commit TrainingBattleSimulator changes**

```bash
git add Assets/Scripts/DDA/TrainingBattleSimulator.cs
git commit -m "fix(training): apply difficulty to next area, track episodes correctly

- Difficulty now applied at area start (was set by previous area)
- Reset difficulty to normal at run start
- Move OnAreaEnter call after difficulty applied
- Call OnAreaComplete after each area (ends episode)
- Add _episodeCount to track areas completed"
```

---

## Task 3: Update TrainingUIDisplay for Correct Episode Display

**Files:**
- Modify: `Assets/Scripts/DDA/TrainingUIDisplay.cs`

- [ ] **Step 1: Read TrainingUIDisplay to check episode display**

Run: Read the file first.

- [ ] **Step 2: Update episode display if needed**

If the file shows "Episode" with wrong count, update to use `EpisodeCount` property which now correctly represents areas.

- [ ] **Step 3: Commit UI changes if any**

```bash
git add Assets/Scripts/DDA/TrainingUIDisplay.cs
git commit -m "fix(ui): display correct episode count (areas)"
```

---

## Self-Review Checklist

**1. Spec coverage:**
- [x] 1 episode = 1 area: DDAAgent.OnAreaComplete calls EndEpisode()
- [x] Difficulty applies to NEXT area: RequestDecision in OnAreaComplete, applied at next OnAreaEnter
- [x] First area uses default: ResetToNormal at run start, no prior decision

**2. Placeholder scan:**
- [x] No "TODO", "TBD", "implement later"
- [x] All code blocks show exact implementations
- [x] All steps have specific commands

**3. Type consistency:**
- [x] OnAreaEnter signature unchanged
- [x] OnAreaComplete signature unchanged (bool won)
- [x] OnRunEnd signature unchanged
- [x] EpisodeCount property returns int (unchanged)

---

Plan complete and saved to `docs/superpowers/plans/2026-04-27-episode-and-difficulty-timing-fix.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?