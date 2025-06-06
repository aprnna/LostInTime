using UnityEngine;

namespace Manager
{
    public class ResultBattleState:GameState
    {
        public ResultBattleState(GameManager gameManager, MonoBehaviour monoBehaviour) : base(gameManager,
            monoBehaviour)
        {
            
        }
        public override void OnEnter()
        {
            Debug.Log("Battle Result ");
            if (_gameManager.BattleResult == BattleResult.PlayerWin)
            {
                _gameManager.SetMap(true);
            }else 
            {
                _gameManager.SetBattleResult(true);
            }
        }
        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
        }
    }
}