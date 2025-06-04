using UnityEngine.InputSystem;

namespace Input
{
    public class InputValue<T> where T : struct
    {
        private InputAction _action;
        public InputAction Action => _action;
        private T _value;
        public InputValue(InputAction action)
        {
            _action = action;
        }
        public void ForcePoll()
        {
            _value = _action.ReadValue<T>();
        }
        public T Get()
        {
            return _action.ReadValue<T>();
        }
    }
}