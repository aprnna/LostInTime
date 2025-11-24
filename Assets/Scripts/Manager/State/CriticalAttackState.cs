using System;
using Cysharp.Threading.Tasks;
using Minigames;
using UnityEngine;

namespace Manager
{
    public class CriticalAttackState: GameState
    {
        
        public CriticalAttackState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle): 
            base(battleSystem,uiManagerBattle)
        {
        }
        public override void OnEnter()
        {
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
            // var result = await _battleSystem.MinigameManager.PlayTapZone();
            _battleSystem.LogPlayerDefend(_battleSystem.SelectedAction.ActionName,_battleSystem.SelectedAction.BaseDefend);
            _battleSystem.SetPlayerDefend(_battleSystem.SelectedAction.BaseDefend);
        }

        private async UniTask AttackAction()
        {
            _battleSystem.MinigameManager.SetDifficulty(_battleSystem.SelectedAction.DifficultyCritical);
            var result = await _battleSystem.MinigameManager.PlayTapZone(); 
            var isCriticalHit = result == Minigame.Result.Success;
            var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalHitDamage : _battleSystem.SelectedAction.BaseDamage;
            _battleSystem.LogPlayerAction(damage);
            await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: false);
            _battleSystem.ShowDamagePopup(_battleSystem.SelectedTarget.transform.position, damage, isCriticalHit);
            _battleSystem.SelectedTarget.PlayAnim("isDamaged");
            _battleSystem.SelectedTarget.EnemyStats.GetHit(damage);
            _battleSystem.UIManagerBattle.EnemyStatsUI.InitializeStats(_battleSystem.SelectedTarget.EnemyStats);
            await _battleSystem.SelectedAction.PlayVfx(_battleSystem.SelectedTarget.transform); 
            Debug.Log("Damage dealt: " + damage);
            await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: false);
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