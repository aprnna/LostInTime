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
- **Checkpoint**: `results/ddqn_retrain_sidang/ddqn_dda/ddqn_dda-499948.pt` (final step). Also `ddqn_dda-499948.onnx` for cross-check. ML-Agents standard checkpoint format.
- **TrainingLogger**: text-only log, does not record the 6-obs vector. Replay buffer not saved (`save_replay_buffer: false`). → Background data must be collected via a new additive state logger during one inference run.

## 3. Approach

**Approach A (chosen): Reconstruct minimal PyTorch net + GradientExplainer (DeepSHAP).**
Build a faithful minimal `ShapQNet` in pure PyTorch mirroring the plugin architecture (Norm + 2 Linear/ReLU + ValueHead), load weights from `.pt`. Use `shap.GradientExplainer` (native PyTorch, differentiable, fast) for per-decision attribution on all 5 Q-value outputs.

**Fallback B**: If checkpoint key mapping fails or normalization stats are not recoverable, instantiate the real `QNetworkDDQN` via `mlagents` (construct `ObservationSpec(shape=(6,), ObservationType.DEFAULT)`, `ActionSpec.create_discrete((5,))`, `NetworkSettings(hidden_units=128, num_layers=2, normalize=True)`), `load_state_dict`, and wrap `critic_pass`. The SHAP wrapper exposes the same `forward(x)→[B,5]` interface so `explain_shap.py` is backend-agnostic.

**Rejected C**: KernelExplainer on ONNX — model-agnostic but slow (approx exponential), approximation not exact DeepSHAP, not differentiable (counterfactual only via re-run). Emergency only.

## 4. Components

All Python under `tools/xai/`. One new C# file + one minimal hook.

| # | Unit | File | Purpose |
|---|------|------|---------|
| 1 | State logger | `Assets/Scripts/DDA/ShapStateLogger.cs` | Static additive logger. Dumps per decision `{obs:[6], action:int, qvalues:[5], run:int, area:int, hp_initial:float, hp_final:float, survival_ratio:float, outcome_label:str}` to JSONL at `Application.persistentDataPath/.../DDA_Training/shap_states.jsonl`. `survival_ratio = hp_final / hp_initial` (guard `hp_initial == 0` → skip decision). `outcome_label` assigned at run time (see §4.1). Gated by `#if SHAP_LOG` define. Thread-safe (mirror `TrainingLogger` lock pattern). |
| 2 | Logger hook | `Assets/Scripts/DDA/DDAAgent.cs` | 2–3 lines in `OnActionReceived` under `#if SHAP_LOG`: call `ShapStateLogger.Log(...)`. Zero effect on normal builds (define off). |
| 3 | State collector | `tools/xai/collect_states.py` | Parse `shap_states.jsonl` → `states.npy[N,6]`, `actions.npy[N]`, `qvalues.npy[N,5]`, `survival.npy[N]` (survival_ratio), `outcomes.npy[N]` (integer-coded outcome_label: 0=Subjugate, 1=Balanced, 2=Rebellious). Validate obs in `[0,1]`; clamp + warn on violation. Validate `survival_ratio` in `[0,1]` (clamp). |
| 4 | Q-net replica | `tools/xai/shap_net.py` | `ShapQNet(obs=6, hidden=128, layers=2, actions=5)` with `Norm` layer (running mean/var + eps). `load_checkpoint(path)`: inspect ML-Agents checkpoint keys, map norm stats + encoder linears + value head. `forward(x)→[B,5]`. Fallback B path via `--backend plugin`. |
| 5 | Explainer | `tools/xai/explain_shap.py` | CLI: `--checkpoint`, `--states`, `--decision N` (index or "representative"), `--all-actions`, `--counterfactual`, `--filter-outcome {Subjugate,Balanced,Rebellious}` (restrict explained + background subset to one outcome category; default all), `--self-check`. Builds `GradientExplainer(net, background)` (background = random 100 from the selected subset of `states.npy`, or synthetic uniform if empty). Emits waterfalls, beeswarm, counterfactual diffs. |
| 6 | Report generator | `tools/xai/report.py` (or part of explain_shap) | Auto-write `report.md` with: per-decision top features, **global feature ranking**, **explicit "Pola kegagalan (Subjugate)" section** (dominant SHAP features among Subjugate decisions, not just global ranking), **"Pola kegagalan (Rebellious)" section** (dominant features among Rebellious decisions), counterfactual boundary notes. Per-category analysis: mean |SHAP| per feature restricted to that outcome subset, top-3 drivers, and the typical action chosen. |

### 4.1 Outcome classification

`outcome_label` derived solely from `survival_ratio = hp_final / hp_initial`:

