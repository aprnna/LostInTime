using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

            var tasks = new List<UniTask<(GameObject rouletteObject, int result)>>();
            var aliveEnemies = new List<EnemyController>();
            
            foreach (var enemy  in _battleSystem.Enemies)
            {
                if (!enemy.EnemyStats.IsAlive()) continue;
                var min = enemy.EnemyStats.MinDamage();
                var max = enemy.EnemyStats.MaxDamage();
                tasks.Add(_battleSystem.RouletteSystem
                    .SetRoulette(min, max, true));
                aliveEnemies.Add(enemy);
            }

            var allResults = await UniTask.WhenAll(tasks);

            for (int i = 0; i < allResults.Length; i++)
            {
                var (rouletteObject, damage) = allResults[i];
                _rouletteObjects.Add(rouletteObject);
                
                if (i < aliveEnemies.Count)
                {
                    _attacks.Add((aliveEnemies[i], damage));
                }
            }

            int totalDamage = 0;

            foreach (var (enemy, damage) in _attacks)
            {
                if (enemy == null || !enemy.EnemyStats.IsAlive())
                    continue;
                totalDamage =+ damage;
                _battleSystem.PlayerStats.GetHit(damage);
                Debug.Log($"Enemy hit with {damage})");

                if (!_battleSystem.PlayerStats.IsAlive())
                {
                    int playerHpAfter = _battleSystem.PlayerStats.Health;
                    _battleSystem.LogEnemyTurn(playerHpAfter,playerHpBefore,totalDamage);
                    _battleSystem.LogPlayerDeath();
                    _battleSystem.ChangeBattleResult(BattleResult.EnemiesWin);
                    _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
                    return;
                }
                await UniTask.Delay(500);
            }
            int playerHpAfter2 = _battleSystem.PlayerStats.Health;
            _battleSystem.LogEnemyTurn(playerHpAfter2,playerHpBefore, totalDamage);
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
            _battleSystem.ResetBattle();
            ClearRoulette();
            _attacks.Clear();
        }
    }
}