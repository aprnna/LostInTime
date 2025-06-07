using UnityEngine;
using System.Collections;
namespace Manager
{
    public class DamageRouletteState: GameState
    {
        private bool _isRouletteStarted = false;
        public DamageRouletteState(BattleSystem battleSystem, MonoBehaviour coroutineRunner): base(battleSystem, coroutineRunner)
        {
        }
        public override void OnEnter()
        {
            _battleSystem.SetRouletteButton(true, OnStartButtonPressed);
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
            if (_battleSystem.SelectedAction.IsDefend)
            {
                var min = _battleSystem.SelectedAction.MinDefend;
                var max =_battleSystem.SelectedAction.MaxDefend;
                var roulette = Random.Range(min, max);
                Debug.Log("Defend: " + roulette);
                _battleSystem.SetPlayerDefend(roulette);
            }
            else
            {
                var min = _battleSystem.SelectedAction.MinDamage;
                var max =_battleSystem.SelectedAction.MaxDamage;
                var roulette = Random.Range(min, max);
                Debug.Log("Damage dealt: " + roulette);
                _battleSystem.SelectedTarget.EnemyStats.GetHit(roulette);
                _battleSystem.SetEnemyStats(_battleSystem.SelectedTarget.EnemyStats, true);
            }

            yield return new WaitForSeconds(2f);
            _isRouletteStarted = false;
            if(EnemiesAvailable())_battleSystem.StateMachine.ChangeState(_battleSystem.EnemyTurnState);
            else
            {
                _battleSystem.ChangeBattleResult(BattleResult.PlayerWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            }
            
        }

        private bool EnemiesAvailable()
        {
            foreach (var e in _battleSystem.Enemies)
            {
                if (e.EnemyStats.IsAlive())
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
            _battleSystem.SelectedTarget.OnChangeMarker(false);
            _battleSystem.SetRouletteButton(false, null);
            _battleSystem.SetEnemyStats(_battleSystem.SelectedTarget.EnemyStats, false);
            _battleSystem.ClearTarget();
            _battleSystem.ClearAction();

        }
    }
}