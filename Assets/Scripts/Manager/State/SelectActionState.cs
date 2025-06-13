using UnityEngine;

namespace Manager
{
    public class SelectActionState: GameState
    {
        public SelectActionState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle): 
            base(battleSystem, uiManagerBattle)
        {
        }
        public override void OnEnter()
        {
            _battleSystem.GameManager.ChangeInstruction("Select Action");
            _uiManagerBattle.SetActionsButton(true);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _uiManagerBattle.SetActionsButton(false);
            _battleSystem.GameManager.ChangeInstruction("");
        }
    }
}