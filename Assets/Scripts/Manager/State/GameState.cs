using UnityEngine;

namespace Manager
{
    public abstract class GameState : IState
    {
        protected BattleSystem _battleSystem;
        protected MonoBehaviour _monoBehaviour;

        public GameState(BattleSystem battleSystem, MonoBehaviour monoBehaviour)
        {
            _battleSystem = battleSystem;
            _monoBehaviour = monoBehaviour;
        }
        public abstract void OnEnter();
        public abstract void OnUpdate();
        public abstract void OnExit();
    }
}