# PRD — DDQN DDA Convergence Fix

## Problem Statement

The DDQN-based Dynamic Difficulty Adjustment (DDA) agent in *Lost In Time* does not converge during ML-Agents training. After 2,000,000 steps across 4 parallel environments (run `test6`), the agent learns nothing useful:

- `CumulativeReward` mean stays negative (-0.0555, never crosses 0).
- `Policy.ExtrinsicValue` (Q-values) never leaves ~0 (max 0.0026 over the entire run) — the network assigns no value to any state-action.
- `Losses/ValueLoss` is near-zero because both target and prediction sit at ~0.
- Training battle logs show the simulated player dies at Area 3 in every run, yielding a fixed -0.542 cumulative reward regardless of what the agent does.

From the researcher's perspective: the thesis experiment comparing ML-based DDA against baseline approaches cannot be run, because the ML agent has not learned a policy at all. The comparison is meaningless until the agent converges to a usable difficulty-adjustment policy.

Root-cause analysis (performed against the codebase and training logs) identified two compounding defects:

1. **Instance-mismatch bug (structural).** The `TrainingBattleSimulator` creates a private runtime copy of `DifficultySettings` for itself, while the `DDAAgent` mutates a separate `DifficultySettings` instance it holds via its own serialized reference. No code ever shares the simulator's instance with the agent. As a result the agent's `Increase`/`Decrease` actions mutate one object, but the simulator reads another (always Normal, multipliers 1.00x) when applying difficulty to the next battle area. The agent's entire action space is disconnected from the environment, so no action has any effect on reward — Q-values cannot form and collapse to zero. This is the primary blocker of convergence.
2. **Environment unwinnability (calibration).** With player skill fixed at 0.5, the simulated player cannot survive Area 3 (three enemies, depleted action resources) at Normal difficulty, dying every run. There is no winnable positive-reward region reachable by lowering difficulty, so the reward signal is dominated by the -1.0 loss penalty and offers no learnable gradient even after the instance bug is fixed.

The DDQN algorithm itself (online network selects the greedy action, target network evaluates it, soft target update via tau) is implemented correctly in the custom trainer plugin and is not the cause.

## Solution

Make the DDQN DDA agent converge to a difficulty-adjustment policy by (a) restoring the causal link between the agent's actions and the environment, (b) calibrating the environment so difficulty choices span a win→lose gradient, (c) reshaping the reward into a cleaner signal, (d) tuning off-policy hyperparameters for the 12-area episode horizon, and (e) reducing per-battle reward noise.

Concretely:

- **Fix the instance mismatch.** The training simulator owns one `DifficultySettings` instance and shares it with the agent, so the agent's action mutates the exact object the simulator reads when applying difficulty to the next battle area. The agent's actions become effective.
- **Create a win→lose difficulty gradient.** Widen the difficulty multiplier range and raise the simulated player's skill so that the easiest difficulty lets the player win with high HP (low reward) while the hardest difficulty causes a loss (-0.5), with the HP 50% "sweet spot" reachable in between. The agent's action now moves the player along a learnable gradient.
- **Reshape the reward.** Broaden the sweet-spot band, soften the loss penalty so it does not dominate the mean early in training, and keep the reward at the area-completion level (no per-turn dense shaping) to preserve the thesis-friendly reward structure.
- **Tune hyperparameters.** Raise the effective discount factor so the run-completion bonus propagates back to early-area decisions; enlarge the replay buffer and batch for stable off-policy updates; lengthen the time horizon so the 92-step episode is not over-truncated, preserving long-horizon credit.
- **Reduce reward variance.** Narrow the damage-roulette range so the same difficulty yields more consistent HP outcomes, cutting the per-episode reward swings while keeping the random map path (so the agent still generalizes across map branches).

The DDQN algorithm, the agent's observation/action structure, and the trainer plugin remain unchanged (frozen as the thesis contribution being compared).

## User Stories

