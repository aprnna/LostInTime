using System;
using Cysharp.Threading.Tasks;
using Minigames;
using UnityEngine;

namespace Manager
{
    public class CriticalAttackState: GameState
    {
        private int hpTargetBefore;
        private int hpTargetAfter;
        
        public CriticalAttackState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle): 
            base(battleSystem,uiManagerBattle)
        {
        }
        public override void OnEnter()
        {
            hpTargetBefore = _battleSystem.SelectedTarget.EnemyStats.Health;
            _battleSystem.GameManager.ChangeInstruction(" ");
            OnStartRoulette();

        }
        public void OnStartRoulette()
        {
            StartTapZone().Forget();
        }
        private async UniTask StartTapZone()
        {
            if (_battleSystem.SelectedAction.IsDefend)
                 DefendAction();
            else
                await AttackAction();
            if (EnemiesAvailable())
            {
                _battleSystem.StateMachine.ChangeState(_battleSystem.EnemyTurnState);
            }
            else
            {
                _battleSystem.ChangeBattleResult(BattleResult.PlayerWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            }
        }

        private void  DefendAction()
        {
            _battleSystem.LogPlayerTurn(0,0,0, false);
            _battleSystem.SetPlayerDefend(_battleSystem.SelectedAction.BaseDefend);
        }

        private async UniTask AttackAction()
        {
            _battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.DifficultyCritical);
            var result = await _battleSystem.MinigameManager.PlayTapZone(); 
            var isCriticalHit = result == Minigame.Result.Success;
            var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalHitDamage : _battleSystem.SelectedAction.BaseDamage;
            await _battleSystem.EnemyGetHit(damage, isCriticalHit);
            hpTargetAfter = _battleSystem.SelectedTarget.EnemyStats.Health;
            Debug.Log("Damage dealt: " + damage);
            await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: false);
            _battleSystem.LogPlayerTurn(hpTargetBefore, hpTargetAfter, damage, isCriticalHit);
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
                _uiManagerBattle.SetActionPanel(false);
            }else
            {
                _battleSystem.SelectedTarget.OnChangeMarker(false);
                _uiManagerBattle.SetEnemyPanel(_battleSystem.SelectedTarget.EnemyStats,false);
                _battleSystem.ResetBattle();
            }
        }
    }
}