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

    public class SimEnemy
    {
        public int Health;
        public int MaxHealth;
        public int BaseDamage;
        public int IntervalDamage;

        public bool IsAlive() => Health > 0;
        public int MinDamage() => Mathf.Max(0, BaseDamage - IntervalDamage);
        public int MaxDamage() => BaseDamage + IntervalDamage;
        public void TakeDamage(int dmg)
        {
            Health = Mathf.Max(0, Health - dmg);
        }
        public int RollDamage()
        {
            return Random.Range(MinDamage(), MaxDamage() + 1);
        }
    }

    public class BattleSimulator
    {
        private const int PLAYER_MAX_HP       = 100;
        private const int PLAYER_BASE_DAMAGE  = 12;
        private const int PLAYER_INTERVAL_DMG = 3;
        private const int PLAYER_CRIT_PERCENT = 20;   
        private const int PLAYER_CRIT_BONUS   = 6;    
        private const int PLAYER_BASE_DEFEND  = 2;
        private const int PLAYER_INTERVAL_DEF = 2;
        private const int PLAYER_SHIELD_COUNT = 2;    

        private const int ENEMY_BASE_HP       = 40;
        private const int ENEMY_BASE_DAMAGE   = 10;
        private const int ENEMY_INTERVAL_DMG  = 3;

        private const int MAX_TURNS = 30;             

        private float GetDamageFactor(DifficultyAction action)
        {
            return action switch
            {
                DifficultyAction.Increase => 1.25f,
                DifficultyAction.Decrease => 0.75f,
                _                         => 1.00f,
            };
        }
        private float GetHPFactor(DifficultyAction action)
        {
            return action switch
            {
                DifficultyAction.Increase => 1.20f,
                DifficultyAction.Decrease => 0.80f,
                _                         => 1.00f,
            };
        }
        private int GetEnemyCount(DifficultyAction action)
        {
            return action switch
            {
                DifficultyAction.Increase => Random.Range(2, 4),
                DifficultyAction.Decrease => 1,
                _                         => Random.Range(1, 3), 
            };
        }

        // ===================== Main Simulate =====================
        public BattleResult Simulate(DifficultyAction action)
        {
            float damageFactor = GetDamageFactor(action);
            float hpFactor     = GetHPFactor(action);
            int enemyCount     = GetEnemyCount(action);

            int playerHP      = PLAYER_MAX_HP;
            int playerDefend  = 0;
            int shieldLeft    = PLAYER_SHIELD_COUNT;
            int totalDamageTaken = 0;
            int turn          = 0;

            SimEnemy[] enemies = new SimEnemy[enemyCount];
            for (int i = 0; i < enemyCount; i++)
            {
                enemies[i] = new SimEnemy
                {
                    Health        = Mathf.RoundToInt(ENEMY_BASE_HP * hpFactor),
                    MaxHealth     = Mathf.RoundToInt(ENEMY_BASE_HP * hpFactor),
                    BaseDamage    = Mathf.RoundToInt(ENEMY_BASE_DAMAGE * damageFactor),
                    IntervalDamage = ENEMY_INTERVAL_DMG
                };
            }

            while (turn < MAX_TURNS)
            {
                turn++;

                // --- Player Turn ---
                bool useDefend = shieldLeft > 0 && playerHP < PLAYER_MAX_HP * 0.4f;

                if (useDefend)
                {
                    // Defend action: gain defend shield (meniru DamageRouletteState defend)
                    int defendRoll = Random.Range(
                        PLAYER_BASE_DEFEND - PLAYER_INTERVAL_DEF,
                        PLAYER_BASE_DEFEND + PLAYER_INTERVAL_DEF + 1
                    );
                    playerDefend = Mathf.Max(playerDefend, defendRoll);
                    shieldLeft--;
                }
                else
                {
                    // Attack action: pilih enemy hidup yang HP-nya paling rendah (target paling lemah)
                    SimEnemy target = GetWeakestEnemy(enemies);
                    if (target == null) break; // semua enemy mati

                    // Roll damage (meniru Roulette System)
                    int dmg = Random.Range(
                        PLAYER_BASE_DAMAGE - PLAYER_INTERVAL_DMG,
                        PLAYER_BASE_DAMAGE + PLAYER_INTERVAL_DMG + 1
                    );

                    // Critical hit check
                    bool isCrit = Random.Range(0, 100) < PLAYER_CRIT_PERCENT;
                    if (isCrit)
                        dmg += Mathf.RoundToInt(PLAYER_BASE_DAMAGE * (PLAYER_CRIT_PERCENT / 100f));

                    target.TakeDamage(dmg);
                }

                // Cek semua enemy mati
                if (AllEnemiesDead(enemies)) break;

                // --- Enemy Turn ---
                // Semua enemy hidup menyerang sekaligus (meniru EnemyTurnState)
                int totalEnemyDmg = 0;
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsAlive()) continue;
                    totalEnemyDmg += enemy.RollDamage();
                }

                // Defend menyerap damage dulu (meniru PlayerStats.GetHit)
                if (playerDefend > 0)
                {
                    playerDefend -= totalEnemyDmg;
                    if (playerDefend < 0)
                    {
                        int remaining = Mathf.Abs(playerDefend);
                        playerDefend = 0;
                        playerHP = Mathf.Max(0, playerHP - remaining);
                        totalDamageTaken += remaining;
                    }
                }
                else
                {
                    playerHP = Mathf.Max(0, playerHP - totalEnemyDmg);
                    totalDamageTaken += totalEnemyDmg;
                }

                if (playerHP <= 0) break;
            }

            // ===================== Hitung Result =====================
            bool win = AllEnemiesDead(enemies);
            float hpRatio = (float)playerHP / PLAYER_MAX_HP;

            BattleResult r = new BattleResult
            {
                win              = win,
                playerHPRatio    = hpRatio,
                totalTurns       = turn,
                totalDamageTaken = totalDamageTaken,

                // HPState: berdasarkan HP akhir player
                hp = hpRatio >= 0.6f ? HPState.High :
                     hpRatio >= 0.3f ? HPState.Medium : HPState.Low,

                // TimeState: berdasarkan jumlah turn
                time = turn <= 8  ? TimeState.Fast :
                       turn <= 18 ? TimeState.Normal : TimeState.Slow,

                // DamageState: berdasarkan total damage diterima relatif terhadap max HP
                damage = totalDamageTaken <= PLAYER_MAX_HP * 0.3f ? DamageState.Low :
                         totalDamageTaken <= PLAYER_MAX_HP * 0.6f ? DamageState.Medium : DamageState.High,
            };

            return r;
        }

        private bool AllEnemiesDead(SimEnemy[] enemies)
        {
            foreach (var e in enemies)
                if (e.IsAlive()) return false;
            return true;
        }

        private SimEnemy GetWeakestEnemy(SimEnemy[] enemies)
        {
            SimEnemy target = null;
            foreach (var e in enemies)
            {
                if (!e.IsAlive()) continue;
                if (target == null || e.Health < target.Health)
                    target = e;
            }
            return target;
        }
    }
}