1. As a thesis researcher, I want the DDA agent's difficulty actions to actually affect the next battle's enemy stats, so that the agent can learn a causal policy instead of no-ops.
2. As a thesis researcher, I want a single shared difficulty-state instance between the simulator and the agent, so that there is one source of truth and no silent desynchronization.
3. As a thesis researcher, I want the training battle log to show the agent's chosen difficulty reflected in the next area's applied HP/damage multipliers, so that I can verify the action→environment link is intact.
4. As a thesis researcher, I want the simulated player to be able to win battles at the easiest difficulty and lose at the hardest, so that there is a learnable reward gradient across the difficulty range.
5. As a thesis researcher, I want the difficulty multiplier range widened beyond 0.75x–1.25x, so that a single difficulty step produces a visible change in battle outcome.
6. As a thesis researcher, I want the simulated player's skill raised moderately, so that Normal difficulty is survivable and the full difficulty range spans win to loss.
7. As a thesis researcher, I want the reward sweet spot broadened, so that the +1.0 peak reward is reachable more often and provides a denser positive signal.
8. As a thesis researcher, I want the loss penalty softened, so that early-training deaths do not dominate the mean reward and obscure learning progress.
9. As a thesis researcher, I want the reward to remain at the area-completion level, so that the reward structure stays comparable to the documented design and defensible in the thesis.
10. As a thesis researcher, I want the discount factor raised, so that the run-completion bonus propagates back to early-area difficulty decisions over the 12-area horizon.
11. As a thesis researcher, I want the replay buffer and batch size enlarged, so that off-policy Q-updates are stable and less noisy.
12. As a thesis researcher, I want the time horizon lengthened, so that the long episode is not over-truncated and long-horizon credit is preserved.
13. As a thesis researcher, I want the effective discount factor to be set in the reward signal (where the optimizer actually reads it), so that the configuration's stated gamma matches the gamma used in training.
14. As a thesis researcher, I want the damage-roulette variance narrowed, so that per-battle HP outcomes are more consistent and reward noise is reduced.
15. As a thesis researcher, I want the random map path kept across runs, so that the agent generalizes over map branches rather than overfitting one path.
16. As a thesis researcher, I want to re-run training after the fix and observe `CumulativeReward` mean rising above zero and stabilizing, so that I have evidence the agent is learning.
17. As a thesis researcher, I want to observe `Policy.ExtrinsicValue` (Q) moving away from zero and stabilizing, so that I can confirm the network is assigning value to actions.
18. As a thesis researcher, I want to observe `Losses/ValueLoss` rising then falling, so that I can confirm a learning signal is present and being fit.
19. As a thesis researcher, I want to observe the win rate climbing and the player's end-of-area HP distribution centering on the 40–60% sweet spot, so that I can confirm the agent drives difficulty toward the target challenge.
20. As a thesis researcher, I want the instance-mismatch fix applied first and verified before any other change, so that the prerequisite causal link is established before tuning.
21. As a thesis researcher, I want a documented convergence-criteria checklist tied to TensorBoard gauges, so that I can objectively judge whether a training run converged.
22. As a thesis researcher, I want the DDQN algorithm and agent observation/action structure left unchanged, so that the thesis comparison (ML-DDA vs baseline) remains valid.
23. As a thesis researcher, I want configuration changes limited to reward design, difficulty calibration, simulator balance, and hyperparameters, so that the scope of "what is tunable" is explicit and defensible.
24. As a thesis researcher, I want training run outputs (checkpoints, run logs, TensorBoard events) preserved per run-id under the existing results layout, so that runs are comparable.
25. As a thesis researcher, I want the existing training command unchanged in form, so that re-running training requires no new tooling.

## Implementation Decisions

- **Single difficulty-state instance.** The `TrainingBattleSimulator` creates its runtime copy of `DifficultySettings` (for multi-environment independence, as already intended) and then passes that same instance to the `DDAAgent` via a new setter on the agent. The agent stores and mutates this shared instance. The simulator reads the same instance when applying difficulty to the next battle area. This restores the action→environment causal link and is the prerequisite for all other changes. The agent's existing `OnRunStart` reset-to-normal and `OnActionReceived` increase/decrease now operate on the instance the simulator consumes.

