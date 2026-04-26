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
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Applies difficulty multipliers to enemy stats.
        /// Call this after enemy is spawned, before battle starts.
        /// </summary>
        /// <param name="enemyStats">The enemy stats to modify.</param>
        public void ApplyDifficulty(EnemyStats enemyStats)
        {
            if (_difficultySettings == null)
            {
                Debug.LogWarning("[DifficultyApplier] DifficultySettings not assigned, using default multipliers.");
                return;
            }

            enemyStats.ApplyDifficultyMultiplier(
                _difficultySettings.HPMultiplier,
                _difficultySettings.DamageMultiplier
            );
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