using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

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
            if(EnemiesAvailable())_battleSystem.StateMachine.ChangeState(_battleSystem.EnemyTurnState);
            else
            {
                _battleSystem.ChangeBattleResult(BattleResult.PlayerWin);
                _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            }
        }

        private async UniTask DefendAction()
        {
            var min = _battleSystem.SelectedAction.MinDefend;
            var max =_battleSystem.SelectedAction.MaxDefend;
            var result = await _battleSystem.RouletteSystem.SetRoulette(min,max); 
            Debug.Log("Defend: " + result);
            _battleSystem.SetPlayerDefend(result);
        }

        private async UniTask AttackAction()
        {
            var min = _battleSystem.SelectedAction.MinDamage;
            var max =_battleSystem.SelectedAction.MaxDamage;
            var result = await _battleSystem.RouletteSystem.SetRoulette(min,max); 
            _battleSystem.SelectedTarget.PlayAnim("isDamaged");
            _battleSystem.SelectedTarget.EnemyStats.GetHit(result);
            _battleSystem.UIManagerBattle.EnemyStatsUI.InitializeStats(_battleSystem.SelectedTarget.EnemyStats);
            await _battleSystem.SelectedAction.PlayVfx(_battleSystem.SelectedTarget.transform); 
            Debug.Log("Damage dealt: " + result);
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