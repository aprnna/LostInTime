#!/usr/bin/env python3
"""
Migrate old battle JSONL logs to new enriched format.

Infers player state from actual gameplay data:
- base_damage: from non-critical damage values in turn logs
- max_hp: from highest player_start_hp seen (rest heals to max)
- level: count of exp overflow events
- base_defend: tracked via level up detection (when max_hp unchanged but base_defend changed)

Usage: python migrate_logs.py old_battle_xxx.jsonl
Output: old_battle_xxx_migrated.jsonl
"""

import json
import sys
import os
from datetime import datetime

# --- Game Design Constants ---
ENEMY_REWARDS = {
    "Caveman":     {"exp": 10,  "coin": 5},
    "Sabertooth":  {"exp": 25,  "coin": 10},
    "Raptor":      {"exp": 30,  "coin": 10},
    "TRex":        {"exp": 100, "coin": 30},
    "Triceratops": {"exp": 150, "coin": 50},
}

INIT_MAX_HP = 100
INIT_BASE_DEFEND = 2
INIT_SHIELD = 2
INIT_MAX_SHIELD = 2
INIT_LEVEL = 1
INIT_MAX_EXP = 100
LEVEL_UP_MAX_EXP_BONUS = 25

ACTION_CONFIGS = {
    "Fist":   {"percentage_damage": 30,  "limit": 0,  "critical_pct": 10},
    "Sword":  {"percentage_damage": 90,  "limit": 15, "critical_pct": 10},
    "Gun":    {"percentage_damage": 100, "limit": 10, "critical_pct": 10},
    "Shield": {"percentage_damage": 0,   "limit": None, "critical_pct": 0},
}


def infer_area_type(level_id: str) -> str:
    if level_id.startswith("Boss"):
        return "Boss"
    return "Enemy"


def get_enemy_rewards(enemy_list: list) -> tuple[int, int]:
    total_exp, total_coin = 0, 0
    for enemy in enemy_list:
        name = enemy.get("enemy_id", "")
        r = ENEMY_REWARDS.get(name, {"exp": 0, "coin": 0})
        total_exp += r["exp"]
        total_coin += r["coin"]
    return total_exp, total_coin


def infer_base_damage(turn_logs: list) -> int | None:
    """Infer base_damage from non-critical hits."""
    for log in turn_logs:
        if log.get("actor") != "PlayerTurn" or log.get("is_critical", False):
            continue
        action = log.get("action", "")
        damage = log.get("damage", 0)
        if action == "Gun" and damage > 0:
            return damage  # Gun = 100% = base_damage
        elif action == "Sword" and damage > 0:
            return round(damage / 0.9)
        elif action == "Fist" and damage > 0:
            return round(damage / 0.3)
    return None


class PlayerState:
    def __init__(self):
        self.level = INIT_LEVEL
        self.exp = 0
        self.max_exp = INIT_MAX_EXP
        self.max_hp = INIT_MAX_HP
        self.hp = INIT_MAX_HP
        self.base_damage = 12  # will be inferred
        self.base_defend = INIT_BASE_DEFEND
        self.shield = INIT_SHIELD
        self.max_shield = INIT_MAX_SHIELD
        self.defend = 0
        self.coin = 0

    def on_battle_start(self, payload: dict):
        """Update HP from battle start (detect heals/rest)."""
        start_hp = payload.get("player_start_hp", self.hp)
        # If start_hp > current hp, player was healed
        # If start_hp > max_hp, max_hp increased (level up HP or item)
        if start_hp > self.max_hp:
            self.max_hp = start_hp
        self.hp = min(start_hp, self.max_hp)

    def on_battle_end(self, payload: dict, won: bool):
        """Update state after battle ends."""
        # Update HP
        self.hp = payload.get("player_performance", {}).get("player_hp_end", self.hp)

        # Update base_damage from turn logs
        new_dmg = infer_base_damage(payload.get("turn_logs", []))
        if new_dmg is not None and new_dmg > 0:
            self.base_damage = new_dmg

        # Add exp/coin if won
        exp_earned, coin_earned = 0, 0
        if won:
            enemy_list = payload.get("enemy_list", [])
            exp_earned, coin_earned = get_enemy_rewards(enemy_list)
            self.exp += exp_earned
            self.coin += coin_earned

            # Check level ups
            while self.exp >= self.max_exp:
                self.exp -= self.max_exp
                self.level += 1
                self.max_exp += LEVEL_UP_MAX_EXP_BONUS

        return exp_earned, coin_earned

    def to_battle_start_perf(self) -> dict:
        return {
            "player_level": self.level,
            "player_hp": self.hp,
            "player_max_hp": self.max_hp,
            "player_defend": self.defend,
            "player_base_defend": self.base_defend,
            "player_base_damage": self.base_damage,
            "player_exp": self.exp,
            "player_max_exp": self.max_exp,
            "player_coin": self.coin,
        }

    def to_battle_end_perf(self, old_perf: dict) -> dict:
        return {
            "player_hp_start": old_perf.get("player_hp_start", 0),
            "player_hp_end": old_perf.get("player_hp_end", 0),
            "player_max_hp": self.max_hp,
            "player_defend": 0,
            "player_base_defend": self.base_defend,
            "player_level": self.level,
            "player_base_damage": self.base_damage,
            "player_exp": self.exp,
            "player_max_exp": self.max_exp,
            "player_coin": self.coin,
            "damage_dealt": old_perf.get("damage_dealt", 0),
            "damage_taken": old_perf.get("damage_taken", 0),
        }


