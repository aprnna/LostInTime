using UnityEngine;

namespace Manager
{
    public class SelectEnemyState: GameState
    {
        public SelectEnemyState(BattleSystem battleSystem, MonoBehaviour monoBehaviour):base(battleSystem, monoBehaviour)
        {
          
        }
        public override void OnEnter()
        {
            Debug.Log("Select Enemy");
            if ((_battleSystem).SelectedAction == null)
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