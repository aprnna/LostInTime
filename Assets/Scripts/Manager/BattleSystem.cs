using System;
using System.Collections.Generic;
using Player;
using Player.Item;
using UnityEngine;

namespace Manager
{
    public class BattleSystem: MonoBehaviour
    {
        public static BattleSystem Instance;
        [SerializeField] private UIManagerBattle _uiManager;
        [SerializeField] private List<EnemyController> _enemies = new List<EnemyController>();
        public PlayerStats PlayerStats { get; private set; }
        public EnemyController SelectedTarget { get; private set; }
        public BaseAction SelectedAction { get; private set; }
        public List<EnemyController> Enemies => _enemies;
        public PlayerTurnState PlayerTurnState { get; private set; }
        public SelectActionState SelectActionState{ get; private set; }
        public SelectEnemyState SelectEnemyState { get; private set; }
        public EnemyTurnState EnemyTurnState { get; private set; }
        public DamageRouletteState DamageRouletteState { get; private set; }
        public ResultBattleState ResultBattleState { get; private set; }
        public FiniteStateMachine<GameState> StateMachine { get; private set; }
        public BattleResult BattleResult { get; private set; }
        public GameManager GameManager { get; private set; }
        public int PlayerDefend { get; private set; }
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
            GameManager = GameManager.Instance;
        

            PlayerTurnState      = new PlayerTurnState(this, this);
            SelectActionState    = new SelectActionState(this, this);
            SelectEnemyState     = new SelectEnemyState(this, this);
            DamageRouletteState  = new DamageRouletteState(this, this);
            EnemyTurnState       = new EnemyTurnState(this, this);
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
            var enemies = GameManager.GetEnemies();
            var activeBiome = GameManager.ActiveBiome;
            var enemiesPos = GameManager.GetEnemiesPos(activeBiome);
            _enemies.Clear();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy=Instantiate(enemies[i].Prefab, enemiesPos[i]);
                _enemies.Add(enemy.GetComponent<EnemyController>());
            }
        }

        public void DropItems()
        {
            _uiManager.SetDropItemPanel(true);
            var dropItems = GameManager.GetDropItems();
            foreach (var item in dropItems)
            {
                _uiManager.InstantiateDropItem(item.Icon, item.Amount);
                Debug.Log($"Get {item.Type} {item.Amount}");
            }
        }

        public void AppliedDropItem()
        {
            var dropItems = GameManager.GetDropItems();
            foreach (var item in dropItems)
            {
                if (item.Type != ConsumableType.SparePart)
                {
                    item.AppliedToPlayerStats(PlayerStats);
                };
            }
            Debug.Log(PlayerStats.IsLevelUp);
            if(!PlayerStats.IsLevelUp) ResultBattleState.Continue();
            PlayerStats.ResetLevelUpStatus();
        }
        public void OnContinueClicked()
        {
            GameManager.PlayerLevelUp += ResultBattleState.Continue;
            AppliedDropItem();
            ClearDropItem();
        }
        public GameObject InstantiateVFX(GameObject vfx)
        {
            return Instantiate(vfx, SelectedTarget.transform);
        }

        public void ClearVfx(GameObject vfx)
        {
            Destroy(vfx);
        }
        public void SetPlayerDefend(int value)
        {
            PlayerDefend = value;
        }
        public void ClearDropItem()
        {
            _uiManager.SetDropItemPanel(false);
            _uiManager.ClearDropItem();
        }
        public void OnActionButtonClicked(BaseAction action)
        {
            if (StateMachine.CurrentState == SelectActionState)
            {
                if (action.IsLimited)
                {
                    if (!(action.CurrentLimit > 0) && !action.IsDefend)
                    {
                        Debug.Log("Out of limit"); 
                        return;
                    }
                    action.UseAction();
                }
                SelectedAction = action;
                if (action.IsDefend)
                {
                    PlayerStats.UseShield();
                    StateMachine.ChangeState(DamageRouletteState);
                }
                else StateMachine.ChangeState(SelectEnemyState);
            }
        }

        public void OnEnemyButtonClicked(EnemyController enemyUnit)
        {
            if (StateMachine.CurrentState == SelectEnemyState)
            {
                SelectedTarget = enemyUnit;
                SetEnemyStats(enemyUnit.EnemyStats);
                SetEnemyPanel(true);
                StateMachine.ChangeState(DamageRouletteState);
            }
          
        }

        public void ChangeStatusMap(bool value)
        {
            GameManager.ChangeStatusMap(value);
        }

        public void OnHoverEnemy(EnemyStats enemyUnit, bool active)
        {
            SetEnemyPanel(active);
            SetEnemyPortrait(enemyUnit.GetPotrait());
            SetEnemyStats(enemyUnit);
        }

        public void SetActionButton(bool value)
        {
            _uiManager.SetActionsButton(value);
        }

        public void SetRouletteButton(bool value, System.Action callback)
        {
            _uiManager.SetRouletteButton(value, callback);
        }

        public void OnChangeActionDescription(string value)
        {
            _uiManager.SetActionDescription(value);
        }

        public void ChangeBattleResult(BattleResult result)
        {
            BattleResult = result;
        }
        public void SetEnemyStats(EnemyStats enemyUnit)
        {
            _uiManager.SetEnemyStats(enemyUnit);
        }
        public void SetEnemyPortrait(Sprite image)
        {
            _uiManager.SetEnemyPortrait(image);
        }
        public void SetBattleResult(bool value)
        {
            GameManager.UIManager.SetBattleResult(value);
        }

        public void SetEnemyPanel(bool value)
        {
            _uiManager.SetEnemyPanel(value);
        }

        public void ClearTarget()
        {
            SelectedTarget = null;
        }

        public void ClearAction()
        {
            SelectedAction = null;
        }
        public void Leave()
        {
            _uiManager.SetMainCanvas(false);
            GameManager.UIManager.SetMap(true);
            GameManager.ChangeStatusMap(true);
        }

        private void OnDestroy()
        {
            GameManager.PlayerLevelUp -= ResultBattleState.Continue;
        }
    }

    public enum BattleResult
    {
        PlayerWin,
        EnemiesWin
    }

    public enum Biome
    {
        Forest,
        Cave
    }

    [Serializable]
    public class EnemyBiomePos
    {
        [SerializeField] private Transform[] _pos;
        [SerializeField] private Biome _type;
        public Transform[] EnemiesPos => _pos;
        public Biome Type => _type;
    }
}