def transform_battle_start(payload: dict, player: PlayerState, battle_exp: int, battle_coin: int) -> dict:
    return {
        "level_id": payload.get("level_id"),
        "area_type": infer_area_type(payload.get("level_id", "")),
        "hp_multiplier": 1.0,
        "damage_multiplier": 1.0,
        "enemy_count": payload.get("enemy_count", 0),
        "total_enemy_start_hp": payload.get("total_enemy_start_hp", 0),
        "exp_available": battle_exp,
        "coin_available": battle_coin,
        "player_performance": player.to_battle_start_perf(),
    }


def transform_player_turn(payload: dict, player: PlayerState) -> dict:
    return {
        "player_turn": payload.get("turn", 0),
        "player_level": player.level,
        "action": payload.get("action"),
        "target": payload.get("target"),
        "target_hp_before": payload.get("target_hp_before", 0),
        "target_hp_after": payload.get("target_hp_after", 0),
        "damage": payload.get("damage", 0),
        "is_critical": payload.get("is_critical", False),
    }


def transform_enemy_turn(payload: dict, player: PlayerState) -> dict:
    return {
        "enemy_turn": payload.get("turn", 0),
        "player_level": player.level,
        "player_hp_before": payload.get("player_hp_before", 0),
        "player_hp_after": payload.get("player_hp_after", 0),
        "damage": payload.get("damage", 0),
    }


def transform_turn_logs(turn_logs: list) -> list:
    result = []
    for t in turn_logs:
        result.append({
            "turn_number": t.get("turn", 0),
            "actor": t.get("actor"),
            "target": t.get("target"),
            "action": t.get("action"),
            "damage": t.get("damage", 0),
            "is_critical": t.get("is_critical", False),
            "target_hp_before": t.get("targetHPBefore", 0),
            "target_hp_after": t.get("targetHPAfter", 0),
            "description": t.get("description", ""),
        })
    return result


def transform_battle_end(payload: dict, player: PlayerState, exp_earned: int, coin_earned: int) -> dict:
    turn_logs = payload.get("turn_logs", [])
    player_turn_count = sum(1 for t in turn_logs if t.get("actor") == "PlayerTurn")
    enemy_turn_count = sum(1 for t in turn_logs if t.get("actor") == "EnemyTurn")
    old_perf = payload.get("player_performance", {})
    old_behavior = payload.get("player_behavior", {})

    return {
        "level_id": payload.get("level_id"),
        "area_type": infer_area_type(payload.get("level_id", "")),
        "battle_result": payload.get("battle_result"),
        "battle_duration": old_perf.get("battle_duration", 0),
        "player_turn_count": player_turn_count,
        "enemy_turn_count": enemy_turn_count,
        "enemy_count": payload.get("Enemy_count", 0),
        "enemy_list": payload.get("enemy_list", []),
        "enemy_total_hp_start": old_perf.get("enemy_total_hp_start", 0),
        "enemy_total_hp_end": old_perf.get("enemy_total_hp_end", 0),
        "exp_earned": exp_earned,
        "coin_earned": coin_earned,
        "player_performance": player.to_battle_end_perf(old_perf),
        "player_behavior": {
            "fist_used": old_behavior.get("fist_used", 0),
            "sword_used": old_behavior.get("sword_used", 0),
            "gun_used": old_behavior.get("gun_used", 0),
            "defend_used": old_behavior.get("defend_used", 0),
            "critical_success": old_behavior.get("critical_success", 0),
            "fist_max_usage": ACTION_CONFIGS["Fist"]["limit"],
            "sword_max_usage": ACTION_CONFIGS["Sword"]["limit"],
            "gun_max_usage": ACTION_CONFIGS["Gun"]["limit"],
            "defend_max_usage": player.shield,
            "fist_critical_pct": ACTION_CONFIGS["Fist"]["critical_pct"],
            "sword_critical_pct": ACTION_CONFIGS["Sword"]["critical_pct"],
            "gun_critical_pct": ACTION_CONFIGS["Gun"]["critical_pct"],
        },
        "turn_logs": transform_turn_logs(turn_logs),
    }


