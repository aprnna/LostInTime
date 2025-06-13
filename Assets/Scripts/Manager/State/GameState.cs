using UnityEngine;

namespace Manager
{
    public abstract class GameState : IState
    {
        protected BattleSystem _battleSystem;
        protected UIManagerBattle _uiManagerBattle;

        public GameState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle)
        {
            _battleSystem = battleSystem;
            _uiManagerBattle = uiManagerBattle;
        }
        public abstract void OnEnter();
        public abstract void OnUpdate();
        public abstract void OnExit();
    }
}