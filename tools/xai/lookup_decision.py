# tools/xai/lookup_decision.py
"""Look up decision metadata by index: session, agent action, outcome, state vector.

Usage:
  python -m tools.xai.lookup_decision 72
  python -m tools.xai.lookup_decision --all
  python -m tools.xai.lookup_decision 0 72 86
"""

import argparse
import json
import os

import numpy as np

from . import constants as C


def load(states_path, meta_path):
    states = np.load(states_path).astype(np.float32)
    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)
    return states, meta["decisions"]


def print_decision(idx, state, d, with_state):
    print(f"=== Decision {idx} ===")
    print(f"  session_id        : {d['session_id']}")
    print(f"  decision_ts        : {d['decision_ts']}")
    print(f"  aksi agent         : {d['action']}")
    print(f"  governed_battle_ts : {d['governed_battle_ts']}")
    print(f"  HP                 : {d['hp_initial']} -> {d['hp_final']}")
    print(f"  survival_ratio     : {d['survival_ratio']:.3f}")
    print(f"  outcome            : {d['outcome']}")
    if with_state:
        print(f"  state (fitur)      :")
        for name, v in zip(C.FEATURE_NAMES, state):
            print(f"    {name:20s} = {v:.3f}")
    print()


def main():
    ap = argparse.ArgumentParser(description="Lookup decision metadata by index")
    ap.add_argument("indices", nargs="*", type=int, help="Decision index (0-%d)" % 86)
    ap.add_argument("--all", action="store_true", help="Print all decisions")
    ap.add_argument("--states", default="xai/states.npy")
    ap.add_argument("--meta", default="xai/meta.json")
    ap.add_argument("--no-state", action="store_true", help="Skip state vector print")
    args = ap.parse_args()

    states, decisions = load(args.states, args.meta)
    n = len(decisions)

    if args.all:
        for i, d in enumerate(decisions):
            print_decision(i, states[i], d, with_state=False)
        return

    if not args.indices:
        ap.error("berikan index, contoh: python -m tools.xai.lookup_decision 72  (atau --all)")

    for i in args.indices:
        if not 0 <= i < n:
            print(f"Decision {i} di luar rentang (0-{n-1}), dilewati.\n")
            continue
        print_decision(i, states[i], decisions[i], with_state=not args.no_state)


if __name__ == "__main__":
    main()