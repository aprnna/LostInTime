# tools/xai/parse_dda_logs.py
"""Parse real closed-beta dda_event logs into numpy arrays. Spec §2, §4.1, §5.

CROSS-EVENT pairing (spec §12, resolved empirically — see task-2-report.md):
  In real beta logs each ``dda_event`` is emitted at the END of the battle its
  action was applied to (sandwiched between that battle's ``battle_start`` and
  ``battle_end``). ``dda_action_taken`` == the difficulty of the ENDING battle
  (verified 100% of 98), NOT the next battle (48%). So action_k was chosen from
  the PREVIOUS observation obs_{k-1}, and obs_k is the end-of-battle-k state that
  drives the NEXT decision. The spec §2/§4.1 same-event assumption is empirically
  false; the user approved CROSS-EVENT pairing.

Two-stage build:

  Stage A — unit building (per session, sorted by ts):
    A "unit" k = (obs_k, act_k, sr_k, hp_initial_k, hp_final_k, ts_k, hp_ratio_k)
    where act_k = dda_action_taken of dda_event k, and sr_k = SR of the battle
    ENDING at dda_event k = hp_final_k / hp_initial_k. The state machine pairs each
    dda_event with the most-recent battle_start (hp_initial) and the next
    battle_end (hp_final) after it. This handles both observed orderings:
      - real beta logs:    battle_start -> dda_event -> battle_end
        (hp_initial from the preceding battle_start)
      - fixture/synthetic: dda_event -> battle_start -> battle_end
        (hp_initial supplied by the following battle_start)

  Stage B — cross-event emit (per session):
    for i in range(len(units) - 1):
        state    = units[i].obs               # observation the agent saw
        action   = units[i+1].act_int          # action chosen from obs_i, applied to battle i+1
        survival = units[i+1].sr               # SR of battle i+1 (governed by that action)
        outcome  = survival_to_outcome(survival)
        hp_ratio = units[i].obs_player_hp_ratio  # indexed by the OBSERVATION unit
    This drops each session's LAST unit (no following action) and the FIRST unit's
    action (the baseline — every session's first dda_event action is Normal, verified
    11/11). N = sum_sessions(|units_s| - 1).
"""
import json, os, glob
from collections import defaultdict
import numpy as np
from . import constants as C


