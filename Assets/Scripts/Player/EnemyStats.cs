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
        private int _scaledIntervalDamage;
        private bool _isDifficultyApplied = false;

        private void Awake()
        {
            // Initialize in Awake to ensure _enemyModel is ready before ApplyDifficultyMultiplier
            InitializeModel();
        }

        public void Start()
        {
            // Ensure model is initialized (Awake may not run if component added at runtime)
            if (_enemyModel == null)
            {
                InitializeModel();
            }
        }

        private void InitializeModel()
        {
            if (_enemyData == null)
            {
                Debug.LogError($"[EnemyStats] No EnemySO assigned on {gameObject.name}");
                return;
            }
            _enemyModel = new EnemyModel(_enemyData);
            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
            _scaledIntervalDamage = _enemyModel.IntervalDamage;
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

        public int IntervalDamage => _scaledIntervalDamage;

        public int MinDamage()
        {
            return Mathf.Max(1, _scaledBaseDamage - _scaledIntervalDamage);
        }

        public int MaxDamage()
        {
            return Mathf.Max(2, _scaledBaseDamage + _scaledIntervalDamage);
        }

        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Called after enemy is spawned, before battle starts.
        /// </summary>
        public void ApplyDifficultyMultiplier(float hpMultiplier, float damageMultiplier)
        {
            // Ensure model is initialized
            if (_enemyModel == null)
            {
                InitializeModel();
            }

            _scaledMaxHealth = Mathf.RoundToInt(_enemyModel.MaxHealth * hpMultiplier);
            _scaledBaseDamage = Mathf.RoundToInt(_enemyModel.BaseDamage * damageMultiplier);
            _scaledIntervalDamage = Mathf.RoundToInt(_enemyModel.IntervalDamage * damageMultiplier);
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
                      $"Result: HP={_scaledMaxHealth}, DMG={_scaledBaseDamage}, Interval={_scaledIntervalDamage}");
        }

        /// <summary>
        /// Resets difficulty to base values.
        /// </summary>
        public void ResetDifficulty()
        {
            if (_enemyModel == null) return;

            _scaledMaxHealth = _enemyModel.MaxHealth;
            _scaledBaseDamage = _enemyModel.BaseDamage;
            _scaledIntervalDamage = _enemyModel.IntervalDamage;
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