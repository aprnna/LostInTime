using UnityEngine;

namespace Manager
{
    public class SelectEnemyState: GameState
    {
        public SelectEnemyState(BattleSystem battleSystem,UIManagerBattle uiManagerBattle):
            base(battleSystem, uiManagerBattle)
        {
          
        }
        public override void OnEnter()
        {
            _battleSystem.GameManager.ChangeInstruction("Select Enemy");
            if (_battleSystem.SelectedAction == null)
            {
                _battleSystem.StateMachine.ChangeState(_battleSystem.PlayerTurnState);
                return;
            }
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {

        }
    }
}