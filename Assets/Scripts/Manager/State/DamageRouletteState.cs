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
            if (_battleSystem.GameManager.IsAutoStart) OnStartButtonPressed();
            _battleSystem.GameManager.SetRoulette(true ,OnStartButtonPressed);
        }
        private void OnStartButtonPressed()
        {
            _battleSystem.GameManager.ButtonStartRoulette.SetActive(false);
            _battleSystem.GameManager.ButtonStopRoulette.SetActive(true);
            if (_isRouletteStarted) return;

            _isRouletteStarted = true;

           _battleSystem.GameManager.SetInstruction("Roulette Started!");

            _monoBehaviour.StartCoroutine(RouletteRoutine());
        }

        private IEnumerator RouletteRoutine()
        {
            // yield return new WaitForSeconds(2f);
            if (_battleSystem.SelectedAction.IsDefend)
            {
                var min = _battleSystem.SelectedAction.MinDefend;
                var max =_battleSystem.SelectedAction.MaxDefend;
                Debug.Log("min : "+min+"max: "+max);
                yield return PlayRoulette(min,max); 
                // var roulette = Random.Range(min, max);
                Debug.Log("Defend: " + _battleSystem.GameManager.RouletteResult);
                _battleSystem.SetPlayerDefend(_battleSystem.GameManager.RouletteResult);
            }
            else
            {
                var min = _battleSystem.SelectedAction.MinDamage;
                var max =_battleSystem.SelectedAction.MaxDamage;
                // var roulette = Random.Range(min, max);
                yield return PlayRoulette(min,max); 
                _battleSystem.SelectedTarget.PlayAnim("isDamaged");
                yield return PlayVFX(); 
                Debug.Log("Damage dealt: " + _battleSystem.GameManager.RouletteResult);
                _battleSystem.SelectedTarget.EnemyStats.GetHit(_battleSystem.GameManager.RouletteResult);
                _battleSystem.SetEnemyStats(_battleSystem.SelectedTarget.EnemyStats);
            }
            _isRouletteStarted = false;
            if(EnemiesAvailable())_battleSystem.StateMachine.ChangeState(_battleSystem.EnemyTurnState);
            else
            {
                _battleSystem.ChangeBattleResult(BattleResult.PlayerWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            }
            
        }

        private IEnumerator PlayRoulette(int min, int max)
        {
            _battleSystem.GameManager.SetRoulette(min, max);
            yield return new WaitUntil(() => _battleSystem.GameManager.IsRouletteStop);
            yield return new WaitForSeconds(1f);

        }
        private IEnumerator PlayVFX()
        {
            var vfx = _battleSystem.SelectedAction.VFX;
            var objectVfx = _battleSystem.InstantiateVFX(vfx);
            var animator = objectVfx.GetComponent<Animator>();
            yield return null;
            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f || animator.IsInTransition(0))
            {
                yield return null;
            }
            _battleSystem.ClearVfx(objectVfx);
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
            if (_battleSystem.SelectedAction.IsDefend)
            {
                _battleSystem.ClearAction();
            }else
            {
                _battleSystem.SelectedTarget.OnChangeMarker(false);
                _battleSystem.SetEnemyPanel(false);
                _battleSystem.SetEnemyStats(_battleSystem.SelectedTarget.EnemyStats);
                _battleSystem.ClearTarget();
            }
            var roulette = _battleSystem.GameManager.RouletteObject;
            _battleSystem.GameManager.DestroyObject(roulette);
            _battleSystem.GameManager.SetRoulette(false ,null);

        }
    }
}