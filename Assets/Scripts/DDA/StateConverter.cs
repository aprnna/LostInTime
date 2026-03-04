namespace DDA
{
    public static class StateConverter
    {
        // -------- HP STATE --------
        public static HPState GetHPState(int hpEnd, int hpMax)
        {
            float hpRatio = (float)hpEnd / hpMax;

            if (hpRatio <= 0.30f)
                return HPState.Low;
            else if (hpRatio <= 0.70f)
                return HPState.Medium;
            else
                return HPState.High;
        }

        // -------- DAMAGE STATE --------
        public static DamageState GetDamageState(int hpStart, int hpEnd)
        {
            int damageTaken = hpStart - hpEnd;
            float damageRatio = (float)damageTaken / hpStart;

            if (damageRatio < 0.20f)
                return DamageState.Low;
            else if (damageRatio <= 0.50f)
                return DamageState.Medium;
            else
                return DamageState.High;
        }

        // -------- TIME STATE --------
        public static TimeState GetTimeState(int turnCount)
        {
            const int FAST_THRESHOLD = 3;
            const int SLOW_THRESHOLD = 6;

            if (turnCount < FAST_THRESHOLD)
                return TimeState.Fast;
            else if (turnCount <= SLOW_THRESHOLD)
                return TimeState.Normal;
            else
                return TimeState.Slow;
        }

        // -------- FINAL STATE --------
        public static State GetState(
            int hpStart,
            int hpEnd,
            int hpMax,
            int turnCount)
        {
            HPState hpState = GetHPState(hpEnd, hpMax);
            DamageState damageState = GetDamageState(hpStart, hpEnd);
            TimeState timeState = GetTimeState(turnCount);

            return new State(hpState, timeState, damageState);
        }
    }
}