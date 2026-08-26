# SHAP Explainable AI for DDQN DDA — Design Spec

**Date:** 2026-08-27
**Scope:** Offline thesis analysis. Produce SHAP explanations of the trained DDQN DDA policy from checkpoint `ddqn_dda-499948.pt`. No change to game runtime behavior; no Unity UI integration.

## 1. Goal

Explain why the trained DDQN agent picks a difficulty action for a given battle state, using SHAP feature attribution on the Q-network. Output static plots + a written report for the thesis (skripsi).

Three explanation layers:
- **Local** — per decision: SHAP waterfall for all 5 action Q-values (why Normal beats Hard).
- **Global** — beeswarm summary: which of the 6 observations consistently drives Q across the run.
- **Counterfactual** — perturb one observation (e.g. HP 0.55 → 0.30), re-explain, show how Q per action shifts (decision boundary).

## 2. Verified Context

- **Network**: `QNetworkDDQN` (`ml_agents_plugin/mlagents_plugin_ddqn/networks.py`). Policy actor = the Q-network. `critic_pass(obs) → {extrinsic: [B,5]}`. `normalize: true` → running mean/var normalization lives inside `ObservationEncoder.VectorInput`, applied to raw obs before encoding. Stats persist in checkpoint.
- **Architecture**: `Norm → Linear(6→128) → ReLU → Linear(128→128) → ReLU → ValueHeads(128→5)`. (`hidden_units=128`, `num_layers=2`, `vis_encode_type=simple` per `config/ddqn.yaml`.)
- **Observations**: `DDAAgent.CollectObservations` adds **6** features (CLAUDE.md says 8 — wrong, no Area Progress / Current Difficulty in sensor), all clamped `[0,1]`:
  1. HP Ratio
  2. Turn Count / 15
  3. Player Level / 5
  4. Damage Dealt Ratio (areaTotalEnemyHP / damageDealt)
  5. QTE Accuracy (successfulQTE / totalQTEOpportunities)
  6. Resource Depletion
- **Actions**: 5 discrete — Very Easy(0), Easy(1), Normal(2), Hard(3), Very Hard(4).
- **Model source**: the deployed beta `.onnx` in `Assets/Resources/DDA/Models/` (candidates: `ddqn_dda_sidang1.onnx` mtime 2026-08-03, `ddqn_dda_final.onnx` 2026-07-31, plus `ddqn_dda5.onnx`, `ddqn_dda_testing*.onnx`). The beta test (DataPost) ran 2026-08-10, so a model dated ≤ 2026-08-10 was deployed. The exact beta model is identified empirically by a match-rate probe (§9.7) across candidates — `argmax Q(onnx, obs)` vs `dda_action_taken`, highest match wins.
- **`.pt` checkpoints evicted.** `ddqn_retrain_sidang` training is still running (pid 12692); `keep_checkpoints: 5` evicted every step before the current last 5 (599960–799978). The 499948 `.pt` referenced in earlier drafts is **gone**. No `.pt` matches the beta `.onnx` (verified: `results/ddqn_sidang1/` empty, no onnx/pt). → Weights are extracted from the beta `.onnx` initializers into `ShapQNet` (PyTorch). The `.onnx` is the ground-truth deployed model.
- **Real beta-deployment logs (DATA SOURCE)**: `E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost\*.jsonl` — 11 sessions, 99 battles, **98 `dda_event` records** = the 98 DDA decisions behind BCM 15.31% (Tabel VI). Each `dda_event` payload contains the exact 6-obs vector the agent acted on:
  ```
  {dda_action_taken:"Very Hard", dda_reward:0.0,
   dda_obs_snapshot:[HP Ratio, Turn/15, Level/5, DmgDealtRatio, QTE Acc, Resource Depl],
   dda_episode_count, player_hp_ratio, total_turns, damage_taken, heals_used}
  ```
  Action distribution in the 98: Very Hard 39, Hard 31, Very Easy 17, Normal 11, Easy 0.
