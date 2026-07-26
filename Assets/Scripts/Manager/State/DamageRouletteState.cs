using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DDA;

namespace Manager
{
    public class DamageRouletteState: GameState
    {
        private bool _autoStart;
        private GameObject _rouletteObject;
        
        public DamageRouletteState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle): 
            base(battleSystem,uiManagerBattle)
        {
        }
        public override void OnEnter()
        {
            _battleSystem.GameManager.ChangeInstruction("Start Roulette");
            _autoStart = _battleSystem.RouletteSystem.AutoStartRoulette;
            _battleSystem.RouletteSystem.EnableRouletteAction();
            if(_autoStart) OnStartRoulette();
            else _battleSystem.RouletteSystem.SetRouletteButton(OnStartRoulette);
        }
        public void OnStartRoulette()
        {
            StartRoulette().Forget();
            _battleSystem.RouletteSystem.ButtonStartRoulette.SetActive(false);
        }
        private async UniTask StartRoulette()
        {
            if (_battleSystem.SelectedAction.IsDefend)
                await DefendAction();
            else
                await AttackAction();
                
            // Player action completed, increment turn count
            DDAIntegration.Instance?.OnTurnEnd(0, 0);

            if(EnemiesAvailable())
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

        private async UniTask DefendAction()
        {
            var defendValue = _battleSystem.SelectedAction.BaseDefend;
            Debug.Log("Defend: " + defendValue);
            _battleSystem.SetPlayerDefend(defendValue);
            // Defend deals no damage but still consumes a turn — buffer 0 so EnemyTurnState
            // can fire DDA OnTurnEnd(0, enemyDamage) once for this cycle.
            _battleSystem.LastPlayerDamageDealt = 0;
        }

        private async UniTask AttackAction()
        {
            if (_battleSystem.SelectedTarget == null)
            {
                Debug.LogError("[DamageRouletteState] SelectedTarget is null during AttackAction!");
                _battleSystem.LastPlayerDamageDealt = 0;
                return;
            }

            var min = _battleSystem.SelectedAction.BaseDamage;
            var max =_battleSystem.SelectedAction.BaseDamage;
            var result = await _battleSystem.RouletteSystem.SetRoulette(min,max);
            _battleSystem.SelectedTarget.PlayAnim("isDamaged");
            _battleSystem.SelectedTarget.EnemyStats.GetHit(result);
            _battleSystem.UIManagerBattle.EnemyStatsUI.InitializeStats(_battleSystem.SelectedTarget.EnemyStats);
            await _battleSystem.SelectedAction.PlayVfx(_battleSystem.SelectedTarget.transform);
            Debug.Log("Damage dealt: " + result);
            // Buffer damage for DDA: EnemyTurnState will pair it with enemy damage later.
            _battleSystem.LastPlayerDamageDealt = result;
            Debug.Log($"[DamageRouletteState] Player dealt {result} damage. LastPlayerDamageDealt={_battleSystem.LastPlayerDamageDealt}");

            // Notify DDA immediately so observations update in real-time (for F9 panel)
            DDAIntegration.Instance?.OnPlayerAttack(result);

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
            if (_battleSystem.SelectedAction != null && _battleSystem.SelectedAction.IsDefend)
            {
                _uiManagerBattle.SetActionPanel(false);
            }
            else
            {
                if (_battleSystem.SelectedTarget != null)
                {
                    _battleSystem.SelectedTarget.OnChangeMarker(false);
                    _uiManagerBattle.SetEnemyPanel(_battleSystem.SelectedTarget.EnemyStats, false);
                }
                _battleSystem.ResetBattle();
            }
        }
    }
}