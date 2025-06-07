using System;
using Manager;
using UnityEngine;

namespace Player
{
    public class EnemyController:MonoBehaviour
    {
        private BattleSystem _battleSystem;
        private EnemyStats _enemyStats;
        private void Start()
        {
            _battleSystem = BattleSystem.Instance;
            _enemyStats = GetComponent<EnemyStats>();
        }

        void OnMouseEnter()
        {
            _battleSystem.OnHoverEnemy(_enemyStats, true);
        }

        private void OnMouseExit()
        {
            if(_battleSystem.SelectedTarget != null) return;
            _battleSystem.OnHoverEnemy(_enemyStats, false);
        }

        void OnMouseDown()
        {
            _battleSystem.OnEnemyButtonClicked(_enemyStats);
        }
    }
}