def _load_events(paths):
    """Read jsonl paths, return list of (ts, session_id, event_type, payload)."""
    evs = []
    for p in paths:
        with open(p, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                try:
                    d = json.loads(line)
                except json.JSONDecodeError:
                    continue
                evs.append((d.get("ts", ""), d.get("session_id", ""),
                           d.get("event_type", ""), d.get("payload", {}) or {}))
    return evs


def _player_hp(payload):
    """Player HP from a player_performance block.

    battle_start uses ``player_hp`` (hp at battle start = hp_initial).
    battle_end in real beta logs uses ``player_hp_end`` (hp at battle end = hp_final);
    the synthetic fixture uses ``player_hp`` for both. Prefer ``player_hp`` and fall
    back to ``player_hp_end`` so both schemas resolve to the correct value.
    """
    pp = payload.get("player_performance", {}) or {}
    if pp.get("player_hp") is not None:
        return pp.get("player_hp")
    return pp.get("player_hp_end")


def parse_dda_logs(log_paths, out_dir=None):
    """Parse dda_event + aligned battles -> arrays dict (CROSS-EVENT pairing).

    log_paths: list of .jsonl file paths (e.g. DataPost/*.jsonl).
    out_dir: if given, write states/actions/survival/outcomes.npy + meta.json.
    Returns dict with numpy arrays.
    """
    evs = _load_events(log_paths)
    # group by session, sort by ts
    by_session = defaultdict(list)
    for ts, sid, et, pl in evs:
        by_session[sid].append((ts, et, pl))
    for sid in by_session:
        by_session[sid].sort(key=lambda t: t[0])

    states, actions, survival, outcomes, hp_ratios = [], [], [], [], []
    meta_decisions = []  # traceability

    for sid, sess in by_session.items():
        # ---- Stage A: build ordered units for this session ----
        units = []
        pending = None      # most recent dda_event awaiting its battle_end
        hp_initial = None   # hp at the most recent battle_start
        for ts, et, pl in sess:
            if et == "dda_event":
                snap = pl.get("dda_obs_snapshot")
                name = pl.get("dda_action_taken")
                if snap is None or len(snap) != C.OBS_SIZE or name not in C.ACTION_NAME_TO_INT:
                    continue
                pending = {
                    "obs": list(snap),
                    "act_int": C.ACTION_NAME_TO_INT[name],
                    "action_name": name,
                    "hp_ratio": pl.get("player_hp_ratio"),
                    "ts": ts,
                }
                # NOTE: hp_initial is intentionally NOT reset here. It is supplied
                # by the most recent battle_start (preceding in real data, following
                # in the synthetic fixture). See module docstring.
            elif et == "battle_start":
                hp_initial = _player_hp(pl)
            elif et == "battle_end" and pending is not None:
                hp_final = _player_hp(pl)
                if hp_initial is None or hp_initial == 0 or hp_final is None:
                    pending = None
                    hp_initial = None
                    continue
                sr = float(hp_final) / float(hp_initial)
                sr = min(1.0, max(0.0, sr))
                units.append({
                    "obs": pending["obs"],
                    "act_int": pending["act_int"],
                    "action_name": pending["action_name"],
                    "hp_ratio": pending["hp_ratio"],
                    "ts": pending["ts"],
                    "hp_initial": hp_initial,
                    "hp_final": hp_final,
                    "sr": sr,
                })
                pending = None
                hp_initial = None  # clear so the next dda_event needs a fresh battle_start

        # ---- Stage B: cross-event emit within this session ----
        for i in range(len(units) - 1):
            obs_unit = units[i]
            gov_unit = units[i + 1]  # the battle governed by the action chosen from obs_i
            states.append(obs_unit["obs"])
            actions.append(gov_unit["act_int"])
            survival.append(gov_unit["sr"])
            outcomes.append(C.survival_to_outcome(gov_unit["sr"]))
            hp_ratios.append(obs_unit["hp_ratio"])
            meta_decisions.append({
                "session_id": sid,
                "decision_ts": obs_unit["ts"],            # observation time
                "action": gov_unit["action_name"],         # action chosen from obs_i
                "governed_battle_ts": gov_unit["ts"],      # battle the action was applied to
                "hp_initial": gov_unit["hp_initial"],
                "hp_final": gov_unit["hp_final"],
                "survival_ratio": gov_unit["sr"],
                "outcome": C.OUTCOME_INT_TO_NAME[C.survival_to_outcome(gov_unit["sr"])],
            })

    states = np.asarray(states, dtype=np.float32)
    actions = np.asarray(actions, dtype=np.int64)
    survival = np.asarray(survival, dtype=np.float32)
    outcomes = np.asarray(outcomes, dtype=np.int64)
    hp_ratios = np.asarray([hp if hp is not None else np.nan for hp in hp_ratios],
                           dtype=np.float32)

    # range validation (spec §8): clamp obs to [0,1], warn on violation
    n_viol = int(np.sum((states < 0) | (states > 1)))
    if n_viol:
        print(f"[parse_dda_logs] WARN {n_viol} obs values out of [0,1]; clamping")
        states = np.clip(states, 0.0, 1.0)

    out = {
        "states": states, "actions": actions, "survival": survival,
        "outcomes": outcomes, "hp_ratios": hp_ratios,
    }
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)
        np.save(os.path.join(out_dir, "states.npy"), states)
        np.save(os.path.join(out_dir, "actions.npy"), actions)
        np.save(os.path.join(out_dir, "survival.npy"), survival)
        np.save(os.path.join(out_dir, "outcomes.npy"), outcomes)
        meta = {
            "n_decisions": int(states.shape[0]),
            "feature_names": C.FEATURE_NAMES,
            "pairing": "cross_event",
            "decisions": meta_decisions,
        }
        with open(os.path.join(out_dir, "meta.json"), "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2)
        with open(os.path.join(out_dir, "log_paths.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(str(p) for p in log_paths))
    return out


def main():
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--log-dir", default=r"E:\COLLEGE\SKOM\Implementasi\Battle Logs\DataPost")
    ap.add_argument("--out-dir", default="xai")
    a = ap.parse_args()
    paths = sorted(glob.glob(os.path.join(a.log_dir, "*.jsonl")))
    if not paths:
        raise SystemExit(f"no jsonl in {a.log_dir}")
    out = parse_dda_logs(paths, out_dir=a.out_dir)
    print(f"parsed {out['states'].shape[0]} decisions from {len(paths)} files")
    # spec §9.7 traceability: action distribution
    for k, v in C.ACTION_INT_TO_NAME.items():
        n = int(np.sum(out["actions"] == k))
        print(f"  {v}: {n}")


if __name__ == "__main__":
    main()