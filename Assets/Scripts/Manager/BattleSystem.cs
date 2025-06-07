using System;
using System.Collections.Generic;
using Player;
using Player.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class BattleSystem: MonoBehaviour
    {
        public static BattleSystem Instance;
        [SerializeField] private UIManagerBattle _uiManager;
        [SerializeField] private List<EnemyStats> _enemies = new List<EnemyStats>();
        [SerializeField] private Transform[] _enemiesPos;
        public PlayerStats PlayerStats { get; private set; }
        public EnemyStats SelectedTarget { get; private set; }
        public BaseAction SelectedAction { get; private set; }
        public List<EnemyStats> Enemies => _enemies;
        public PlayerTurnState PlayerTurnState { get; private set; }
        public SelectActionState SelectActionState{ get; private set; }
        public SelectEnemyState SelectEnemyState { get; private set; }
        public EnemyTurnState EnemyTurnState { get; private set; }
        public DamageRouletteState DamageRouletteState { get; private set; }
        public ResultBattleState ResultBattleState { get; private set; }
        public SelectMapState SelectMapState { get; private set; }
        public FiniteStateMachine<GameState> StateMachine { get; private set; }
        public BattleResult BattleResult { get; private set; }
        private UIManager _uiManagerGeneral;
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
            PlayerStats = PlayerStats.Instance;
            _gameManager = GameManager.Instance;
            _uiManagerGeneral = _gameManager.UIManager;
            PlayerStats.InitializeStats("Kamikaze",100,100, 3, 3, 20,0,20,0);

            PlayerTurnState      = new PlayerTurnState(this, this);
            SelectActionState    = new SelectActionState(this, this);
            SelectEnemyState     = new SelectEnemyState(this, this);
            DamageRouletteState  = new DamageRouletteState(this, this);
            EnemyTurnState       = new EnemyTurnState(this, this);
            SelectMapState       = new SelectMapState(this, this);
            ResultBattleState    = new ResultBattleState(this, this);

            StateMachine = new FiniteStateMachine<GameState>(PlayerTurnState);
            SpawnEnemies();
            StateMachine.Init();
        }
        void Update()
        {
            StateMachine.OnUpdate();
        }

        public void SpawnEnemies()
        {
            var enemies = _gameManager.GetEnemies();
            _enemies.Clear();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy=Instantiate(enemies[i].Prefab, _enemiesPos[i]);
                _enemies.Add(enemy.GetComponent<EnemyStats>());
            }
        }

        public void DropItems()
        {
            var dropItems = _gameManager.GetDropItems();
            foreach (var item in dropItems)
            {
                _uiManager.InstantiateDropItem(item.Icon, item.Amount);
                item.AppliedToPlayerStats(PlayerStats);
            }
        }
        public void OnActionButtonClicked(BaseAction action)
        {
            if (StateMachine.CurrentState == SelectActionState)
            {
                SelectedAction = action;
                SelectedAction.InitializeDamage(PlayerStats.BaseDamage);
                if(action.IsDefend) StateMachine.ChangeState(EnemyTurnState);
                else StateMachine.ChangeState(SelectEnemyState);
            }
        }

        public void OnEnemyButtonClicked(EnemyStats enemyUnit)
        {
            if (StateMachine.CurrentState == SelectEnemyState)
            {
                SelectedTarget = enemyUnit;
                SetEnemyStats(enemyUnit, true);
                StateMachine.ChangeState(DamageRouletteState);
            }
          
        }

        public void OnHoverEnemy(EnemyStats enemyUnit, bool active)
        {
            SetEnemyStats(enemyUnit, active);
        }

        public void SetActionButton(bool value)
        {
            _uiManager.SetActionsButton(value);
        }

        public void SetRouletteButton(bool value, System.Action callback)
        {
            _uiManager.SetRouletteButton(value, callback);
        }

        public void ChangeBattleResult(BattleResult result)
        {
            BattleResult = result;
        }
        public void SetEnemyStats(EnemyStats enemyUnit, bool active)
        {
            _uiManager.SetEnemyStats(enemyUnit, active);
        }

        public void SetMap(bool value)
        {
            _uiManagerGeneral.SetMap(value);
        }

        public void SetBattleResult(bool value)
        {
            _uiManager.SetBattleResult(value);
        }

        public void ClearTarget()
        {
            SelectedTarget = null;
        }

        public void ClearAction()
        {
            SelectedAction = null;
        }
    }

    public enum BattleResult
    {
        PlayerWin,
        EnemiesWin
    }
}