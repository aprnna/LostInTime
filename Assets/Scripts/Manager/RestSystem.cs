using System;
using System.Collections;
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

        private async UniTask StartRoulette(RestActionController restController)
        {
            var restType = restController.RestItem.Type;
            int rouletteMin = restController.RestItem.Min;
            int rouletteMax = restController.RestItem.Max;
            int hpBefore = _playerStats.Health;

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

            LogRestAction(restType, result, rouletteMin, rouletteMax, hpBefore);
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
                player_coin = ps?.Coin ?? 0
            });

            Debug.Log($"[RestSystem] Player entered rest area with {ps?.Health ?? 0}/{ps?.MaxHealth ?? 0} HP");
        }

        private void LogRestAction(RestType restType, int rouletteResult, int rouletteMin, int rouletteMax, int hpBefore)
        {
            var ps = PlayerStats.Instance;
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
                player_coin = ps?.Coin ?? 0
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
                player_coin = ps?.Coin ?? 0
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