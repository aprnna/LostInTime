using System.Collections;
using UnityEngine;

namespace Manager
{
    public class ResultBattleState:GameState
    {
        public bool isContinueClicked  = false;
        public ResultBattleState(BattleSystem battleSystem, MonoBehaviour monoBehaviour) : base(battleSystem,
            monoBehaviour)
        {
            
        }
        public override void OnEnter()
        {
            Debug.Log("Battle Result ");
            _monoBehaviour.StartCoroutine(BattleResultRuntime());
        }

        private IEnumerator BattleResultRuntime()
        {
            if (_battleSystem.BattleResult == BattleResult.PlayerWin)
            {
                _battleSystem.DropItems();
                yield return new WaitUntil(() => isContinueClicked);
                _battleSystem.ClearDropItem();
                _battleSystem.SetMap(true);
            }else 
            {
                _battleSystem.SetBattleResult(true);
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