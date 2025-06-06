using UnityEngine;

namespace Manager
{
    public abstract class GameState : IState
    {
        protected GameManager _gameManager;
        protected MonoBehaviour _monoBehaviour;

        public GameState(GameManager gameManager, MonoBehaviour monoBehaviour)
        {
            _gameManager = gameManager;
            _monoBehaviour = monoBehaviour;
        }
        public abstract void OnEnter();
        public abstract void OnUpdate();
        public abstract void OnExit();
    }
}