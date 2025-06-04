using System;
using UnityEngine;

namespace Input
{
    public class InputManager:PersistentSingleton<InputManager>
    {
        private InputActions _inputActions;
        private FiniteStateMachine<ActionMap> _actionMapStates;
        private PlayerActionMap _player;
        private WorldActionMap _world;
        public PlayerActionMap PlayerInput => _player;
        public WorldActionMap World => _world;
        protected override void Awake()
        {
            base.Awake();
            InitializedManager();
        }

        private void InitializedManager()
        {
            _inputActions = new InputActions();
            _world = new WorldActionMap(_inputActions);
            _player = new PlayerActionMap(_inputActions);
            _actionMapStates = new FiniteStateMachine<ActionMap>(_world);
        }

        public void PlayerMode()
        {
            _actionMapStates.ChangeState(_player);
        }
    }
}