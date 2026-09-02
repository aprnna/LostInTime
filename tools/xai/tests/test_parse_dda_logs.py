# tools/xai/tests/test_parse_dda_logs.py
import json, os, numpy as np
from xai import parse_dda_logs as P
from xai import constants as C

FIX = os.path.join(os.path.dirname(__file__), "fixtures", "mini_session.jsonl")
FIX3 = os.path.join(os.path.dirname(__file__), "fixtures", "mini_session_3.jsonl")


def test_parse_mini_session(tmp_path):
    out = P.parse_dda_logs([FIX], out_dir=str(tmp_path))
    # fixture has 2 dda_events -> cross-event pairing emits 1 row
    # (obs_1 paired with act_2 = Very Hard, governed battle B: hp 96->38)
    assert out["states"].shape == (1, 6)
    assert out["actions"].tolist() == [4]  # act_2 = Very Hard, chosen from obs_1
    assert out["survival"].shape == (1,)
    assert 0.0 <= out["survival"].min() and out["survival"].max() <= 1.0
    # governed battle B: hp 96->38 => SR 38/96 = 0.3958... => Rebellious(2)
    assert out["outcomes"][0] == C.OUTCOME_CODES["Rebellious"]
    # files written
    for f in ["states.npy", "actions.npy", "survival.npy", "outcomes.npy", "meta.json"]:
        assert os.path.exists(tmp_path / f)


def test_snapshot_index0_is_hp_ratio(tmp_path):
    out = P.parse_dda_logs([FIX], out_dir=str(tmp_path))
    # dda_obs_snapshot[0] must equal the logged player_hp_ratio of the OBSERVATION unit.
    # states[0] = obs_1 (snapshot[0]=0.96), hp_ratios[0] = obs_1's player_hp_ratio (0.96).
    assert np.allclose(out["states"][:, 0], out["hp_ratios"], atol=1e-6)


def test_cross_event_shift_with_three_events(tmp_path):
    """3 dda_events -> 2 cross-event pairs; actions are act_2 then act_3."""
    out = P.parse_dda_logs([FIX3], out_dir=str(tmp_path))
    assert out["states"].shape == (2, 6)
    assert out["actions"].tolist() == [4, 2]  # act_2=Very Hard, act_3=Normal
    assert out["survival"].shape == (2,)
    assert np.allclose(out["states"][:, 0], out["hp_ratios"], atol=1e-6)