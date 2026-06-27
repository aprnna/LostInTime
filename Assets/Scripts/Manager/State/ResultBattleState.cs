using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DDA;
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
            _battleSystem.OnBattleEnd();
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

                // Area resolved — agent decides difficulty for next area (inference triggers here).
                DDAIntegration.Instance?.OnAreaComplete(true);

                if (MapSystem.Instance.CurrentPlayerMapNode == MapSystem.Instance.lastNode)
                {
                    DDAIntegration.Instance?.OnRunEnd(true, MapSystem.Instance.AreaIndex, MapSystem.Instance.AreaTotal);
                    await UniTask.Delay(TimeSpan.FromSeconds(1.5), ignoreTimeScale: false);
                    SceneManager.LoadScene("Epilog");
                }
            }
            else
            {
                _battleSystem.GameManager.BattleResult(BattleResult.EnemiesWin);
                // Player lost — area failed, run ends.
                DDAIntegration.Instance?.OnAreaComplete(false);
                DDAIntegration.Instance?.OnRunEnd(false, MapSystem.Instance.AreaIndex, MapSystem.Instance.AreaTotal);
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