| Label | Rule | Meaning (DDA failure mode) |
|-------|------|----------------------------|
| **Balanced** | `0.4 ≤ SR ≤ 0.6` | Sweet spot — aligns with reward parabola peak at HP 50%. Intended DDA outcome. |
| **Subjugate** | `SR > 0.6` | Under-challenged — agent kept difficulty too low; player retains too much HP, stomps enemies. Failure mode (unengaging). |
| **Rebellious** | `SR < 0.4` | Over-challenged — agent set difficulty too high; player loses too much HP / dies. Failure mode. |

Integer code for `outcomes.npy`: `0=Subjugate, 1=Balanced, 2=Rebellious`. Assignment done in C# at run time (`ShapStateLogger` or `TrainingBattleSimulator` computes label from `hp_initial`/`hp_final` before dumping). `hp_initial == 0` → decision skipped (no division). Thresholds are constants in the logger; not configurable at runtime.

### Output location
`results/shap/499948/` containing:
- `waterfall_decision{d}_action{a}.png` (5 × N_decision plots)
- `summary_beeswarm.png`
- `counterfactual_{feature}_{from}_to_{to}.png` + diff table
- `failure_pattern_subjugate.png`, `failure_pattern_rebellious.png` (per-category mean |SHAP| bar)
- `report.md`

## 5. Data Flow

```
Unity Editor
  scene: BattleSimulationTraining
  model: ddqn_dda-499948.onnx assigned to DDAAgent BehaviorParameters
  define: SHAP_LOG
  mode: instant (_instantMode = true on TrainingBattleSimulator)
  run: ~100 runs (≈1200 battle decisions)
     ↓ ShapStateLogger (#if SHAP_LOG)
  shap_states.jsonl
     ↓ collect_states.py
  states.npy [~1200,6], actions.npy, qvalues.npy
     ↓ background = 100 random rows
  ddqn_dda-499948.pt → shap_net.load_checkpoint() → ShapQNet
     ↓
  GradientExplainer(net, background)
     ↓
  Local: 5 waterfall per chosen decision (Q[a] decomposed per feature)
  Global: beeswarm (SHAP distribution across all decisions, chosen action Q)
  Counterfactual: perturb one obs, re-explain, diff Q per action
     ↓
  results/shap/499948/*.png + report.md
```

Inference run is manual (user runs Unity; agent cannot). Spec records the exact procedure so it is reproducible.

### 5.1 Data provenance & validity

**Source: re-simulasi, bukan log training asli.** The explained states come from a *new* inference run that loads the final trained model (`ddqn_dda-499948.onnx`) and re-plays the `TrainingBattleSimulator` in instant mode. They are **not** states harvested from the original training log (`TrainingLogger` writes text-only event summaries, never the 6-obs vector) and **not** from the training replay buffer (`save_replay_buffer: false`, buffer discarded after training).

Implications:

- **Distribution match (good for the question asked).** Background and explained states are drawn from the *final policy's own visitation distribution* — exactly the policy being explained. SHAP's local-accuracy guarantee holds because background ≈ test distribution. This is the correct regime for "why does the final agent do X."
- **Not the training distribution.** States reflect exploitation (epsilon ≈ 0.05 post-training), not the exploration-heavy distribution seen during learning. SHAP results therefore describe the *deployed* policy, not how it was learned. Do not claim attribution over training-time behavior.
- **Deterministic sim, stochastic policy.** `TrainingBattleSimulator` instant mode is deterministic given seed; the policy is near-greedy at epsilon 0.05, so re-runs are mostly reproducible. Small stochasticity (ε-greedy random actions) means a few decisions may differ between runs — acceptable for thesis-level attribution; report notes run seed.
- **Self-collected background caveat.** Background is on-policy (generated by the same network under explanation). This is standard for post-hoc policy explanation but means SHAP values are relative to *what the final agent typically sees*, not to a hand-defined "neutral" state. Counterfactuals that push observations outside the visited distribution (e.g. HP 0.30 when the agent rarely lets HP drop that low) are extrapolation — report flags them as such.
- **Validity scope line for thesis.** SHAP explanations are valid for the final trained policy on states it actually visits; they are descriptive of deployed behavior, not causal claims about training and not guaranteed off-distribution.

## 6. Checkpoint Loading & Normalization

`ddqn_dda-499948.pt` is ML-Agents format. Keys must be inspected at implementation time (structure varies: commonly `{"policy": {..., "model": <state_dict>, ...}}` or a direct state_dict under a behavior-id key). Extract the network state_dict.

