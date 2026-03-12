using System;
using UnityEngine;

namespace DDA
{
    /// <summary>
    /// Menyimpan multiplier difficulty yang aktif saat ini.
    /// Diubah oleh DDA agent, dibaca oleh sistem battle/enemy.
    /// 
    /// Penggunaan di enemy:
    ///   int finalDamage = Mathf.RoundToInt(baseDamage * DifficultySettings.Instance.EnemyDamageMultiplier);
    ///   int finalHP     = Mathf.RoundToInt(baseHP * DifficultySettings.Instance.EnemyHPMultiplier);
    /// </summary>
    public class DifficultySettings : MonoBehaviour
    {
        // ===================== SINGLETON =====================
        public static DifficultySettings Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ===================== MULTIPLIERS =====================

        [Header("Current Multipliers (runtime)")]
        [SerializeField] private float _enemyHpMultiplier = 1.0f;
        [SerializeField] private float _enemyDamageMultiplier = 1.0f;

        public float EnemyHPMultiplier => _enemyHpMultiplier;
        public float EnemyDamageMultiplier => _enemyDamageMultiplier;

        [Header("Adjustment Settings")]
        [Tooltip("Berapa persen naik/turun per step (0.15 = 15%)")]
        [SerializeField] private float _adjustStep = 0.15f;

        [Tooltip("Batas minimum multiplier")]
        [SerializeField] private float _minMultiplier = 0.5f;

        [Tooltip("Batas maksimum multiplier")]
        [SerializeField] private float _maxMultiplier = 2.0f;

        /// <summary>Event dipanggil setiap kali difficulty berubah</summary>
        public event Action<DifficultyAction, float, float> OnDifficultyChanged;

        // ===================== PUBLIC API =====================

        /// <summary>
        /// Terapkan action dari Q-Learning agent.
        /// </summary>
        public void ApplyAction(DifficultyAction action)
        {
            switch (action)
            {
                case DifficultyAction.Increase:
                    _enemyHpMultiplier     = Mathf.Min(_enemyHpMultiplier + _adjustStep, _maxMultiplier);
                    _enemyDamageMultiplier = Mathf.Min(_enemyDamageMultiplier + _adjustStep, _maxMultiplier);
                    break;

                case DifficultyAction.Decrease:
                    _enemyHpMultiplier     = Mathf.Max(_enemyHpMultiplier - _adjustStep, _minMultiplier);
                    _enemyDamageMultiplier = Mathf.Max(_enemyDamageMultiplier - _adjustStep, _minMultiplier);
                    break;

                case DifficultyAction.Maintain:
                    // Tidak berubah
                    break;
            }

            Debug.Log($"[DifficultySettings] Action={action} | " +
                      $"EnemyHP x{_enemyHpMultiplier:F2} | EnemyDmg x{_enemyDamageMultiplier:F2}");

            OnDifficultyChanged?.Invoke(action, _enemyHpMultiplier, _enemyDamageMultiplier);
        }

        /// <summary>
        /// Reset ke default (misalnya saat new game).
        /// </summary>
        public void ResetToDefault()
        {
            _enemyHpMultiplier = 1.0f;
            _enemyDamageMultiplier = 1.0f;
            Debug.Log("[DifficultySettings] Reset to default (1.0x)");
        }

        /// <summary>
        /// Dapatkan string difficulty level saat ini untuk logging.
        /// </summary>
        public string GetCurrentDifficultyLabel()
        {
            float avg = (_enemyHpMultiplier + _enemyDamageMultiplier) / 2f;
            if (avg >= 1.3f) return "Hard";
            if (avg >= 0.85f) return "Normal";
            return "Easy";
        }
    }
}
