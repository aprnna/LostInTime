using System;
using System.Collections.Generic;
using Audio;
using Cysharp.Threading.Tasks;
using Player;
using Player.Item;
using Roulette;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class BattleSystem: MonoBehaviour
    {
        public static BattleSystem Instance;
        [field:SerializeField] public UIManagerBattle UIManagerBattle { get; private set; }
      
        [Header("Game State")]
        public EnemyController SelectedTarget { get; private set; }
        public BaseAction SelectedAction { get; private set; }
        public PlayerTurnState PlayerTurnState { get; private set; }
        public SelectActionState SelectActionState{ get; private set; }
        public SelectEnemyState SelectEnemyState { get; private set; }
        public EnemyTurnState EnemyTurnState { get; private set; }
        public DamageRouletteState DamageRouletteState { get; private set; }
        public ResultBattleState ResultBattleState { get; private set; }
        public FiniteStateMachine<GameState> StateMachine { get; private set; }
        
        [Header("Other")]
        public PlayerStats PlayerStats { get; private set; }
        public BattleResult BattleResult { get; private set; }
        public int PlayerDefend { get; private set; }
        public List<EnemyController> Enemies { get; private set; } = new List<EnemyController>();
        public MapSystem MapSystem{ get; private set; }
        public GameManager GameManager { get; private set; }
        public RouletteSystem RouletteSystem { get; private set; }
        
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
            GameManager = GameManager.Instance;
            MapSystem = MapSystem.Instance;
            RouletteSystem = RouletteSystem.Instance;
            PlayerStats = PlayerStats.Instance;
            
            PlayerTurnState      = new PlayerTurnState(this,UIManagerBattle );
            SelectActionState    = new SelectActionState(this, UIManagerBattle);
            SelectEnemyState     = new SelectEnemyState(this, UIManagerBattle);
            DamageRouletteState  = new DamageRouletteState(this, UIManagerBattle);
            EnemyTurnState       = new EnemyTurnState(this, UIManagerBattle);
            ResultBattleState    = new ResultBattleState(this, UIManagerBattle);
            StateMachine         = new FiniteStateMachine<GameState>(PlayerTurnState);
            
            StateMachine.Init();
            SpawnEnemies();
        }
        void Update()
        {
            StateMachine.OnUpdate();
        }

        public void SpawnEnemies()
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
            PlayerDefend = value;
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
            PlayerDefend = 0;
        }

        public void Leave()
        {
            UIManagerBattle.SetMainCanvas(false);
            GameManager.ChangeDungeon(true);
        }
        // private void OnDestroy()
        // {
        //     GameManager.PlayerLevelUp -= ResultBattleState.Continue;
        // }

        public void DestroyObject(GameObject gameObject)
        {
            Destroy(gameObject);
        }
    }

    public enum BattleResult
    {
        PlayerWin,
        EnemiesWin
    }

}