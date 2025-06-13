using System.Collections;
using Manager;
using UnityEngine;

namespace Player
{
    public class EnemyController:MonoBehaviour
    {
        [SerializeField] private GameObject _marker;
        private BattleSystem _battleSystem;
        public EnemyStats EnemyStats { get; private set; }
        public Animator AnimEnemy { get; private set; }

        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            EnemyStats = GetComponent<EnemyStats>();
            AnimEnemy = GetComponent<Animator>();
            _marker.SetActive(false);
        }

        public void PlayAnim(string stateName)
        {
            AnimEnemy.SetTrigger(stateName);
        }
        public void OnChangeMarker(bool value)
        {
            _marker.SetActive(value);
        }
        void OnMouseEnter()
        {
            if(_battleSystem.StateMachine.CurrentState == _battleSystem.EnemyTurnState) return;
            OnChangeMarker(true);
            OnHoverEnemy(EnemyStats, true);
        }

        private void OnMouseExit()
        {
            if (_battleSystem.SelectedTarget != null) return;
            OnChangeMarker(false);
            OnHoverEnemy(EnemyStats, false);
        }

        void OnMouseDown()
        {
            if(_battleSystem.StateMachine.CurrentState != _battleSystem.SelectEnemyState) return;
            OnEnemyButtonClicked(this);
        }
        
        public void OnEnemyButtonClicked(EnemyController enemyUnit)
        {
            if (_battleSystem.StateMachine.CurrentState == _battleSystem.SelectEnemyState)
            {
                _battleSystem.SelectEnemy(enemyUnit);
                _battleSystem.UIManagerBattle.SetEnemyPanel(enemyUnit.EnemyStats, true);
                _battleSystem.StateMachine.ChangeState(_battleSystem.DamageRouletteState);
            }
        }

        public void OnHoverEnemy(EnemyStats enemyUnit, bool active)
        {
            _battleSystem.UIManagerBattle.SetEnemyPanel(enemyUnit, active);
        }

    }
}