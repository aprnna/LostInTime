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
        private MinigamesActionMap _minigames;
        public PlayerActionMap PlayerInput => _player;
        public WorldActionMap World => _world;
        public MinigamesActionMap Minigames => _minigames;
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
            _minigames = new MinigamesActionMap(_inputActions);
        }

        public void PlayerMode()
        {
            _actionMapStates.ChangeState(_player);
        }
        public void WorldMode()
        {
            _actionMapStates.ChangeState(_world);
        }
        public void MinigamesMode()
        {
            _actionMapStates.ChangeState(_minigames);
        }
    }
}