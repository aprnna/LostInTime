using System;
using Manager;
using UnityEngine;

namespace Player
{
    public class EnemyController:MonoBehaviour
    {
        private GameManager _gameManager;
        private EnemyStats _enemyStats;
        private void Start()
        {
            _gameManager = GameManager.Instance;
            _enemyStats = GetComponent<EnemyStats>();
        }

        void OnMouseEnter()
        {
            _gameManager.OnHoverEnemy(_enemyStats, true);
        }

        private void OnMouseExit()
        {
            if(_gameManager.SelectedTarget != null) return;
            _gameManager.OnHoverEnemy(_enemyStats, false);
        }

        void OnMouseDown()
        {
            _gameManager.OnEnemyButtonClicked(_enemyStats);
        }
    }
}