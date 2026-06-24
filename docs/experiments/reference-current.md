# Reference: Current Implementation (HEAD `exp/1`)

Snapshot states/actions/reward dari source code sekarang. Pakai sebagai baseline untuk experiment berikutnya — di section "Apa yang berubah dari baseline/referensi" tulis cuma yang beda dari sini.

Snapshot diambil dari:
- `Assets/Scripts/DDA/DDAAgent.cs`
- `Assets/Scripts/DDA/DifficultySettings.cs`
- `config/ddqn.yaml`

## States (Observations) — 10 total

Semua dinormalisasi [0,1].

| # | Nama | Range | Cara hitung |
|---|------|-------|-------------|
| 1 | HP Ratio | 0–1 | `playerEndHP / battleStartHP`, clamp01 |
| 2 | Turn Count | 0–1 | `clamp01(turnCount / 20)` |
| 3 | Player Level | 0–1 | `clamp01(level / 10)` |
| 4 | Damage Ratio | 0–1 | `damageDealt / totalEnemyHP`, clamp01 |
| 5 | Resource Depletion | 0–1 | avg depletion of actions (dari simulator) |
| 6 | Area Progress Ratio | 0–1 | `areasCompleted / 12` |
| 7 | Current Difficulty | 0–1 | `currentLevelIndex / 4` |
| 8 | Sword Remaining | 0–1 | `uses / maxUses` |
| 9 | Gun Remaining | 0–1 | `uses / maxUses` |
| 10 | Defend Remaining | 0–1 | `uses / maxUses` |

## Actions — 3 discrete

| Action | Nama | Efek |
|--------|------|------|
| 0 | Maintain | no change |
| 1 | Increase | `DifficultySettings.IncreaseDifficulty()` (+1 level) |
| 2 | Decrease | `DifficultySettings.DecreaseDifficulty()` (−1 level) |

Branch size: 3. Heuristic: I=increase, D=decrease.

## Reward

**Per-area base reward** (`CalculateReward`):

```
if (!won) return -0.5f;                          // loss (softened dari -1.0)

hpRatio = clamp01(endHP / startHP)

if (0.35 <= hpRatio <= 0.65):                    // sweet spot, parabolic peak di 50%
    return 1.0 - 25.0 * (hpRatio - 0.5)^2        // range ~0.44 (edge) sampai 1.0 (peak)

if (hpRatio > 0.65):                             // too easy
    return (1.0 - hpRatio) * 0.5                 // small positive, dorong harder

// hpRatio < 0.35, too hard:
return hpRatio * 0.3                             // small positive, dorong easier
```

**Progressive weight** (`OnAreaComplete`):

```
weight = 0.5 + 0.5 * (areasCompleted / totalAreas)   // 0.5 (area 1) → 1.0 (area 12)
finalReward = baseReward * weight
```

**Run bonus** (`OnRunEnd`):

```
runBonus = won ? +0.5 : -0.1
```

**First area**: reward SKIPPED (baseline, no agent action caused it).

**Credit assignment**: reward area t di-attribute ke action a_{t-1} yang set difficulty untuk area t.

**Decision**: `RequestDecision()` only after battle areas (not Rest/Shop).

## Episode

- 1 episode = 1 full run (12 areas)
- `EndEpisode()` only in `OnRunEnd()`

## Difficulty — 5 levels

| Index | Name | HP/Damage Multiplier |
|-------|------|----------------------|
| 0 | Very Easy | 0.6× |
| 1 | Easy | 0.8× |
| 2 | Normal | 1.0× |
| 3 | Hard | 1.2× |
| 4 | Very Hard | 1.4× |

Start run: Normal (index 2). Multiplier apply ke HP + Damage.

## Hyperparameter (`config/ddqn.yaml`)

| Param | Value |
|-------|-------|
| trainer_type | ddqn |
| learning_rate | 0.0003 |
| learning_rate_schedule | constant |
| batch_size | 128 |
| buffer_size | 50000 |
| gamma | 0.99 |
| tau | 0.005 |
| steps_per_update | 4 |
| exploration_initial_eps | 1.0 |
| exploration_final_eps | 0.03 |
| exploration_decay_steps | 1,000,000 |
| hidden_units | 128 |
| num_layers | 2 |
| normalize | true |
| time_horizon | 64 |
| max_steps | 1,000,000 |
| summary_freq | 5000 |
| checkpoint_interval | 50000 |

## Environment

| Setting | Value |
|---------|-------|
| num_envs | 4 |
| time_scale | 100 |
| no_graphics | true |
| training scene | BattleSimulationTraining |
| simulator mode | instant (`_instantMode = true`) |