using System;
using Input;
using UnityEngine;

namespace Player
{
    public class PlayerMovement:MonoBehaviour
    {
        private InputManager _inputManager;
        private Rigidbody2D _rigidbody;
        private float _speed = 3f;
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }
        private void Start()
        {
            _inputManager.PlayerMode();
        }

        private void Update()
        {
            OnMove(_inputManager.PlayerInput.Movement.Get());
        }

        private void OnEnable()
        {
            _inputManager = InputManager.Instance;
            _inputManager.PlayerInput.Attack.OnDown += Attack;
        }

        private void OnDisable()
        {
            _inputManager.PlayerInput.Attack.OnDown -= Attack;
        }
 
        private void OnMove(Vector2 value)
        {
            _rigidbody.linearVelocity = value * _speed;
        }
        private void Attack()
        {
            Debug.Log("ATTACK");
        }
    }
}