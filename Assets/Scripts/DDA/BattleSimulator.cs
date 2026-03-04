using UnityEngine;

namespace DDA
{
    public struct BattleResult
    {
        public bool win;
        public HPState hp;
        public TimeState time;
        public DamageState damage;
    }

    public class BattleSimulator
    {
        public BattleResult Simulate(DifficultyAction action)
        {
            float factor = 1f;
            if (action == DifficultyAction.Increase) factor = 1.2f;
            if (action == DifficultyAction.Decrease) factor = 0.8f;

            BattleResult r = new BattleResult();
            r.win = Random.value < (0.7f / factor);

            r.hp = !r.win ? HPState.Low :
                factor > 1.1f ? HPState.Medium : HPState.High;

            r.time = factor > 1.1f ? TimeState.Slow :
                factor < 0.9f ? TimeState.Fast : TimeState.Normal;

            r.damage = factor > 1.1f ? DamageState.High :
                factor < 0.9f ? DamageState.Low : DamageState.Medium;

            return r;
        }
    }
}