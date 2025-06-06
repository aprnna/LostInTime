using UnityEngine;

namespace Manager
{
    public class PlayerTurnState:GameState
    {
        public PlayerTurnState(GameManager gameManager, MonoBehaviour coroutineRunner): base(gameManager,coroutineRunner)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Player Turn");
            _gameManager.SetActionButton(false);
            if(!_gameManager.PlayerStats.IsAlive()) _gameManager.StateMachine.ChangeState(_gameManager.ResultBattleState);
            else _gameManager.StateMachine.ChangeState(_gameManager.SelectActionState);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _gameManager.SetActionButton(false);
        }
    }
}