- **Agent interface change.** `DDAAgent` gains a method to receive the shared `DifficultySettings` instance (replacing its current behavior of using a separately-loaded or serialized reference). The agent's initialization fallback (loading from Resources when no reference is provided) is retained for the real-game integration path, but in the training simulator path the simulator-supplied instance takes precedence.

- **Difficulty range widening.** The `DifficultySettings` level multiplier array is widened from the current five-level 0.75x–1.25x band to a broader 0.6x–1.4x band (kept as five discrete levels for action-space compatibility: Maintain/Increase/Decrease still moves ±1 level). This makes a single level change produce a visible battle-outcome shift. The level-index math, normalization, reset, and runtime-copy behavior of the module are unchanged. (Prototype-derived decision — current array: `{ 0.75f, 0.875f, 1.0f, 1.125f, 1.25f }`; target array: a five-point spread across 0.6x–1.4x, e.g. `{ 0.6f, 0.8f, 1.0f, 1.2f, 1.4f }`.)

- **Player skill calibration.** The simulated player's skill parameter is raised from 0.5 to 0.7 so that Normal difficulty is survivable and the full difficulty range spans win (easiest) to loss (hardest). Exact value is a calibration knob to be confirmed against battle logs post-fix.

- **Reward reshape (area-level, static).** The area-completion reward function is reshaped while remaining a pure static method at the area level (no per-turn dense shaping). Loss penalty softened from -1.0 to -0.5; sweet-spot band broadened from 40–60% to 35–65%; the parabolic peak at HP 50% (1.0) is retained; the too-easy and too-hard linear regions are retained with their existing slopes. Run-completion bonus (+0.5 win / -0.1 loss) and progressive weighting (0.5 + 0.5 × progress) are retained. (Prototype-derived decision — current reward shape encodes: `if !won return -1.0`; sweet spot `1.0 - 25.0 * (hp - 0.5)^2` on 40–60%; too-easy `(1 - hp) * 0.5`; too-hard `hp * 0.3`. Target: loss `-0.5`, sweet-spot band `35–65%`, parabola and linear regions preserved.)

- **Hyperparameter set.** The off-policy hyperparameters are tuned for the 12-area episode horizon: effective discount factor (set in the extrinsic reward signal, which is the gamma the DDQN optimizer actually reads) raised from 0.90 to 0.99; the hyperparameters-level gamma is set to match for documentation clarity (it is otherwise unused by the optimizer). Replay buffer enlarged from 10000 to 50000. Batch size raised from 64 to 128. Time horizon raised from 32 to 64 to reduce bootstrap truncation bias on the ~92-step episode. Unchanged: learning rate 0.0003 (constant), tau 0.005 (soft target update), steps_per_update 4, exploration epsilon decay 1.0 → 0.03 over 1,000,000 steps, max_steps 2,000,000, num_envs 4.

- **Variance reduction.** The damage-roulette calculation in the simulated battle AI is narrowed so that, for a given difficulty, HP outcomes vary less run-to-run. The random map path construction is left in place so the agent continues to see varied map branches.

- **Scope boundary.** The DDQN trainer plugin (`ddqn_optimizer.py`, `networks.py`, trainer registration), the agent's 10-observation / 3-action structure, and the Double Q-learning update formula are frozen and not modified. The real-game `DDAIntegration` path is out of scope; only the training simulator path is changed.

- **Validation approach (no automated tests).** Per explicit decision, no unit or integration tests are written. Convergence is validated by re-running training and inspecting TensorBoard gauges and the training battle log. The convergence criteria are enumerated in Testing Decisions. The instance-sharing contract therefore carries unmitigated regression risk; this is accepted by the user.

## Testing Decisions

