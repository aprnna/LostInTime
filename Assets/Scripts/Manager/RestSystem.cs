using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Player;
using Player.Item;
using Playfab;
using Roulette;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Manager
{
    public class RestSystem:MonoBehaviour
    {
        [SerializeField] private GameObject _mainCanvas;
        [SerializeField] private GameObject _buttonPanel;
        [SerializeField] private TMP_Text _textDescription;
        public static RestSystem Instance;
        private PlayerStats _playerStats;
        private RouletteSystem _rouletteSystem;
        private GameManager _gameManager;
        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }else
            {
                Destroy(gameObject);
            }
        }
        public void Start()
        {
            _playerStats = PlayerStats.Instance;
            _rouletteSystem = RouletteSystem.Instance;
            _gameManager = GameManager.Instance;

            _buttonPanel.SetActive(true);
            LogRestEnter();
        }

        public void SetDescription(string value)
        {
            _textDescription.text = value;
        }
        public void OnHoverAction(RestActionController restController)
        {
            switch (restController.RestItem.Type)
            {
                case RestType.Heal: SetDescription($"Take a short break to recover {restController.RestItem.Min}-{restController.RestItem.Max} Health"); break;
                case RestType.Repair: SetDescription($"Repair your weapon to restore its durability. Recovers {restController.RestItem.Min}-{restController.RestItem.Max} weapon usage"); break;
            }
        }
        public void OnClickAction(RestActionController restController)
        {
            _buttonPanel.SetActive(false);
            StartRoulette(restController).Forget();
        }

        /// <summary>
        /// Snapshot current/max limit for every action affected by a repair.
        /// Returns a list of { name, type, limit_before, max_limit } dicts for the log.
        /// </summary>
        private List<object> SnapshotActionsBefore(BaseAction[] actions)
        {
            var list = new List<object>();
            if (actions == null) return list;
            foreach (var a in actions)
            {
                if (a == null || !a.IsLimited) continue;
                list.Add(new
                {
                    action_name = a.ActionName,
                    action_type = a.ActionType.ToString(),
                    limit_before = a.CurrentLimit,
                    max_limit = a.Limit
                });
            }
            return list;
        }

        private List<object> SnapshotActionsAfter(BaseAction[] actions)
        {
            var list = new List<object>();
            if (actions == null) return list;
            foreach (var a in actions)
            {
                if (a == null || !a.IsLimited) continue;
                list.Add(new
                {
                    action_name = a.ActionName,
                    action_type = a.ActionType.ToString(),
                    limit_after = a.CurrentLimit,
                    max_limit = a.Limit
                });
            }
            return list;
        }

        /// <summary>
        /// Snapshot all limited actions (for area-wide snapshots like rest_enter).
        /// </summary>
        private List<object> SnapshotAllActions()
        {
            var all = Resources.LoadAll<BaseAction>("Player/Actions");
            var list = new List<object>();
            foreach (var a in all)
            {
                if (a == null || !a.IsLimited) continue;
                list.Add(new
                {
                    action_name = a.ActionName,
                    action_type = a.ActionType.ToString(),
                    current_limit = a.CurrentLimit,
                    max_limit = a.Limit
                });
            }
            return list;
        }

        private async UniTask StartRoulette(RestActionController restController)
        {
            var restType = restController.RestItem.Type;
            int rouletteMin = restController.RestItem.Min;
            int rouletteMax = restController.RestItem.Max;
            int hpBefore = _playerStats.Health;

            // Snapshot action limits BEFORE repair
            var actionsBefore = (restType == RestType.Repair)
                ? SnapshotActionsBefore(restController.Action)
                : null;

            var (rouletteObject, result) = await _rouletteSystem.SetRoulette(rouletteMin, rouletteMax,
                true);
            Destroy(rouletteObject);
            switch (restType)
            {
                case RestType.Heal: _playerStats.Heal(result);
                    break;
                case RestType.Repair:
                    RepairWeapon(result, restController.Action);
                    break;
                default:
                    Debug.Log("Type Not Match");
                    break;
            }

            // Snapshot action limits AFTER repair
            var actionsAfter = (restType == RestType.Repair)
                ? SnapshotActionsAfter(restController.Action)
                : null;

            LogRestAction(restType, result, rouletteMin, rouletteMax, hpBefore, restController.Action, actionsBefore, actionsAfter);
            Leave();
        }


        private void RepairWeapon(int value, BaseAction[] actions)
        {
            foreach (var action in actions)
            {
                if(action.IsLimited) action.AddLimit(value);
            }
        }

        private void LogRestEnter()
        {
            var ps = PlayerStats.Instance;
            BattleFileLogger.WriteEvent("rest_enter", new
            {
                player_hp = ps?.Health ?? 0,
                player_max_hp = ps?.MaxHealth ?? 0,
                player_level = ps?.Level ?? 0,
                player_coin = ps?.Coin ?? 0,
                action_limits = SnapshotAllActions()
            });

            Debug.Log($"[RestSystem] Player entered rest area with {ps?.Health ?? 0}/{ps?.MaxHealth ?? 0} HP");
        }

        private void LogRestAction(RestType restType, int rouletteResult, int rouletteMin, int rouletteMax,
            int hpBefore, BaseAction[] affectedActions,
            List<object> actionsBefore, List<object> actionsAfter)
        {
            var ps = PlayerStats.Instance;

            object repairData = null;
            if (restType == RestType.Repair)
            {
                repairData = new
                {
                    repair_amount_rolled = rouletteResult,
                    actions_before = actionsBefore,
                    actions_after = actionsAfter
                };
            }

            BattleFileLogger.WriteEvent("rest_action", new
            {
                rest_type = restType.ToString(),
                roulette_result = rouletteResult,
                roulette_range_min = rouletteMin,
                roulette_range_max = rouletteMax,
                player_hp_before = hpBefore,
                player_hp_after = ps?.Health ?? 0,
                player_max_hp = ps?.MaxHealth ?? 0,
                player_level = ps?.Level ?? 0,
                player_coin = ps?.Coin ?? 0,
                repair = repairData,
                all_action_limits_after = SnapshotAllActions()
            });

            PlayfabManager.Instance?.EnqueueEvent("rest_action", new
            {
                rest_type = restType.ToString(),
                roulette_result = rouletteResult,
                roulette_range_min = rouletteMin,
                roulette_range_max = rouletteMax,
                player_hp_before = hpBefore,
                player_hp_after = ps?.Health ?? 0,
                player_max_hp = ps?.MaxHealth ?? 0,
                player_level = ps?.Level ?? 0,
                player_coin = ps?.Coin ?? 0,
                repair = repairData
            });

            Debug.Log($"[RestSystem] Logged rest action: {restType}, result: {rouletteResult} (range {rouletteMin}-{rouletteMax}), HP: {hpBefore} -> {ps?.Health ?? 0}");
        }

        private void Leave()
        {
            _buttonPanel.SetActive(false);
            _mainCanvas.SetActive(false);
            _gameManager.ChangeDungeon(true);
        }
    }
}