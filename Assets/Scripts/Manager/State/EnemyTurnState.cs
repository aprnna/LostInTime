using UnityEngine;
using System.Collections;
using Player;

namespace Manager
{
    public class EnemyTurnState: GameState
    {
        public EnemyTurnState(BattleSystem battleSystem, MonoBehaviour coroutineRunner) : base(battleSystem, coroutineRunner)
        {
        }

        public override void OnEnter()
        {
            _battleSystem.GameManager.SetInstruction("Enemy Turn");
            _battleSystem.StartCoroutine(ExecuteEnemyAI());
        }
        private IEnumerator ExecuteEnemyAI()
        {
            yield return new WaitForSeconds(1f);

            foreach (var e in _battleSystem.Enemies)
            {
                if (e.EnemyStats.IsAlive())
                {
                    int roll = Random.Range(e.EnemyStats.MinDamage(), e.EnemyStats.MaxDamage() + 1);
                    Debug.Log($"{e.name} menyerangmu {roll} dmg!");
                    _battleSystem.PlayerStats.GetHit(roll - _battleSystem.PlayerDefend);
                    if(!_battleSystem.PlayerStats.IsAlive())
                    {
                        _battleSystem.ChangeBattleResult(BattleResult.EnemiesWin);
                        _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(1f);
            _battleSystem.StateMachine.ChangeState(_battleSystem.PlayerTurnState);
        }
        public override void OnUpdate()
        {

        }
        public override void OnExit()
        {
            _battleSystem.SetPlayerDefend(0);
        }
    }
}