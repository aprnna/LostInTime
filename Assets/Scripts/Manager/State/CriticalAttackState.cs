using System;
using Cysharp.Threading.Tasks;
using DDA;
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
            if (!_battleSystem.SelectedAction.IsDefend)
            {
                hpTargetBefore = _battleSystem.SelectedTarget.EnemyStats.Health;
            }
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
                
            // Player action completed, increment turn count
            DDAIntegration.Instance?.OnTurnEnd(0, 0);
            
            if (EnemiesAvailable())
            {
                _battleSystem.StateMachine.ChangeState(_battleSystem.EnemyTurnState);
            }
            else
            {
                _battleSystem.LastPlayerDamageDealt = 0;

                _battleSystem.ChangeBattleResult(BattleResult.PlayerWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            }
        }

        private void  DefendAction()
        {
            _battleSystem.LogPlayerDefend(_battleSystem.SelectedAction.BaseDefend);
            _battleSystem.SetPlayerDefend(_battleSystem.SelectedAction.BaseDefend);
        }

        private async UniTask AttackAction()
        {
            _battleSystem.MinigameManager.SetSpeedPercent(_battleSystem.SelectedAction.SpeedPercent);
            _battleSystem.MinigameManager.SetZoneWidth(_battleSystem.SelectedAction.ZoneWidthPercent);
            var result = await _battleSystem.MinigameManager.PlayTapZone();
            var isCriticalHit = result == Minigame.Result.Success;

            // Track QTE accuracy for DDA
            DDAIntegration.Instance?.OnQTECompleted(isCriticalHit);

            var damage = isCriticalHit ? _battleSystem.SelectedAction.CriticalDamage : _battleSystem.SelectedAction.BaseDamage;
            await _battleSystem.EnemyGetHit(damage, isCriticalHit);
            hpTargetAfter = _battleSystem.SelectedTarget.EnemyStats.Health;
            Debug.Log("Damage dealt: " + damage);

            // Notify DDA of player damage (mirrors DamageRouletteState)
            DDAIntegration.Instance?.OnPlayerAttack(damage);
            _battleSystem.LastPlayerDamageDealt = damage;

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