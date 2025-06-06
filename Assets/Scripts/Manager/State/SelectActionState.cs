using UnityEngine;

namespace Manager
{
    public class SelectActionState: GameState
    {
        public SelectActionState(GameManager gameManager, MonoBehaviour monoBehaviour): base(gameManager, monoBehaviour)
        {
        }
        public override void OnEnter()
        {
            Debug.Log("Select Action");
            _gameManager.SetActionButton(true);
        }

        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            _gameManager.SetActionButton(false);
        }
    }
}