using Player;
using UnityEngine;

namespace DDA
{
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
            if (_difficultySettings == null)
            {
                _difficultySettings = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
                if (_difficultySettings != null)
                    Debug.Log("[DifficultyApplier] Auto-loaded DefaultDifficultySettings from Resources.");
            }
        }

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

        public DifficultySettings GetDifficultySettings()
        {
            return _difficultySettings;
        }

        public void SetDifficultySettings(DifficultySettings settings)
        {
            _difficultySettings = settings;
        }
    }
}