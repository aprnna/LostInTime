using UnityEngine;

namespace Manager
{
    public class PlayerTurnState:GameState
    {
        public PlayerTurnState(BattleSystem battleSystem,UIManagerBattle uiManagerBattle): 
            base(battleSystem,uiManagerBattle)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Player Turn");
            if(!_battleSystem.PlayerStats.IsAlive()) _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            else _battleSystem.StateMachine.ChangeState(_battleSystem.SelectActionState);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
        }
    }
}