Map to minimal net:
- **Normalization** — `VectorInput` running mean / running var → `ShapQNet.norm.running_mean`, `running_var`. Forward: `(x - running_mean) / sqrt(running_var + eps)`, `eps = 1e-5` (ML-Agents default). These stats **must** be found; without them the replica is not faithful.
- **Encoder** — 2 `Linear` layers (6→128, 128→128) + ReLU. Map `weight`/`bias`.
- **ValueHead** — `ValueHeads` `Linear` (128→5). Map `weight`/`bias`.

If any mapping fails → fallback B (instantiate real `QNetworkDDQN`, `load_state_dict`, wrap `critic_pass`). Log a warning. The `forward→[B,5]` interface stays identical so the explainer does not care which backend ran.

## 7. SHAP Configuration

- **Explainer**: `shap.GradientExplainer(model, background)`. Returns one SHAP array per network output (5 actions) → list of `[N, 6]`.
- **Explained target**: all 5 action Q-values per decision (per chosen option). For global beeswarm, attribute the Q-value of the chosen action.
- **Background**: 100 random rows from `states.npy`. If `states.npy` empty → synthetic uniform `[0,1]^6` with a loud warning (less representative; affects validity).
- **Feature names**: `["HP Ratio", "Turn Count", "Player Level", "Dmg Dealt Ratio", "QTE Accuracy", "Resource Depletion"]`.
- **`nsamples`**: default 200 (GradientExplainer integrates over background samples); tune down if NaN/inf appears.

## 8. Error Handling

| Case | Handling |
|------|----------|
| Checkpoint key mismatch / norm stats missing | Switch to fallback B; warn. |
| `states.npy` empty | Synthetic uniform background; warn loudly (validity caveat in report). |
| SHAP NaN/inf (ReLU dead neuron / gradient explode) | Reduce `nsamples`, use DeepSHAP stabilized gradient, retry; skip decision if persistent. |
| Observation outside `[0,1]` | Clamp + log decision as skipped. |
| Additivity violated (`base_value + sum(shap) ≉ Q`) | Flag decision unreliable in report. |
| Checkpoint file missing | Hard error with path. |

## 9. Testing (self-check, no framework)

`python -m explain_shap --self-check` runs:

1. **Faithfulness** — Load `.pt` via `shap_net`, forward 10 random states. Run same states through `ddqn_dda-499948.onnx` via `onnxruntime`. Assert `max|Q_shap_net − Q_onnx| < 1e-4`. If fail → replica wrong; stop, do not produce plots.
2. **Range** — assert `states.npy` all in `[0,1]`, shape `[N,6]`.
3. **Additivity** — per decision: `|base_value + sum(shap_values[chosen]) − Q[chosen]| < 1e-3`. SHAP property.
4. **Determinism** — run SHAP twice on same state, assert identical results (GradientExplainer is deterministic given fixed background).
5. **Outcome coverage** — assert all three labels present in `outcomes.npy` (Subjugate, Balanced, Rebellious each ≥ 1 decision). If a category is empty, the per-category failure-pattern section is skipped with a note (insufficient data), not fabricated.
6. **Survival sanity** — assert `survival_ratio` consistent with `hp_final/hp_initial` per row; assert label matches threshold rule (§4.1).

All pass → safe to generate thesis plots.

## 10. Inference Run Procedure (manual, in Unity)

1. Open `BattleSimulationTraining.unity`.
2. Assign `results/ddqn_retrain_sidang/ddqn_dda/ddqn_dda-499948.onnx` to the DDAAgent `BehaviorParameters` Model.
3. Enable `SHAP_LOG` scripting define (Project Settings → Player → Scripting Define Symbols, or via build).
4. Ensure `TrainingBattleSimulator._instantMode = true`.
5. Press Play. Let it run ~100 runs (instant mode: seconds).
6. Locate `shap_states.jsonl` under `Application.persistentDataPath/DDA_Training/`.
7. Copy to `tools/xai/shap_states.jsonl`.
8. Run `python collect_states.py` → `states.npy` etc.
9. Run `python -m explain_shap --self-check` then the explain commands.

## 11. Scope Exclusions (YAGNI)

- No Unity in-game UI for SHAP (deferred; separate spec if needed).
- No dependence plots (only waterfall, beeswarm, counterfactual).
- No multi-checkpoint comparison (only final 499948; can extend later).
- No retraining; no replay buffer enable.
- No change to existing `DDAAgent` logic — only an additive `#if SHAP_LOG` hook and a new logger file.

## 12. Open Items Resolved at Implementation

- Exact ML-Agents checkpoint key layout (inspect `torch.load(...).keys()`).
- `eps` value used by ML-Agents `VectorInput` normalization (confirm default 1e-5).
- Representative decision indices for local waterfalls (pick programmatically: cluster by HP ratio buckets, early vs late run).