def migrate_file(input_path: str):
    output_path = input_path.replace(".jsonl", "_migrated.jsonl")
    player = PlayerState()

    # Pre-scan: group events by battle
    battles = []
    current_battle = []
    with open(input_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            et = event.get("event_type", "")
            if et == "battle_start":
                if current_battle:
                    battles.append(current_battle)
                current_battle = [event]
            elif current_battle:
                current_battle.append(event)
        if current_battle:
            battles.append(current_battle)

    # Process events
    output_lines = []
    battle_index = 0

    with open(input_path, "r", encoding="utf-8") as f:
        for line in f:
            stripped = line.strip()
            if not stripped:
                continue
            if stripped.startswith("#"):
                output_lines.append(stripped)
                continue
            try:
                event = json.loads(stripped)
            except json.JSONDecodeError:
                output_lines.append(stripped)
                continue

            et = event.get("event_type", "")
            payload = event.get("payload", {})
            ts = event.get("ts", "")
            sid = event.get("session_id", "")

            if et == "session_start" or et == "session_end":
                output_lines.append(json.dumps(event, ensure_ascii=False))

            elif et == "battle_start":
                # Get exp/coin from battle_end's enemy_list
                battle_exp, battle_coin = 0, 0
                if battle_index < len(battles):
                    for be in battles[battle_index]:
                        if be.get("event_type") == "battle_end":
                            battle_exp, battle_coin = get_enemy_rewards(
                                be.get("payload", {}).get("enemy_list", [])
                            )
                            break

                player.on_battle_start(payload)
                new_payload = transform_battle_start(payload, player, battle_exp, battle_coin)
                output_lines.append(json.dumps(
                    {"ts": ts, "session_id": sid, "event_type": "battle_start", "payload": new_payload},
                    ensure_ascii=False
                ))

            elif et == "player_turn":
                new_payload = transform_player_turn(payload, player)
                output_lines.append(json.dumps(
                    {"ts": ts, "session_id": sid, "event_type": "player_turn", "payload": new_payload},
                    ensure_ascii=False
                ))

            elif et == "enemy_turn":
                new_payload = transform_enemy_turn(payload, player)
                output_lines.append(json.dumps(
                    {"ts": ts, "session_id": sid, "event_type": "enemy_turn", "payload": new_payload},
                    ensure_ascii=False
                ))

            elif et == "battle_end":
                won = payload.get("battle_result") == "Win"
                exp_earned, coin_earned = player.on_battle_end(payload, won)
                new_payload = transform_battle_end(payload, player, exp_earned, coin_earned)
                output_lines.append(json.dumps(
                    {"ts": ts, "session_id": sid, "event_type": "battle_end", "payload": new_payload},
                    ensure_ascii=False
                ))
                battle_index += 1

            else:
                output_lines.append(json.dumps(event, ensure_ascii=False))

    with open(output_path, "w", encoding="utf-8") as f:
        for line in output_lines:
            f.write(line + "\n")

    print(f"[OK] {os.path.basename(input_path)} -> {os.path.basename(output_path)}")
    print(f"  Battles: {battle_index}")
    print(f"  Final: Level={player.level}, EXP={player.exp}/{player.max_exp}, "
          f"BaseDMG={player.base_damage}, BaseDEF={player.base_defend}, "
          f"MaxHP={player.max_hp}, Coin={player.coin}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        for fname in sorted(os.listdir(script_dir)):
            if fname.startswith("old_battle_") and fname.endswith(".jsonl") and "_migrated" not in fname:
                migrate_file(os.path.join(script_dir, fname))
    else:
        for path in sys.argv[1:]:
            migrate_file(path)