- **`Data/` folder excluded** — 13 older sessions, 107 battles, **0 `dda_event`** (pre-DDA-logging format). Not used.
- **Battle-level hp for survival_ratio**: `battle_start` payload `player_performance.player_hp` (hp_initial) and `battle_end` payload `player_performance.player_hp` (hp_final), alignable to each `dda_event` by in-session order. → outcome_label reconstructable from real logs (no C# runtime needed).
- **TrainingLogger / replay buffer**: not used. `TrainingLogger` is text-only (no 6-obs); replay buffer not saved (`save_replay_buffer: false`). Irrelevant now — real `dda_event` logs replace them.

## 3. Approach

**Data source — Option 1 (chosen, feasible): reconstruct 6-obs from real beta logs.**
The 98 `dda_event` records in `DataPost/` already carry `dda_obs_snapshot[6]` + `dda_action_taken`. Parse them → `states.npy[98,6]`, `actions.npy[98]`. Run `shap_net.forward()` on these real states for Q-values, then SHAP. **No Unity re-run, no simulation, no QTE imputation** — the explained states are exactly the states that produced BCM 15.31%.

**Network side — Approach A (chosen): Extract weights from beta `.onnx` into a minimal PyTorch net + GradientExplainer (DeepSHAP).**
The beta `.onnx` initializers (normalization running mean/var, encoder Linear weights, value-head Linear weights) are loaded into a faithful minimal `ShapQNet` (Norm + 2 Linear/ReLU + ValueHead). Use `shap.GradientExplainer` (native PyTorch, differentiable, fast) for per-decision attribution on all 5 Q-value outputs. Extraction correctness is validated by the faithfulness self-check (§9.1: `ShapQNet.forward` vs `onnxruntime` inference on the same states, `max|diff| < 1e-4`).

**Fallback C (auto, if extraction fails faithfulness)**: `shap.KernelExplainer` running directly on the beta `.onnx` via `onnxruntime` — model-agnostic, no weight extraction, approximate SHAP. Slower but guaranteed to use the exact beta model. Engaged automatically if §9.1 fails after extraction attempt.

**Rejected B**: instantiate real `QNetworkDDQN` via `mlagents` + `load_state_dict` — requires a `.pt`, which is gone. Not viable for the beta model.

**Rejected (simulation re-run)**: §5/§10 originally proposed collecting states from a new `TrainingBattleSimulator` instant-mode run. Rejected because real beta logs with the exact 6-obs already exist, so simulation would explain a *different* distribution than the reported BCM 15.31% — a validity gap. Simulation remains a fallback only if the real logs were lost.

## 4. Components

All Python under `tools/xai/`. No C# changes, no Unity run.

| # | Unit | File | Purpose |
|---|------|------|---------|
| 1 | Log parser | `tools/xai/parse_dda_logs.py` | Parse `DataPost/*.jsonl` (11 files). Extract 98 `dda_event` → `states.npy[98,6]` (from `dda_obs_snapshot`), `actions.npy[98]` (map name→int: Very Easy 0, Easy 1, Normal 2, Hard 3, Very Hard 4). Align `battle_start`/`battle_end` per session to compute per-decision `survival_ratio = hp_final/hp_initial` → `survival.npy[98]`, `outcomes.npy[98]` (0=Subjugate, 1=Balanced, 2=Rebellious, §4.1). Validate obs in `[0,1]` (clamp + warn). Persist `meta.json` (session→decisions map, for traceability). |
| 2 | Q-net replica | `tools/xai/shap_net.py` | `ShapQNet(obs=6, hidden=128, layers=2, actions=5)` with `Norm` layer (running mean/var + eps). `load_checkpoint(path)`: inspect ML-Agents checkpoint keys, map norm stats + encoder linears + value head. `forward(x)→[B,5]`. Fallback B path via `--backend plugin`. |
| 3 | Explainer | `tools/xai/explain_shap.py` | CLI: `--checkpoint`, `--states-dir` (default `tools/xai/`), `--decision N` (index or "representative"), `--all-actions`, `--counterfactual`, `--filter-outcome {Subjugate,Balanced,Rebellious}` (restrict explained + background subset to one outcome category; default all), `--self-check`. Builds `GradientExplainer(net, background)` (background = all 98 states or the filtered subset). Emits waterfalls, beeswarm, counterfactual diffs. |
| 4 | Report generator | `tools/xai/report.py` (or part of explain_shap) | Auto-write `report.md` with: per-decision top features, **global feature ranking**, **explicit "Pola kegagalan (Subjugate)" section** (dominant SHAP features among Subjugate decisions, not just global ranking), **"Pola kegagalan (Rebellious)" section** (dominant features among Rebellious decisions), counterfactual boundary notes. Per-category analysis: mean |SHAP| per feature restricted to that outcome subset, top-3 drivers, and the typical action chosen. |

### 4.1 Outcome classification

`outcome_label` derived solely from `survival_ratio = hp_final / hp_initial`:

| Label | Rule | Meaning (DDA failure mode) |
|-------|------|----------------------------|
| **Balanced** | `0.4 ≤ SR ≤ 0.6` | Sweet spot — aligns with reward parabola peak at HP 50%. Intended DDA outcome. |
| **Subjugate** | `SR > 0.6` | Under-challenged — agent kept difficulty too low; player retains too much HP, stomps enemies. Failure mode (unengaging). |
| **Rebellious** | `SR < 0.4` | Over-challenged — agent set difficulty too high; player loses too much HP / dies. Failure mode. |

Integer code for `outcomes.npy`: `0=Subjugate, 1=Balanced, 2=Rebellious`. Computed in `parse_dda_logs.py` (Python, offline) from the aligned `battle_start`/`battle_end` hp of the battle that each `dda_event` governs. `hp_initial == 0` → decision skipped (no division). Thresholds are constants in the parser; not configurable.

### Output location
`results/shap/499948/` containing:
- `waterfall_decision{d}_action{a}.png` (5 × N_decision plots)
- `summary_beeswarm.png`
- `counterfactual_{feature}_{from}_to_{to}.png` + diff table
- `failure_pattern_subjugate.png`, `failure_pattern_rebellious.png` (per-category mean |SHAP| bar)
- `report.md`

## 5. Data Flow

```
E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost\*.jsonl  (11 files, 98 dda_event)
     ↓ parse_dda_logs.py
  states.npy [98,6]   (dda_obs_snapshot)
  actions.npy [98]    (dda_action_taken → int)
  survival.npy [98]   (battle_end.hp / battle_start.hp, aligned per session)
  outcomes.npy [98]   (0=Subjugate,1=Balanced,2=Rebellious)
  meta.json           (session→decision traceability)
     ↓ background = all 98 states (or --filter-outcome subset)
  beta .onnx (identified by probe §9.7) → ShapQNet.load_from_onnx() → ShapQNet
     ↓
  GradientExplainer(net, background)
     ↓
  Local: 5 waterfall per chosen decision (Q[a] decomposed per feature)
  Global: beeswarm (SHAP distribution across 98 decisions, chosen action Q)
  Counterfactual: perturb one obs, re-explain, diff Q per action
     ↓
  results/shap/499948/*.png + report.md
```

No Unity involvement. All offline Python.

### 5.1 Data provenance & validity

**Source: real closed-beta logs, not simulation.** The 98 explained states come from `DataPost/*.jsonl` — the actual `dda_event` records logged during the closed beta test that produced BCM 15.31% (Tabel VI). Each `dda_obs_snapshot` is the exact 6-obs vector the deployed DDQN agent observed when it chose `dda_action_taken`. This closes the validity gap: SHAP explains the *real* states behind the reported BCM number, not a re-simulated distribution.

Implications:

- **Population, not sample.** N=98 is the full set of DDA decisions in the beta run, not a draw. SHAP background = the 98 states themselves. Local-accuracy holds because background ≈ test distribution (identical population).
- **Checkpoint↔beta model match (must verify).** The beta test deployed *some* trained `.onnx`. SHAP extracts weights from the beta `.onnx` identified by the §9.7 probe. If that is the true deployed model, `argmax Q(shap_net, obs)` matches `dda_action_taken` for ≈95% of decisions (allowing 5% ε-greedy random actions at `exploration_final_eps=0.05`). The probe computes this across all candidate `.onnx`. High match → identified model is the beta model, SHAP explains beta behavior. Low match across all candidates → the deployed model is missing; report must state SHAP explains the *closest available* model on real beta states, not the exact beta policy. **Resolve before claiming thesis validity.**
- **On-policy, deployed regime.** States are from exploitation (near-greedy), the deployed regime. SHAP describes why the deployed agent did what it did on the states it actually met — the strongest validity claim available post-hoc.
- **Counterfactual extrapolation.** Perturbations pushing an obs outside the 98-state distribution (e.g. HP 0.30 when beta HP ratios cluster high) are extrapolation. Report flags counterfactuals that leave the observed range.
- **Validity scope line for thesis.** SHAP explanations are valid for the deployed DDQN policy on the real beta states that produced BCM 15.31%, provided the checkpoint-match self-check passes. They are descriptive of deployed behavior, not causal claims about training.

## 6. ONNX Weight Extraction & Normalization

The beta `.onnx` (identified by §9.7 probe) is the weight source. Parse with the `onnx` package: list `graph.initializer` and map tensors to `ShapQNet`:

- **Normalization** — `VectorInput` running mean / running var (stored as onnx initializers, e.g. `*_running_mean`, `*_running_var`). → `ShapQNet.norm.running_mean`, `running_var`. Forward: `(x - running_mean) / sqrt(running_var + eps)`, `eps = 1e-5` (ML-Agents default; confirm from onnx constant node). Stats **must** be found.
- **Encoder** — 2 `Linear` (6→128, 128→128) + ReLU. Onnx exports Linear as `Gemm` or `MatMul`+`Add`; map `weight` (transposed if Gemm `alpha`/`beta`) + `bias`.
- **ValueHead** — `ValueHeads` `Linear` (128→5). Map `weight`/`bias`.

Extraction is validated by §9.1 faithfulness (PyTorch forward vs onnxruntime on same states). If `max|diff| ≥ 1e-4` → extraction wrong; re-inspect initializer names; if unresolved → auto-switch to Fallback C (KernelExplainer on the .onnx). The `forward(x)→[B,5]` interface stays identical so the explainer is backend-agnostic.

## 7. SHAP Configuration

- **Explainer**: `shap.GradientExplainer(model, background)`. Returns one SHAP array per network output (5 actions) → list of `[N, 6]`.
- **Explained target**: all 5 action Q-values per decision (per chosen option). For global beeswarm, attribute the Q-value of the chosen action.
- **Background**: all 98 real states (the full `states.npy`), or the `--filter-outcome` subset. With `--filter-outcome`, background = same filtered subset (per-category explanation stays within-category). If a subset has < 10 states, warn (SHAP variance high) but proceed.
- **Feature names**: `["HP Ratio", "Turn Count", "Player Level", "Dmg Dealt Ratio", "QTE Accuracy", "Resource Depletion"]`.
- **`nsamples`**: default 200 (GradientExplainer integrates over background samples); tune down if NaN/inf appears.

## 8. Error Handling

| Case | Handling |
|------|----------|
| Checkpoint key mismatch / norm stats missing | Switch to fallback B; warn. |
| `DataPost/` missing or 0 `dda_event` parsed | Hard error pointing to `E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost`. (Fallback to simulation re-run only if user explicitly opts in, with validity caveat.) |
| Action match rate low (< 70%) | Warn: checkpoint ≠ beta model. Report states SHAP explains current checkpoint on real states, not exact beta policy. |
| SHAP NaN/inf (ReLU dead neuron / gradient explode) | Reduce `nsamples`, use DeepSHAP stabilized gradient, retry; skip decision if persistent. |
| Observation outside `[0,1]` | Clamp + log decision as skipped. |
| Additivity violated (`base_value + sum(shap) ≉ Q`) | Flag decision unreliable in report. |
| Checkpoint file missing | Hard error with path. |

## 9. Testing (self-check, no framework)

`python -m explain_shap --self-check` runs:

1. **Faithfulness** — `ShapQNet` (weights extracted from beta `.onnx`) forward on 10 states vs `onnxruntime` inference on the same beta `.onnx` for the same states. Assert `max|Q_shap_net − Q_onnx| < 1e-4`. If fail → extraction wrong; re-inspect initializers; if unresolved → auto-switch to Fallback C.
2. **Range** — assert `states.npy` all in `[0,1]`, shape `[98,6]`.
3. **Additivity** — per decision: `|base_value + sum(shap_values[chosen]) − Q[chosen]| < 1e-3`. SHAP property.
4. **Determinism** — run SHAP twice on same state, assert identical results (GradientExplainer is deterministic given fixed background).
5. **Outcome coverage** — assert all three labels present in `outcomes.npy` (Subjugate, Balanced, Rebellious each ≥ 1 decision). If a category is empty, the per-category failure-pattern section is skipped with a note (insufficient data), not fabricated.
6. **Survival sanity** — assert `survival_ratio` consistent with `hp_final/hp_initial` per row; assert label matches threshold rule (§4.1).
7. **Beta-model identification (probe)** — for each candidate `.onnx` in `Assets/Resources/DDA/Models/`, run `onnxruntime` inference on the 98 `states.npy`, compute `argmax Q == dda_action_taken` match rate. Rank candidates. Highest match = beta model; write its path to `meta.json`. Expected ≥ 90% for the true beta model (allowing 5% ε-greedy random actions at `exploration_final_eps=0.05`). 70–90% → warn, investigate. < 70% across all candidates → hard flag: no candidate matches the beta policy; do not claim SHAP explains the beta policy without resolving (the deployed model may be missing from `Assets/Resources/DDA/Models/`).

All pass → safe to generate thesis plots.

## 10. Data Acquisition

No Unity run. The real beta logs already exist:

## 10. Data Acquisition

No Unity run. The real beta logs already exist:

1. Confirm `E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost\*.jsonl` present (11 files). If the path moved, pass `--log-dir` to `parse_dda_logs.py`.
2. `python parse_dda_logs.py --log-dir "E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost"` → `tools/xai/states.npy`, `actions.npy`, `survival.npy`, `outcomes.npy`, `meta.json`.
3. Confirm candidate `.onnx` models present in `Assets/Resources/DDA/Models/`. Install deps (`pip install -r requirements.txt` + `shap onnxruntime onnx`).
4. `python -m explain_shap --self-check` → must pass, incl. §9.7 beta-model probe (writes chosen `.onnx` to `meta.json`) and §9.1 faithfulness (validates weight extraction).
5. Run explain commands; outputs to `results/shap/<model-name>/`.

(Simulation re-run procedure retained only as emergency fallback if real logs are lost: original Unity procedure — ask user to confirm before adopting, since it changes validity scope.)

## 11. Scope Exclusions (YAGNI)

- No Unity in-game UI for SHAP (deferred; separate spec if needed).
- No dependence plots (only waterfall, beeswarm, counterfactual).
- No multi-checkpoint comparison (only final 499948; can extend later).
- No retraining; no replay buffer enable.
- No C# changes, no `ShapStateLogger`, no `#if SHAP_LOG` hook, no Unity inference run — real `dda_event` logs replace all of that.

## 12. Open Items Resolved at Implementation

- Exact ONNX initializer names for norm running mean/var, encoder Gemm/MatMul weights, value-head weights (inspect via `onnx` package: `model.graph.initializer` + node names). Map to `ShapQNet`.
- `eps` value used by ML-Agents `VectorInput` normalization (read from onnx constant node; default 1e-5).
- **`dda_event` ↔ battle alignment** — confirm the in-session ordering rule that maps each `dda_event` (decision for upcoming battle) to that battle's `battle_start`/`battle_end` hp pair. Inspect one `DataPost` session end-to-end to validate the alignment before computing `survival_ratio`.
- `dda_obs_snapshot` index order **CONFIRMED** — `snapshot[0] == player_hp_ratio` (cross-checked on 3 samples: 0.96, 0.75, 0.391 match exactly). Order = [HP Ratio, Turn/15, Level/5, DmgDealtRatio, QTE Acc, Resource Depl].
- Representative decision indices for local waterfalls (pick programmatically: cluster by HP ratio buckets, early vs late run).