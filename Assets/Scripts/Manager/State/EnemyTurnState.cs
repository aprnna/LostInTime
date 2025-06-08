using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Manager
{
    public class EnemyTurnState: GameState
    {
        private List<GameObject> _rouletteObjects;
        private GameObject _rouletteObject;
        private int _result;
        public EnemyTurnState(BattleSystem battleSystem, MonoBehaviour coroutineRunner) : base(battleSystem, coroutineRunner)
        {
        }

        public override void OnEnter()
        {
            Debug.Log("Enemy Turn");
            _battleSystem.GameManager.SetInstruction("Enemy Turn");
            _battleSystem.StartCoroutine(ExecuteEnemyAIAll());
        }
         private IEnumerator ExecuteEnemyAIAll()
        {
            yield return new WaitForSeconds(1f);

            List<int> results = new List<int>();
            _rouletteObjects = new List<GameObject>();
            int activeEnemyCount = 0;

            // 1) Spawn semua roulette dan langsung mulai coroutinenya, tanpa menunggu
            foreach (var e in _battleSystem.Enemies)
            {
                if (!e.EnemyStats.IsAlive())
                    continue;

                activeEnemyCount++;

                // Spawn satu instance roulette (GameObject)
                GameObject rouletteGO = _battleSystem.GameManager.SpawnRoulette();
                _rouletteObjects.Add(rouletteGO);

                // Jalankan coroutine SetAndPlayRoulette untuk instance ini,
                // tapi jangan 'yield return' di sini—kita cuma ingin coroutine-nya berlalu di background
                _battleSystem.StartCoroutine(
                    _battleSystem.GameManager.SetAndPLayRoulette(
                        rouletteGO,
                        e.EnemyStats.MinDamage(),
                        e.EnemyStats.MaxDamage(),
                        true, // autoStart = true
                        (result) => {
                            results.Add(result);
                        }
                    )
                );
            }

            // 2) Setelah spawn & mulai semua coroutine di atas, tunggu sampai semua selesai
            yield return new WaitUntil(() => results.Count >= activeEnemyCount);

            // 3) Kalkulasi damage untuk setiap hasil yang terkumpul
            foreach (int roll in results)
            {
                int reduce = roll - _battleSystem.PlayerDefend;
                _battleSystem.PlayerStats.GetHit(reduce);
                Debug.Log($"Enemy hit with {roll} damage (reduced to {reduce})");

                if (!_battleSystem.PlayerStats.IsAlive())
                {
                    _battleSystem.ChangeBattleResult(BattleResult.EnemiesWin);
                    _battleSystem.StateMachine.ChangeState(_battleSystem.ResultBattleState);
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            // 4) Bersihkan semua roulette UI
            foreach (var r in _rouletteObjects)
            {
                GameObject.Destroy(r);
            }

            yield return new WaitForSeconds(1f);
            _battleSystem.StateMachine.ChangeState(_battleSystem.PlayerTurnState);
        }

        public override void OnUpdate()
        {

        }
        public override void OnExit()
        {
            _battleSystem.SetPlayerDefend(0);
        }
    }
}