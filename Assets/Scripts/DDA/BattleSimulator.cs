using UnityEngine;

namespace DDA
{
    public struct BattleResult
    {
        public bool win;
        public HPState hp;
        public TimeState time;
        public DamageState damage;
        public float playerHPRatio;   // HP akhir / HP max (0.0 - 1.0)
        public int totalTurns;
        public int totalDamageTaken;
    }

    /// <summary>
    /// BattleSimulator — mensimulasikan hasil battle untuk training Q-Learning.
    ///
    /// MASALAH SIMULATOR LAMA:
    /// - Win rate terlalu tinggi (~70%) untuk semua action
    /// - HP result selalu High saat menang → agent tidak pernah lihat HP.Medium/Low
    /// - Distribusi state tidak realistis → policy bias ke Decrease terus
    ///
    /// PERBAIKAN:
    /// - Win rate berbeda per action (Increase lebih susah, Decrease lebih mudah)
    /// - HP result terdistribusi secara probabilistik → ada Low/Medium/High
    /// - Time dan Damage juga terdistribusi realistis per action
    /// - Hasilnya: agent belajar bahwa Maintain/Increase juga optimal di kondisi tertentu
    ///
    /// TARGET DISTRIBUSI YANG DIHARAPKAN:
    /// ┌────────────┬──────────┬────────────────────────────────────┐
    /// │ Action     │ Win Rate │ HP Distribution (saat menang)       │
    /// ├────────────┼──────────┼────────────────────────────────────┤
    /// │ Increase   │  40%     │ Low 50% / Medium 35% / High 15%    │
    /// │ Maintain   │  60%     │ Low 20% / Medium 50% / High 30%    │
    /// │ Decrease   │  80%     │ Low 5%  / Medium 30% / High 65%    │
    /// └────────────┴──────────┴────────────────────────────────────┘
    /// Win rate di-adjust berdasarkan current State (state-dependent):
    /// - HP High → player kuat, win rate naik sedikit
    /// - HP Low  → player lemah, win rate turun sedikit
    /// Dengan distribusi ini, Maintain akan optimal saat kondisi Medium (challenge pas)
    /// dan Increase akan optimal hanya saat player terlalu kuat (HP.High konsisten).
    /// </summary>
    public class BattleSimulator
    {
        /// <summary>
        /// Simulate battle. Menerima current state agar outcome state-dependent.
        /// </summary>
        public BattleResult Simulate(DifficultyAction action, State currentState)
        {
            // ── 1. Tentukan win/lose berdasarkan difficulty + current state ──────
            float baseWinChance = action switch
            {
                DifficultyAction.Increase => 0.40f,
                DifficultyAction.Maintain => 0.60f,
                DifficultyAction.Decrease => 0.80f,
                _                         => 0.60f
            };

            // Modifier berdasarkan current HP state
            // Jika HP tinggi → player lebih kuat → sedikit lebih mudah menang
            // Jika HP rendah → player lemah → lebih susah menang
            float hpModifier = currentState.hp switch
            {
                HPState.High   =>  0.10f,
                HPState.Medium =>  0.00f,
                HPState.Low    => -0.10f,
                _              =>  0.00f
            };

            float winChance = Mathf.Clamp01(baseWinChance + hpModifier);

            bool win = Random.value < winChance;

            // ── 2. Tentukan HP State ─────────────────────────────────────────────
            HPState hp = GetHPState(action, win);

            // ── 3. Tentukan Time State ───────────────────────────────────────────
            TimeState time = GetTimeState(action, win);

            // ── 4. Tentukan Damage State ─────────────────────────────────────────
            DamageState damage = GetDamageState(action, win);

            // ── 5. Hitung HP Ratio numerik dari HP State ─────────────────────────
            float hpRatio = GetHPRatio(hp);

            return new BattleResult
            {
                win           = win,
                hp            = hp,
                time          = time,
                damage        = damage,
                playerHPRatio = hpRatio
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HP STATE
        // Saat menang: distribusi berbeda per action
        // Saat kalah : hampir selalu HP Low (habis dihajar musuh)
        // ─────────────────────────────────────────────────────────────────────────
        private HPState GetHPState(DifficultyAction action, bool win)
        {
            if (!win)
            {
                // Kalah → HP hampir pasti rendah
                float r = Random.value;
                return r < 0.75f ? HPState.Low : HPState.Medium;
                // HP.High saat kalah = 0% (tidak masuk akal secara gameplay)
            }

            float roll = Random.value;
            return action switch
            {
                // Increase: susah → HP sering Low/Medium saat menang
                DifficultyAction.Increase =>
                    roll < 0.50f ? HPState.Low    :   // 50%
                    roll < 0.85f ? HPState.Medium  :   // 35%
                                   HPState.High,       // 15%

                // Maintain: normal → HP paling sering Medium (kondisi ideal DDA)
                DifficultyAction.Maintain =>
                    roll < 0.20f ? HPState.Low    :   // 20%
                    roll < 0.70f ? HPState.Medium  :   // 50%
                                   HPState.High,       // 30%

                // Decrease: mudah → HP sering High
                DifficultyAction.Decrease =>
                    roll < 0.05f ? HPState.Low    :   // 5%
                    roll < 0.35f ? HPState.Medium  :   // 30%
                                   HPState.High,       // 65%

                _ => HPState.Medium
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // TIME STATE
        // Increase → battle lebih lama (musuh kuat, butuh banyak turn)
        // Decrease → battle lebih cepat (musuh lemah, cepat mati)
        // ─────────────────────────────────────────────────────────────────────────
        private TimeState GetTimeState(DifficultyAction action, bool win)
        {
            float roll = Random.value;
            return action switch
            {
                DifficultyAction.Increase =>
                    roll < 0.50f ? TimeState.Slow   :  // 50% lama
                    roll < 0.85f ? TimeState.Normal  :  // 35% normal
                                   TimeState.Fast,      // 15% cepat

                DifficultyAction.Maintain =>
                    roll < 0.20f ? TimeState.Slow   :  // 20% lama
                    roll < 0.70f ? TimeState.Normal  :  // 50% normal (ideal)
                                   TimeState.Fast,      // 30% cepat

                DifficultyAction.Decrease =>
                    roll < 0.10f ? TimeState.Slow   :  // 10% lama
                    roll < 0.40f ? TimeState.Normal  :  // 30% normal
                                   TimeState.Fast,      // 60% cepat

                _ => TimeState.Normal
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DAMAGE STATE
        // Increase → damage taken lebih tinggi (musuh kuat)
        // Decrease → damage taken rendah (musuh lemah)
        // ─────────────────────────────────────────────────────────────────────────
        private DamageState GetDamageState(DifficultyAction action, bool win)
        {
            float roll = Random.value;
            return action switch
            {
                DifficultyAction.Increase =>
                    roll < 0.55f ? DamageState.High   :  // 55% damage tinggi
                    roll < 0.85f ? DamageState.Medium  :  // 30% medium
                                   DamageState.Low,       // 15% rendah

                DifficultyAction.Maintain =>
                    roll < 0.25f ? DamageState.High   :  // 25% tinggi
                    roll < 0.65f ? DamageState.Medium  :  // 40% medium (ideal)
                                   DamageState.Low,       // 35% rendah

                DifficultyAction.Decrease =>
                    roll < 0.10f ? DamageState.High   :  // 10% tinggi
                    roll < 0.40f ? DamageState.Medium  :  // 30% medium
                                   DamageState.Low,       // 60% rendah

                _ => DamageState.Medium
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HP RATIO NUMERIK
        // Digunakan untuk logging ke QTableLogger (playerHPRatio 0.0 - 1.0)
        // ─────────────────────────────────────────────────────────────────────────
        private float GetHPRatio(HPState hp)
        {
            return hp switch
            {
                HPState.High   => Random.Range(0.70f, 1.00f),
                HPState.Medium => Random.Range(0.31f, 0.69f),
                HPState.Low    => Random.Range(0.01f, 0.30f),
                _              => 0.5f
            };
        }
    }
}
