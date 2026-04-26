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
        public MapSystem MapSystem{ get; private set; }
        public GameManager GameManager { get; private set; }
        public RouletteSystem RouletteSystem { get; private set; }
        public MinigameManager MinigameManager { get; private set; }
        public PlayfabManager PlayfabManager { get; private set; }
        public BattleLogger BattleLogger { get; private set; }

        [Header("DDA")]
        [SerializeField] private DDAIntegration _ddaIntegration;
        
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

            // Notify DDA before spawning enemies so difficulty is set
            if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
            {
                _ddaIntegration.OnBattlePreStart(PlayerStats.Health);
            }

            await SpawnEnemies();
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
                if (_ddaIntegration != null && _ddaIntegration.IsEnabled)
                {
                    _ddaIntegration.ApplyDifficultyToEnemy(enemyController.EnemyStats);
                }
            }
            await UniTask.Yield();
        }

        public void DropItems()
        {
            UIManagerBattle.SetDropItemPanel(true);
            var dropItems = MapSystem.GetDropItems();
            foreach (var item in dropItems)
            {
                UIManagerBattle.InstantiateDropItem(item.Icon, item.Amount);
                Debug.Log($"Get {item.Type} {item.Amount}");
                if (item.Type == ConsumableType.SparePart)
                {
                    GameManager.IncreaseProgress(item.Amount);
                }
            }
            
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
            var dropItems = MapSystem.GetDropItems();
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

            // Calculate total enemy HP
            int totalEnemyHP = 0;
            foreach (var enemy in Enemies)
            {
                totalEnemyHP += enemy.EnemyStats.Health;
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