- **No automated unit or integration tests** are written for this change, per explicit user decision. The regression guard on the instance-sharing contract is therefore absent; this risk is accepted.
- **Validation is behavioral, via training-run metrics.** A training run is judged converged by observing the external behavior of the trained policy and the learning curves, not implementation internals. Convergence criteria (all must hold on a smoothed/mean view, not raw per-episode swings):
  - `Environment.CumulativeReward` mean rises above 0 and stabilizes.
  - `Policy.ExtrinsicValue` (Q) mean moves away from 0 and stabilizes (the network is assigning value to actions).
  - `Losses/ValueLoss` rises (signal present) then falls (fit), rather than staying flat near zero.
  - Battle win rate climbs; end-of-area HP distribution centers on the 40–60% sweet spot.
  - The training battle log shows agent-chosen difficulty reflected in the next area's applied HP/damage multipliers (verifies the instance-mismatch fix).
- **Prior art for this validation.** The existing training runs (`results/test1`–`test6`) and their `run_logs/timers.json` gauges (`CumulativeReward`, `ExtrinsicValue`, `ValueLoss`, `epsilon`, `EpisodeLength`) are the established format for judging a run; the same gauges are used post-fix for direct before/after comparison. The existing `TrainingLogger` battle log format (`[AGENT ACTION]`, `[AREA ENTER] ... HP Mult ... DMG Mult`, `[AREA COMPLETE] ... AreaReward`, `[RUN END] ... CumulativeReward`) is the established format for verifying action→environment linkage.
- **Sequencing test.** The instance-mismatch fix is verified via the battle log (agent action → next area multiplier match) before any other change is applied, because without it no other change can produce a learnable signal.

## Out of Scope

- Modifying the DDQN algorithm, network architecture, activation function, or trainer plugin internals (frozen as the thesis contribution).
- Changing the agent's observation set (10 observations) or action space (3 discrete actions: Maintain/Increase/Decrease).
- Changing the real-game `DDAIntegration` / `DifficultyApplier` runtime path used during actual gameplay (only the training simulator path is in scope).
- Increasing parallel environments beyond 4.
- Implementing the baseline-comparison experiments themselves (this PRD only makes the agent converge; running the thesis comparison is separate).
- Unit or integration automated tests (explicitly excluded by user decision).
- Curriculum/staged-difficulty training schedules (considered and rejected in favor of a single widened range plus raised skill).
- Per-turn dense reward shaping (rejected to keep the reward area-level and thesis-comparable).
- Fixing the map path during training (rejected to preserve map-branch generalization).

## Further Notes

- **Run order is mandatory.** The instance-mismatch fix is a prerequisite for every other change. With the agent's actions ineffective, no amount of reward, calibration, or hyperparameter tuning can produce convergence. Verify the fix via the battle log (`[AGENT ACTION]` chosen level == next `[AREA ENTER]` applied multiplier) before proceeding.
- **Effective gamma location.** The DDQN optimizer reads gamma from the reward signal's extrinsic entry (`self.gammas` derived from `reward_signals.extrinsic.gamma`), not from the top-level `hyperparameters.gamma`. The config currently sets `hyperparameters.gamma: 0.97` and `reward_signals.extrinsic.gamma: 0.90`; the latter is the effective value. Both must be set to 0.99 to make the config self-consistent.
- **Retraining command (unchanged form).** Re-run with the same `mlagents-learn config/ddqn.yaml --run-id=<new-run> --train --num-envs=4` against the built training executable. New results land under `results/<new-run>/` per the existing layout.
- **What "convergence" is not.** Raw per-episode `CumulativeReward` will still swing run-to-run because the random map path and residual damage variance remain. Convergence is judged on the smoothed mean trend and Q-value stabilization, not on flatness of the raw reward line.
- **Sources consulted for DDQN stability.** van Hasselt, Guez, Silver — *Deep Reinforcement Learning with Double Q-learning* (AAAI 2016); Fujimoto et al. — *Addressing Function Approximation Error in Actor-Critic Methods* (TD3, ICML 2018). These informed the target-update and gamma decisions but required no algorithm change.