using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DDA;
using Player;

namespace Manager
{
    public class EnemyTurnState: GameState
    {
        private List<GameObject> _rouletteObjects  = new List<GameObject>();
        private List<(EnemyController enemy, int damage)> _attacks = new List<(EnemyController, int)>();
        private int playerHpBefore;
        public EnemyTurnState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle) : 
            base(battleSystem, uiManagerBattle)
        {
        }

        private EnemyController _attackingEnemy;

        public override void OnEnter()
        {
            Debug.Log("Enemy Turn");
            playerHpBefore = _battleSystem.PlayerStats.Health;
            _battleSystem.GameManager.ChangeInstruction("Enemy Turn");
            ExecuteEnemyAIAll().Forget();
        }
        private async UniTask ExecuteEnemyAIAll()
        {
            await UniTask.DelayFrame(2);

            // Collect all alive enemies
            var aliveEnemies = new List<EnemyController>();
            foreach (var enemy in _battleSystem.Enemies)
            {
                if (enemy.EnemyStats.IsAlive())
                {
                    aliveEnemies.Add(enemy);
                }
            }

            if (aliveEnemies.Count == 0)
            {
                _battleSystem.StateMachine.ChangeState(_battleSystem.PlayerTurnState);
                return;
            }

            // Select random enemy to attack
            var randomIndex = UnityEngine.Random.Range(0, aliveEnemies.Count);
            _attackingEnemy = aliveEnemies[randomIndex];

            // Show attacking enemy UI
            _attackingEnemy.OnChangeMarker(true);
            _battleSystem.UIManagerBattle.SetEnemyPanel(_attackingEnemy.EnemyStats, true);

            var min = _attackingEnemy.EnemyStats.MinDamage();
            var max = _attackingEnemy.EnemyStats.MaxDamage();

            var (rouletteObject, damage) = await _battleSystem.RouletteSystem.SetRoulette(min, max, true);
            _rouletteObjects.Add(rouletteObject);
            _attacks.Add((_attackingEnemy, damage));

            Debug.Log($"Enemy {_attackingEnemy.name} attacks with {damage} damage");

            _battleSystem.PlayerStats.GetHit(damage);

            // Feed DDA: player damage already tracked by DamageRouletteState via OnPlayerAttack().
            // Here we only track enemy damage and increment turn count.
            Debug.Log($"[EnemyTurnState] Calling DDA OnTurnEnd: enemyDmg={damage}");
            DDAIntegration.Instance?.OnTurnEnd(0, damage);
            _battleSystem.LastPlayerDamageDealt = 0;

            if (!_battleSystem.PlayerStats.IsAlive())
            {
                int playerHpAfter = _battleSystem.PlayerStats.Health;
                _battleSystem.LogEnemyTurn(playerHpAfter, playerHpBefore, damage);
                _battleSystem.LogPlayerDeath();
                _battleSystem.ChangeBattleResult(BattleResult.EnemiesWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
                return;
            }

            int playerHpAfter2 = _battleSystem.PlayerStats.Health;
            _battleSystem.LogEnemyTurn(playerHpAfter2, playerHpBefore, damage);
            await UniTask.Delay(TimeSpan.FromSeconds(2));
            _battleSystem.StateMachine.ChangeState(_battleSystem.PlayerTurnState);
        }

        private void ClearRoulette()
        {
            foreach (var rouletteObject in _rouletteObjects)
            {
                _battleSystem.DestroyObject(rouletteObject);
            }
        }
        
        public override void OnUpdate()
        {

        }
        
        public override void OnExit()
        {
            // Hide attacking enemy UI
            if (_attackingEnemy != null)
            {
                _attackingEnemy.OnChangeMarker(false);
            }
            _battleSystem.UIManagerBattle.SetEnemyPanel(null, false);
            _battleSystem.ResetBattle();
            ClearRoulette();
            _attacks.Clear();
        }
    }
}