using System;
using System.Collections.Generic;
using Audio;
using Cysharp.Threading.Tasks;
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
                Enemies.Add(enemy.GetComponent<EnemyController>());
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
        
        #region Battle Lifecycle
        private async UniTask StartBattleLogging()
        {
            Debug.Log("[BattleSystem] SessionID = " + GameManager.SessionID);
            if (BattleLogger == null) return ;
            
            // Gather enemy HPs
            List<int> enemyHPs = new List<int>();
            foreach (var enemy in Enemies)
            {
                enemyHPs.Add(enemy.EnemyStats.Health);
            }
            
            // Start logging
            BattleLogger.StartBattle(
                GameManager.SessionID,
                MapSystem.CurrentPlayerMapNode.mapNodeId,
                PlayerStats.Health,
                enemyHPs
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
            
            // End logging
            if (BattleLogger != null)
            {
                BattleLogger.EndBattle(PlayerStats.Health, totalEnemyHP, playerWon);
            }
            
            SendBattleLog();
        }

        private void SendBattleLog()
        {
            if (BattleLogger == null || PlayfabManager == null) return;
            
            bool playerWon = BattleResult == BattleResult.PlayerWin;
            BattleLog log = BattleLogger.GenerateBattleLog(playerWon);
            
            PlayfabManager.SendBattleLog(log);
            
            Debug.Log($"[BattleSystem] Battle log sent - Duration: {log.player_performance.battle_duration:F2}s, Turns: {log.player_performance.turn_count}");
        }

        #endregion

        #region Action Logging Hooks
        public void LogPlayerAttack(EnemyController target, int damage, bool isCritical = false)
        {
            BattleLogger?.OnPlayerAttack(target.name, damage, isCritical);
        }
   
        public void LogPlayerAction(int damage)
        {
            BattleLogger?.OnPlayerAction(SelectedAction.ActionName,SelectedTarget.name, damage);
        }
        public void LogPlayerFist(EnemyController target, int damage)
        {
            BattleLogger?.OnPlayerFistAction(target.name, damage);
        }
        public void LogPlayerGun(EnemyController target, int damage)
        {
            BattleLogger?.OnPlayerGunAction(target.name, damage);
        }
        public void LogPlayerSword(EnemyController target, int damage)
        {
            BattleLogger?.OnPlayerSwordAction(target.name, damage);
        }
        public void LogPlayerDefend(string action,int defenseValue)
        {
            BattleLogger?.OnPlayerDefendAction(action,defenseValue);
        }
        public void LogEnemyAttack(EnemyController enemy, int damage)
        {
            BattleLogger?.OnEnemyAttack(enemy.name, damage);
        }
        public void LogEnemyDefeated(EnemyController enemy)
        {
            BattleLogger?.OnEnemyDefeated(enemy.name);
        }
        public void LogPlayerDeath()
        {
            BattleLogger?.OnPlayerDeath();
        }

        public void LogPlayerTurnStart()
        {
            BattleLogger?.OnPlayerTurnStart();
        }
        public void LogEnemyTurnStart(string enemyName)
        {
            BattleLogger?.OnEnemyTurnStart(enemyName);
        }
        public void LogMinigameResult(string minigameName, bool success, float difficulty)
        {
            BattleLogger?.OnMinigameCompleted(minigameName, success, difficulty);
        }
        public void LogRouletteResult(int damageMultiplier)
        {
            BattleLogger?.OnRouletteResult(damageMultiplier);
        }

        #endregion
    }

    public enum BattleResult
    {
        PlayerWin,
        EnemiesWin
    }

}