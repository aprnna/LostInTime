# SHAP XAI Report — DDQN DDA

**Model:** `ddqn_dda_sidang1.onnx`
**Beta-model match rate:** 77.0% (spec §9.7)
**Decisions explained:** 87

## Global feature ranking (mean |SHAP|, chosen-action)

| Rank | Feature | mean |SHAP| |
|------|---------|-------------|
| 1 | Player Level | 0.1582 |
| 2 | QTE Accuracy | 0.0799 |
| 3 | Turn Count | 0.0587 |
| 4 | Dmg Dealt Ratio | 0.0374 |
| 5 | HP Ratio | 0.0285 |
| 6 | Resource Depletion | 0.0000 |

## Validitas (spec §5.1)

SHAP explanations are computed on the real closed-beta `dda_event` states (DataPost, 87 real agent decisions (cross-event pairing) = basis BCM 15.31%). Weights are extracted from the deployed beta `.onnx` identified by the match-rate probe. Explanations are descriptive of the deployed policy on states it actually met — not causal claims about training. Counterfactuals outside the observed observation range are extrapolation.

# Pola kegagalan (Subjugate)

n = 57 decisions (outcome code 0).

| Rank | Feature | mean |SHAP| |
|------|---------|-------------|
| 1 | Player Level | 0.1747 |
| 2 | QTE Accuracy | 0.0844 |
| 3 | Turn Count | 0.0568 |
| 4 | Dmg Dealt Ratio | 0.0411 |
| 5 | HP Ratio | 0.0314 |
| 6 | Resource Depletion | 0.0000 |

Typical action chosen: **Hard**.


# Pola kegagalan (Rebellious)

n = 15 decisions (outcome code 2).

| Rank | Feature | mean |SHAP| |
|------|---------|-------------|
| 1 | Player Level | 0.0946 |
| 2 | Turn Count | 0.0684 |
| 3 | QTE Accuracy | 0.0598 |
| 4 | Dmg Dealt Ratio | 0.0240 |
| 5 | HP Ratio | 0.0239 |
| 6 | Resource Depletion | 0.0000 |

Typical action chosen: **Very Hard**.


## Counterfactual boundary notes

See `counterfactual_*.png` and diff tables. Perturbations that move an observation outside the observed beta range are flagged as extrapolation.
