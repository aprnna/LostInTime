from xai import constants as C

def test_feature_names_length():
    assert len(C.FEATURE_NAMES) == 6
    assert C.FEATURE_NAMES[0] == "HP Ratio"
    assert C.FEATURE_NAMES[5] == "Resource Depletion"

def test_action_maps():
    assert C.ACTION_NAME_TO_INT["Very Hard"] == 4
    assert C.ACTION_INT_TO_NAME[2] == "Normal"
    assert len(C.ACTION_NAME_TO_INT) == 5

def test_outcome_codes():
    assert C.OUTCOME_CODES == {"Subjugate": 0, "Balanced": 1, "Rebellious": 2}

def test_dims():
    assert C.OBS_SIZE == 6 and C.ACTION_SIZE == 5