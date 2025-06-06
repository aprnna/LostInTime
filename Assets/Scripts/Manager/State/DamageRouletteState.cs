using UnityEngine;
using System.Collections;
namespace Manager
{
    public class DamageRouletteState: GameState
    {
        private bool _isRouletteStarted = false;
        public DamageRouletteState(GameManager gameManager, MonoBehaviour coroutineRunner): base(gameManager, coroutineRunner)
        {
        }
        public override void OnEnter()
        {
            _gameManager.SetRouletteButton(true, OnStartButtonPressed);
        }
        private void OnStartButtonPressed()
        {
            if (_isRouletteStarted) return;

            _isRouletteStarted = true;

            Debug.Log("Roulette Started!");

            _monoBehaviour.StartCoroutine(RouletteRoutine());
        }

        private IEnumerator RouletteRoutine()
        {
            yield return new WaitForSeconds(2f);
            var min = _gameManager.SelectedAction.MinDamage;
            var max =_gameManager.SelectedAction.MaxDamage;
            var roulette = Random.Range(min, max);
            Debug.Log("Damage dealt: " + roulette);

            _gameManager.SelectedTarget.GetHit(roulette);
            Debug.Log(_gameManager.SelectedTarget.Health);
            _gameManager.SetEnemyStats(_gameManager.SelectedTarget, true);
            yield return new WaitForSeconds(2f);
            _isRouletteStarted = false;
            if(EnemiesAvailable())_gameManager.StateMachine.ChangeState(_gameManager.EnemyTurnState);
            else
            {
                _gameManager.ChangeBattleResult(BattleResult.PlayerWin);
                _gameManager.StateMachine.ChangeState(_gameManager.ResultBattleState);
            }
            
        }

        private bool EnemiesAvailable()
        {
            foreach (var e in _gameManager.Enemies)
            {
                if (e.IsAlive())
                {
                    return true;
                }
            }
            return false;
        }
        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _gameManager.SetRouletteButton(false, null);
            _gameManager.SetEnemyStats(_gameManager.SelectedTarget, false);
            _gameManager.ClearTarget();
            _gameManager.ClearAction();

        }
    }
}