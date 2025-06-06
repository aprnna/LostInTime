using UnityEngine;
using System.Collections;
using Player;

namespace Manager
{
    public class EnemyTurnState: GameState
    {
        public EnemyTurnState(GameManager gameManager, MonoBehaviour coroutineRunner) : base(gameManager, coroutineRunner)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Enemy Turn");
            _gameManager.StartCoroutine(ExecuteEnemyAI());
        }
        private IEnumerator ExecuteEnemyAI()
        {
            yield return new WaitForSeconds(1f);

            foreach (var e in _gameManager.Enemies)
            {
                if (e.IsAlive())
                {
                    int roll = Random.Range(e.MinDamage(), e.MaxDamage() + 1);
                    Debug.Log($"{e.name} menyerangmu {roll} dmg!");
                    _gameManager.PlayerStats.GetHit(roll);
                    if(!_gameManager.PlayerStats.IsAlive())
                    {
                        _gameManager.ChangeBattleResult(BattleResult.EnemiesWin);
                        _gameManager.StateMachine.ChangeState(_gameManager.ResultBattleState);
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(1f);
            _gameManager.StateMachine.ChangeState(_gameManager.PlayerTurnState);
        }
        public override void OnUpdate()
        {

        }
        public override void OnExit()
        {

        }
    }
}