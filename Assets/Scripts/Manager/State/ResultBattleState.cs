using System.Collections;
using UnityEngine;

namespace Manager
{
    public class ResultBattleState:GameState
    {
        private bool isContinueClicked  = false;
        public ResultBattleState(BattleSystem battleSystem, MonoBehaviour monoBehaviour) : base(battleSystem,
            monoBehaviour)
        {
            
        }
        public override void OnEnter()
        {
            Debug.Log("Battle Result ");
            _battleSystem.GameManager.SetInstruction("");
            _monoBehaviour.StartCoroutine(BattleResultRuntime());
        }

        public void Continue()
        {
            isContinueClicked = true;
        }
        private IEnumerator BattleResultRuntime()
        {
            if (_battleSystem.BattleResult == BattleResult.PlayerWin)
            {
                _battleSystem.DropItems();
                _battleSystem.ChangeStatusMap(true);
                yield return new WaitUntil(() => isContinueClicked);
                _battleSystem.Leave();
                ChangeBackground();
            }else 
            {
                _battleSystem.SetBattleResult(true);
            }
        }

        private void ChangeBackground()
        {
            var currentMapType = _battleSystem.GameManager.GetMapType();
            var newBackground = _battleSystem.GameManager.GetBackground();
            if(currentMapType == MapType.Boss) _battleSystem.GameManager.ChangeBackground(newBackground);
         
        }
        public override void OnUpdate()
        {
        }
        public override void OnExit()
        {
            isContinueClicked = false;
        }
    }
}