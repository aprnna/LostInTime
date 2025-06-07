using UnityEngine;

namespace Manager
{
    public class PlayerTurnState:GameState
    {
        public PlayerTurnState(BattleSystem battleSystem, MonoBehaviour coroutineRunner): base(battleSystem,coroutineRunner)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Player Turn");
            _battleSystem.SetActionButton(false);
            if(!_battleSystem.PlayerStats.IsAlive()) _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
            else _battleSystem.StateMachine.ChangeState(_battleSystem.SelectActionState);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _battleSystem.SetActionButton(false);
        }
    }
}