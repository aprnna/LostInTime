using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Manager
{
    public class EnemyTurnState: GameState
    {
        private List<GameObject> _rouletteObjects  = new List<GameObject>();
        private List<int> _results = new List<int>();
        
        public EnemyTurnState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle) : 
            base(battleSystem, uiManagerBattle)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Enemy Turn");
            _battleSystem.GameManager.ChangeInstruction("Enemy Turn");
            ExecuteEnemyAIAll().Forget();
        }
        private async UniTask ExecuteEnemyAIAll()
        {
            await UniTask.DelayFrame(2);

            var tasks = new List<UniTask<(GameObject rouletteObject, int result)>>();
            foreach (var e in _battleSystem.Enemies)
            {
                if (!e.EnemyStats.IsAlive()) continue;

                var min = e.EnemyStats.MinDamage();
                var max = e.EnemyStats.MaxDamage();
                tasks.Add(_battleSystem.RouletteSystem
                    .SetRoulette(min, max, true));
            }

            var allResults = await UniTask.WhenAll(tasks);

            foreach (var (rouletteObject, result) in allResults)
            {
                _rouletteObjects.Add(rouletteObject);
                _results.Add(result);
            }

            foreach (var roll in _results)
            {
                int reduce = roll - _battleSystem.PlayerDefend;
                _battleSystem.PlayerStats.GetHit(reduce);
                Debug.Log($"Enemy hit with {roll} damage (reduced to {reduce})");

                if (!_battleSystem.PlayerStats.IsAlive())
                {
                    _battleSystem.ChangeBattleResult(BattleResult.EnemiesWin);
                    _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
                    await UniTask.Yield();
                }
            }
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
            _results.Clear();
        }
    }
}