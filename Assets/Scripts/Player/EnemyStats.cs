using UnityEngine;

namespace Player
{
    public class EnemyStats : MonoBehaviour
    {
        [SerializeField] private EnemySO _enemyData;
        private EnemyModel _enemyModel;

        // Track scaled values for DDA
        private int _scaledMaxHealth;
        private int _scaledBaseDamage;
        private bool _isDifficultyApplied = false;

        public void Start()
        {
            _enemyModel = new EnemyModel(_enemyData);
            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
        }

        public string EnemyName
        {
            get => _enemyModel.EnemyName;
            private set => _enemyModel.EnemyName = value;
        }

        public EnemyType EnemyType => _enemyData.EnemyType;

        public int Health
        {
            get => _enemyModel.Health;
            private set => _enemyModel.Health = value;
        }

        public int MaxHealth => _scaledMaxHealth;

        public int BaseDamage => _scaledBaseDamage;

        public int MinDamage()
        {
            return BaseDamage - _enemyModel.IntervalDamage;
        }

        public int MaxDamage()
        {
            return BaseDamage + _enemyModel.IntervalDamage;
        }

        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Called by DifficultyApplier before battle.
        /// </summary>
        public void ApplyDifficultyMultiplier(float hpMultiplier, float damageMultiplier)
        {
            _scaledMaxHealth = Mathf.RoundToInt(_enemyModel.MaxHealth * hpMultiplier);
            _scaledBaseDamage = Mathf.RoundToInt(_enemyModel.BaseDamage * damageMultiplier);
            _isDifficultyApplied = true;

            // Scale current health proportionally if already initialized
            if (_enemyModel.Health > 0)
            {
                float healthRatio = (float)_enemyModel.Health / _enemyModel.MaxHealth;
                _enemyModel.Health = Mathf.RoundToInt(_scaledMaxHealth * healthRatio);
            }
            else
            {
                _enemyModel.Health = _scaledMaxHealth;
            }

            Debug.Log($"[EnemyStats] Applied difficulty: HP={hpMultiplier:F2}x, DMG={damageMultiplier:F2}x. " +
                      $"Result: HP={_scaledMaxHealth}, DMG={_scaledBaseDamage}");
        }

        /// <summary>
        /// Resets difficulty to base values.
        /// </summary>
        public void ResetDifficulty()
        {
            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
            _isDifficultyApplied = false;
        }

        public void GetHit(int value)
        {
            if (Health - value > 0)
            {
                Health -= value;
            }
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