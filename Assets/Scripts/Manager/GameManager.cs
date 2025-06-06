using Player;
using Player.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Manager
{
    public class GameManager: PersistentSingleton<GameManager>
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private EnemyStats[] _enemies;
        public PlayerStats PlayerStats { get; private set; }
        public EnemyStats SelectedTarget { get; private set; }
        public BaseAction SelectedAction { get; private set; }
        public EnemyStats[] Enemies => _enemies;
        public PlayerTurnState PlayerTurnState { get; private set; }
        public SelectActionState SelectActionState{ get; private set; }
        public SelectEnemyState SelectEnemyState { get; private set; }
        public EnemyTurnState EnemyTurnState { get; private set; }
        public DamageRouletteState DamageRouletteState { get; private set; }
        public ResultBattleState ResultBattleState { get; private set; }
        public SelectMapState SelectMapState { get; private set; }
        public FiniteStateMachine<GameState> StateMachine { get; private set; }
        public BattleResult BattleResult { get; private set; }

        public void Start()
        {
            PlayerStats = PlayerStats.Instance;
            PlayerStats.InitializeStats("Kamikaze",100,100, 3, 3, 20,0,20,0);

            PlayerTurnState      = new PlayerTurnState(this, this);
            SelectActionState    = new SelectActionState(this, this);
            SelectEnemyState     = new SelectEnemyState(this, this);
            DamageRouletteState  = new DamageRouletteState(this, this);
            EnemyTurnState       = new EnemyTurnState(this, this);
            SelectMapState       = new SelectMapState(this, this);
            ResultBattleState    = new ResultBattleState(this, this);

            StateMachine = new FiniteStateMachine<GameState>(PlayerTurnState);
            StateMachine.Init();
        }
        void Update()
        {
            StateMachine.OnUpdate();
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
            _uiManager.SetMap(value);
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