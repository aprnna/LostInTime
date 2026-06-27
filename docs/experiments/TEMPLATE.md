# Experiment: <run-id>

> Isi file ini SEBELUM run training. Commit bareng config. Lihat [`README.md`](./README.md) untuk aturan.

- **Run ID**: `<--run-id>` (folder `results/<run-id>/`)
- **Tanggal run**: YYYY-MM-DD
- **Tanggal doc**: YYYY-MM-DD
- **Git commit (saat run)**: `<sha>`
- **Branch**: `<branch>`
- **Tujuan**: <satu kalimat. Apa yang diuji / berubah dari run sebelumnya? Hypothesis?>

## Apa yang berubah dari baseline/referensi

> Bedakan dengan [`reference-current.md`](./reference-current.md) atau experiment sebelumnya. Tulis cuma yang beda. Kalau gak ada yang berubah selain hyperparameter, tulis "hanya hyperparameter".

-

## States (Observations)

> Isi dari `DDAAgent.CollectObservations()`. Jumlah + urutan harus persis. Semua dinormalisasi [0,1] kalau gak ada alasan lain.

| # | Nama | Range | Cara hitung | Sumber field |
|---|------|-------|-------------|--------------|
| 1 | HP Ratio | | | |
| 2 | | | | |
| ... | | | | |

Total: __ observations.

## Actions

> Dari `OnActionReceived()`. Discrete actions, `ActionSpec`.

| Action | Nama | Efek |
|--------|------|------|
| 0 | | |
| 1 | | |
| 2 | | |

Branch size: __. Space: __.

## Reward

> Dari `DDAAgent.CalculateReward()` + `OnAreaComplete()` + `OnRunEnd()`. Tulis formula, bukan deskripsi.

**Per-area reward** (attributed to action yang set difficulty untuk area itu):

```
<formula>
```

**Progressive weight**: `weight = 0.5 + 0.5 * (areasCompleted / totalAreas)` → final reward = base × weight.

**Run bonus**: win = `+__`, loss = `__`.

**First area**: reward SKIPPED (baseline, no agent action caused it).

**Decision trigger**: `RequestDecision()` only after battle areas (not Rest/Shop).

## Episode structure

- 1 episode = 1 full run (__ areas)
- `EndEpisode()` only in `OnRunEnd()`

## Hyperparameter

> Copy dari `config/ddqn.yaml` yang DIPAKAI run ini (cek juga `results/<run-id>/configuration.yaml` untuk nilai aktual).

| Param | Value |
|-------|-------|
| trainer_type | ddqn |
| learning_rate | |
| learning_rate_schedule | |
| batch_size | |
| buffer_size | |
| gamma | |
| tau | |
| steps_per_update | |
| exploration_initial_eps | |
| exploration_final_eps | |
| exploration_decay_steps | |
| hidden_units | |
| num_layers | |
| normalize | |
| time_horizon | |
| max_steps | |
| summary_freq | |
| checkpoint_interval | |

## Environment

| Setting | Value |
|---------|-------|
| num_envs | |
| num_areas | |
| time_scale | |
| no_graphics | |
| training scene | |
| simulator mode | instant / coroutine |

## Difficulty

> Dari `DifficultySettings`. 5 levels.

| Index | Name | Multiplier |
|-------|------|------------|
| 0 | Very Easy | |
| 1 | Easy | |
| 2 | Normal | |
| 3 | Hard | |
| 4 | Very Hard | |

Start run at: Normal (index 2). Agent changes ±1 level per action.

## Results

> Isi SETELAH run selesai.

- **Steps trained**: 
- **Final cumulative reward** (mean): 
- **Win rate** (final): 
- **Q-value trend**: (collapsing / stable / diverging?)
- **TensorBoard path**: `results/<run-id>/`
- **Best checkpoint**: `<file>.onnx` (step __)

## Notes / Analysis

- Apakah hypothesis terbukti?
- Masalah yang muncul:
- Follow-up untuk run berikutnya: