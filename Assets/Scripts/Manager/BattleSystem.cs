using System;
using System.Collections.Generic;
using Audio;
using Cysharp.Threading.Tasks;
using DDA;
using Minigames;
using Player;
using Player.Item;
using Playfab;
using Roulette;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class BattleSystem: MonoBehaviour
    {
        public static BattleSystem Instance;
        [field:SerializeField] public UIManagerBattle UIManagerBattle { get; private set; }
        [SerializeField] private GameObject _popupPrefab;
        [Header("Game State")]
        public EnemyController SelectedTarget { get; private set; }
        public BaseAction SelectedAction { get; private set; }
        public PlayerTurnState PlayerTurnState { get; private set; }
        public SelectActionState SelectActionState{ get; private set; }
        public SelectEnemyState SelectEnemyState { get; private set; }
        public EnemyTurnState EnemyTurnState { get; private set; }
        public DamageRouletteState DamageRouletteState { get; private set; }
        public CriticalAttackState CriticalAttackState { get; private set; }
        public ResultBattleState ResultBattleState { get; private set; }
        public FiniteStateMachine<GameState> StateMachine { get; private set; }
        
        [Header("Other")]
        public PlayerStats PlayerStats { get; private set; }
        public BattleResult BattleResult { get; private set; }
        public List<EnemyController> Enemies { get; private set; } = new List<EnemyController>();
        // Cached post-battle reward drops (node consumables + enemy-derived Exp/Coin) built in DropItems().
        private List<DropItem> _rewardCache;
        public MapSystem MapSystem{ get; private set; }
        public GameManager GameManager { get; private set; }
        public RouletteSystem RouletteSystem { get; private set; }
        public MinigameManager MinigameManager { get; private set; }
        public PlayfabManager PlayfabManager { get; private set; }
        public BattleLogger BattleLogger { get; private set; }

        [Header("DDA")]
        [SerializeField] private DDAIntegration _ddaIntegration;

        // Per-cycle buffer: player damage dealt during DamageRouletteState, consumed by EnemyTurnState
        // when calling DDAIntegration.OnTurnEnd(playerDmg, enemyDmg) once per full cycle (matches
        // training-sim granularity where OnTurnEnd fires once per player+enemy turn pair).
        public int LastPlayerDamageDealt { get; set; } = 0;

        [Header("Reward Icons")]
        [Tooltip("Sprite shown for Exp reward in the post-battle drop panel.")]
        [SerializeField] private Sprite _expRewardIcon;
        [Tooltip("Sprite shown for Coin reward in the post-battle drop panel.")]
        [SerializeField] private Sprite _coinRewardIcon;
        
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
            PrepareBattle().Forget();
        }
        public async UniTask PrepareBattle()
        {
            await Initialize();
            await InitializeFSM();

            // Resolve DDA integration (fallback to singleton if SerializeField not assigned)
            if (_ddaIntegration == null) _ddaIntegration = DDAIntegration.Instance;

            await SpawnEnemies();

            // Notify DDA after spawning so Total Enemy HP reflects scaled values
            if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
            {
                _ddaIntegration.OnBattlePreStart(PlayerStats.Health);
            }

            await StartBattleLogging();
        }
        void Update()
        {
            StateMachine.OnUpdate();
        }

        public async UniTask Initialize()
        {
            GameManager = GameManager.Instance;
            MapSystem = MapSystem.Instance;
            RouletteSystem = RouletteSystem.Instance;
            MinigameManager = MinigameManager.Instance;
            PlayerStats = PlayerStats.Instance;
            BattleLogger = BattleLogger.Instance;
            PlayfabManager = PlayfabManager.Instance;
            await UniTask.Yield();
        }

        public async UniTask InitializeFSM()
        {
            PlayerTurnState      = new PlayerTurnState(this,UIManagerBattle );
            SelectActionState    = new SelectActionState(this, UIManagerBattle);
            SelectEnemyState     = new SelectEnemyState(this, UIManagerBattle);
            DamageRouletteState  = new DamageRouletteState(this, UIManagerBattle);
            CriticalAttackState  = new CriticalAttackState(this, UIManagerBattle);
            EnemyTurnState       = new EnemyTurnState(this, UIManagerBattle);
            ResultBattleState    = new ResultBattleState(this, UIManagerBattle);
            StateMachine         = new FiniteStateMachine<GameState>(PlayerTurnState);
            
            StateMachine.Init();
            await UniTask.Yield();
        }
        public async UniTask SpawnEnemies()
        {
            AudioManager.Instance.PlaySound(SoundType.SFX_SpawnEnemy);
            var enemies = MapSystem.GetEnemies();
            Transform[] enemiesPos = GameManager.GetEnemiesPosition();
            Enemies.Clear();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy= Instantiate(enemies[i].Prefab, enemiesPos[i]);
                var enemyController = enemy.GetComponent<EnemyController>();
                Enemies.Add(enemyController);

                // Apply DDA difficulty to enemy stats
                if (_ddaIntegration == null) _ddaIntegration = DDAIntegration.Instance;
                if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
                {
                    _ddaIntegration.ApplyDifficultyToEnemy(enemyController.EnemyStats);
                }
            }
            await UniTask.Yield();
        }

        public void DropItems()
        {
            _rewardCache = BuildRewardDropItems();
            UIManagerBattle.SetDropItemPanel(true);
            foreach (var item in _rewardCache)
            {
                UIManagerBattle.InstantiateDropItem(item.Icon, item.Amount);
                Debug.Log($"Get {item.Type} {item.Amount}");
                if (item.Type == ConsumableType.SparePart)
                {
                    GameManager.IncreaseProgress(item.Amount);
                }
            }

        }

        /// <summary>
        /// Build post-battle reward drops: node consumables (Health/Shield/SparePart) +
        /// enemy-derived Exp + Coin (summed from each defeated enemy's EnemySO reward).
        /// Exp/Coin from node DropItems are ignored — those come from the enemies now.
        /// </summary>
        private List<DropItem> BuildRewardDropItems()
        {
            var list = new List<DropItem>();

            // node drops minus Exp/Coin (those are enemy-sourced now)
            var nodeDrops = MapSystem.GetDropItems();
            if (nodeDrops != null)
            {
                foreach (var d in nodeDrops)
                {
                    if (d.Type != ConsumableType.Exp && d.Type != ConsumableType.Coin)
                        list.Add(d);
                }
            }

            // sum exp + coin from every enemy in this battle
            int totalExp = 0, totalCoin = 0;
            foreach (var enemy in Enemies)
            {
                if (enemy == null || enemy.EnemyStats == null) continue;
                totalExp += enemy.EnemyStats.ExpReward;
                totalCoin += enemy.EnemyStats.CoinReward;
            }
            if (totalExp > 0)  list.Add(new DropItem(ConsumableType.Exp,  _expRewardIcon,  totalExp));
            if (totalCoin > 0) list.Add(new DropItem(ConsumableType.Coin, _coinRewardIcon, totalCoin));

            return list;
        }

        public void ShowDamagePopup(Vector3 position,  float damage, bool isCritical)
        {
            var popup = DamagePopup.Create(_popupPrefab.transform, position, damage, isCritical);
        }
        public void ClearDropItem()
        {
            UIManagerBattle.SetDropItemPanel(false);
            UIManagerBattle.ClearDropItem();
            // GameManager.SetTeleportProgress(false);
        }
        public void AppliedDropItem()
        {
            var dropItems = _rewardCache ?? new List<DropItem>();
            foreach (var item in dropItems)
            {
                if (item.Type != ConsumableType.SparePart)
                    item.AppliedToPlayerStats(PlayerStats);
            }
        }
        
        public void OnContinueClicked()
        {
            // GameManager.PlayerLevelUp += ResultBattleState.Continue;
            AudioManager.Instance.PlaySound(SoundType.SFX_Reward);
            ClearDropItem();
            AppliedDropItem();
        }

        public void SetPlayerDefend(int value)
        {
            PlayerStats.SetPlayerDefend(value);
        }
   
        public void ChangeBattleResult(BattleResult result)
        {
            BattleResult = result;
        }

        public void SelectAction(BaseAction action)
        {
            SelectedAction = action;
        }

        public void SelectEnemy(EnemyController enemy)
        {
            SelectedTarget = enemy;
        }
        public void ResetBattle()
        {
            SelectedTarget = null;
            SelectedAction = null;
        }

        public void Leave()
        {
            UIManagerBattle.SetMainCanvas(false);
            GameManager.ChangeDungeon(true);
        }

        public void DestroyObject(GameObject gameObject)
        {
            Destroy(gameObject);
        }

        public async UniTask EnemyGetHit(int damage, bool isCriticalHit)
        {
            ShowDamagePopup(SelectedTarget.transform.position, damage, isCriticalHit);
            SelectedTarget.PlayAnim("isDamaged");
            SelectedTarget.EnemyStats.GetHit(damage);
            UIManagerBattle.EnemyStatsUI.InitializeStats(SelectedTarget.EnemyStats);
            await SelectedAction.PlayVfx(SelectedTarget.transform); 
        }
        #region Battle Lifecycle
        private async UniTask StartBattleLogging()
        {
            if (BattleLogger == null) return ;

            if (!BattleLogger.HasActiveSession)
            {
                Debug.Log("[BattleSystem] No battle log found, creating new session log...");
                BattleLogger.CreateNewLog(SessionManager.Instance.SessionId);
            }

            // Set battle context (area type + difficulty) before starting
            string areaType = MapSystem.GetMapType().ToString();
            string diffName = null;
            float hpMult = 1f;
            float dmgMult = 1f;
            if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
            {
                diffName = _ddaIntegration.GetCurrentDifficultyName();
                var (hp, dmg) = _ddaIntegration.GetCurrentMultipliers();
                hpMult = hp;
                dmgMult = dmg;
            }
            BattleLogger.SetBattleContext(areaType, diffName, hpMult, dmgMult);

            // Start logging
            BattleLogger.StartBattle(
                MapSystem.CurrentPlayerMapNode.mapNodeId,
                PlayerStats.Health,
                Enemies
            );
            await UniTask.Yield();
        }
        public void OnBattleEnd()
        {
            bool playerWon = BattleResult == BattleResult.PlayerWin;

            // Calculate total enemy HP (use MaxHealth, not remaining Health)
            int totalEnemyHP = 0;
            foreach (var enemy in Enemies)
            {
                totalEnemyHP += enemy.EnemyStats.MaxHealth;
            }

            // Notify DDA of battle end
            if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
            {
                _ddaIntegration.OnBattleEnd(playerWon, PlayerStats.Health);
            }

            // End logging
            if (BattleLogger != null)
            {
                BattleLogger.EndBattle(PlayerStats.Health, totalEnemyHP, playerWon);
                SessionManager.Instance.EndSessionAndSend();
            }
        }
        #endregion

        #region Action Logging Hooks
        public void LogPlayerTurn(int targetHpBefore, int targetHpAfter, int damage, bool isCritical = false)
        {
            BattleLogger.OnPlayerTurn(SelectedAction.ActionType, SelectedTarget.name, targetHpBefore, targetHpAfter, damage, isCritical);
        }

        public void LogPlayerDefend(int defendAmount)
        {
            BattleLogger.OnPlayerTurn(SelectedAction.ActionType, "Player", 0, 0, defendAmount, false);
        }

        public void LogPlayerDeath()
        {
            BattleLogger.OnPlayerDeath();
        }
        public void LogEnemyTurn(int playerHpAfter, int playerHpBefore, int totalDamage)
        {
            BattleLogger.OnEnemyTurn( playerHpAfter ,playerHpBefore,totalDamage);
        }
        #endregion
    }

    public enum BattleResult
    {
        PlayerWin,
        EnemiesWin
    }

}