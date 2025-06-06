using UnityEngine;

namespace Manager
{
    public class SelectEnemyState: GameState
    {
        public SelectEnemyState(GameManager gameManager, MonoBehaviour monoBehaviour):base(gameManager, monoBehaviour)
        {
          
        }
        public override void OnEnter()
        {
            Debug.Log("Select Enemy");
            if ((_gameManager).SelectedAction == null)
            {
                _gameManager.StateMachine.ChangeState(_gameManager.PlayerTurnState);
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