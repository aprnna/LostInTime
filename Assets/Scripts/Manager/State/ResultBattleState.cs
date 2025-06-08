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
                
                var currentMapType = _battleSystem.GameManager.GetMapType();
                var nexBiome = _battleSystem.GameManager.GetNextBiome();
                if(currentMapType == MapType.Boss) ChangeBiome(nexBiome);
            }else 
            {
                _battleSystem.SetBattleResult(true);
            }
        }

        private void ChangeBiome(Biome biome)
        {
            var newBackground = _battleSystem.GameManager.GetBackground();
            _battleSystem.GameManager.ChangeBiome(newBackground, biome);
         
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