using DDA;
using UnityEngine;

namespace Player
{
    public class EnemyStats:MonoBehaviour
    {
        [SerializeField] private EnemySO _enemyData;
        private EnemyModel _enemyModel;
        public void Start()
        {
            _enemyModel = new EnemyModel(_enemyData);
            ApplyDifficultyMultipliers();
        }

        /// <summary>
        /// Terapkan multiplier dari DDA DifficultySettings ke HP dan Damage enemy.
        /// Dipanggil saat Start. Bisa juga dipanggil ulang jika difficulty berubah mid-battle.
        /// </summary>
        private void ApplyDifficultyMultipliers()
        {
            if (DifficultySettings.Instance == null) return;

            float hpMult = DifficultySettings.Instance.EnemyHPMultiplier;
            float dmgMult = DifficultySettings.Instance.EnemyDamageMultiplier;

            // Terapkan ke HP
            int scaledMaxHp = Mathf.RoundToInt(_enemyData.MaxHealth * hpMult);
            int scaledHp = Mathf.RoundToInt(_enemyData.Health * hpMult);
            _enemyModel.MaxHealth = Mathf.Max(1, scaledMaxHp);
            _enemyModel.Health = Mathf.Max(1, scaledHp);

            // Terapkan ke Damage
            int scaledDamage = Mathf.RoundToInt(_enemyData.BaseDamage * dmgMult);
            _enemyModel.BaseDamage = Mathf.Max(1, scaledDamage);

            Debug.Log($"[EnemyStats] {EnemyName} scaled: HP={_enemyModel.Health}/{_enemyModel.MaxHealth} " +
                      $"Dmg={_enemyModel.BaseDamage} (hpMult={hpMult:F2}, dmgMult={dmgMult:F2})");
        }
        
        public string EnemyName
        {
            get => _enemyModel.EnemyName;
            private set
            {
                _enemyModel.EnemyName = value;
            }
        }
        public EnemyType EnemyType => _enemyData.EnemyType;
            
        public int Health
        {
            get => _enemyModel.Health;
            private set
            {
                _enemyModel.Health = value ;
            }
        }
        public int MaxHealth
        {
            get => _enemyModel.MaxHealth;
            private set
            {
                _enemyModel.MaxHealth = value;
            }
        }
        public int BaseDamage
        {
            get => _enemyModel.BaseDamage;
            private set
            {
                _enemyModel.BaseDamage = value;
            }
        }
        public int MinDamage()
        {
            return BaseDamage - _enemyModel.IntervalDamage;
        }
        public int MaxDamage()
        {
            return BaseDamage + _enemyModel.IntervalDamage;
        }
        public void GetHit(int value)
        {
            if(Health - value > 0) Health -= value;
            else
            {
                Health = 0;
                Debug.Log("You Died");
                gameObject.SetActive(false);
            }
        }

        public Sprite GetPortrait()
        {
            return _enemyData.EnemyPortrait;
        }
        public bool IsAlive()
        {
            return Health > 0;
        }
    }
}