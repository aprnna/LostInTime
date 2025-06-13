using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class ResultBattleState:GameState
    {
        public ResultBattleState(BattleSystem battleSystem, UIManagerBattle uiManagerBattle) : 
            base(battleSystem, uiManagerBattle)
        {
            
        }
        public override void OnEnter()
        {
            Debug.Log("Battle Result ");
            _battleSystem.GameManager.ChangeInstruction("");
            BattleResultRuntime().Forget();
        }

        private async UniTask BattleResultRuntime()
        {
            if (_battleSystem.BattleResult == BattleResult.PlayerWin)
            {
                _battleSystem.DropItems();
                await _battleSystem.UIManagerBattle.ButtonCollectReward.OnClickAsync();
                if(_battleSystem.PlayerStats.IsLevelUp) await UniTask.WaitUntil(() =>_battleSystem.PlayerStats.IsLevelUp == false);
                else _battleSystem.PlayerStats.ResetLevelUpStatus();
                _battleSystem.Leave();
                var currentMapType = _battleSystem.MapSystem.GetMapType();
                if (currentMapType == MapType.Boss) _battleSystem.GameManager.NextBiome();
                _battleSystem.GameManager.BattleResult(BattleResult.PlayerWin);
                if (MapSystem.Instance.CurrentPlayerMapNode == MapSystem.Instance.lastNode)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1.5), ignoreTimeScale: false);
                    SceneManager.LoadScene("Epilog");
                }
            }
            else
            {
                _battleSystem.GameManager.BattleResult(BattleResult.EnemiesWin);
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