"""Single source of truth for SHAP XAI pipeline. See spec §2, §4.1."""

OBS_SIZE = 6
ACTION_SIZE = 5

FEATURE_NAMES = [
    "HP Ratio",            # dda_obs_snapshot[0]  (== player_hp_ratio, cross-checked)
    "Turn Count",          # [1] turn/15
    "Player Level",        # [2] level/5
    "Dmg Dealt Ratio",     # [3] areaTotalEnemyHP / damageDealt
    "QTE Accuracy",        # [4] successfulQTE / totalQTE
    "Resource Depletion",  # [5]
]

ACTION_NAME_TO_INT = {
    "Very Easy": 0, "Easy": 1, "Normal": 2, "Hard": 3, "Very Hard": 4,
}
ACTION_INT_TO_NAME = {v: k for k, v in ACTION_NAME_TO_INT.items()}

# outcome_label from survival_ratio = hp_final / hp_initial (spec §4.1)
OUTCOME_CODES = {"Subjugate": 0, "Balanced": 1, "Rebellious": 2}
OUTCOME_INT_TO_NAME = {v: k for k, v in OUTCOME_CODES.items()}
SR_BALANCED = (0.4, 0.6)  # inclusive


def survival_to_outcome(sr: float) -> int:
    """Return outcome int code from survival_ratio. spec §4.1."""
    lo, hi = SR_BALANCED
    if sr < lo:
        return OUTCOME_CODES["Rebellious"]
    if sr > hi:
        return OUTCOME_CODES["Subjugate"]
    return OUTCOME_CODES["Balanced"]