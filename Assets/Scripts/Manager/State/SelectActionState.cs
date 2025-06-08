using UnityEngine;

namespace Manager
{
    public class SelectActionState: GameState
    {
        public SelectActionState(BattleSystem battleSystem, MonoBehaviour monoBehaviour): base(battleSystem, monoBehaviour)
        {
        }
        public override void OnEnter()
        {
            _battleSystem.GameManager.SetInstruction("Select Action");
            _battleSystem.SetActionButton(true);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _battleSystem.SetActionButton(false);
            _battleSystem.OnChangeActionDescription("");
        }
    }
}