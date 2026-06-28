using Player;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Applies difficulty multipliers to enemy stats.
    /// Called during battle initialization before enemies spawn.
    /// </summary>
    public class DifficultyApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DifficultySettings _difficultySettings;

        public static DifficultyApplier Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Fallback: load default settings if not assigned via inspector
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
                if (_difficultySettings != null)
                    Debug.Log("[DifficultyApplier] Auto-loaded DefaultDifficultySettings from Resources.");
            }
        }

        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Call this after enemy is spawned, before battle starts.
        /// </summary>
        /// <param name="enemyStats">The enemy stats to modify.</param>
        public void ApplyDifficulty(EnemyStats enemyStats)
        {
            if (enemyStats == null)
            {
                Debug.LogWarning("[DifficultyApplier] enemyStats is null — skipping.");
                return;
            }
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            }
            if (_difficultySettings == null)
            {
                Debug.LogWarning("[DifficultyApplier] DifficultySettings not assigned, using default multipliers.");
                return;
            }

            float hpMult = _difficultySettings.HPMultiplier;
            float dmgMult = _difficultySettings.DamageMultiplier;
            enemyStats.ApplyDifficultyMultiplier(hpMult, dmgMult);

            Debug.Log($"[DifficultyApplier] Applied {_difficultySettings.GetLevelName()} " +
                      $"(HP x{hpMult:F2}, DMG x{dmgMult:F2}) to {enemyStats.EnemyName}");
        }

        /// <summary>
        /// Gets current difficulty settings for read-only access.
        /// </summary>
        public DifficultySettings GetDifficultySettings()
        {
            return _difficultySettings;
        }

        /// <summary>
        /// Sets difficulty settings reference (useful for runtime injection).
        /// </summary>
        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }
    }
}