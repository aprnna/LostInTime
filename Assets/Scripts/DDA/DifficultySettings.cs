using UnityEngine;

namespace DDA
{
    /// <summary>
    /// ScriptableObject storing difficulty multipliers for DDA system.
    /// Supports 5 discrete levels: Very Easy (0.75x) to Very Hard (1.25x).
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "DDA/DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        [Header("Difficulty Levels")]
        [Tooltip("Multiplier values for each difficulty level")]
        [SerializeField] private float[] _difficultyLevels = { 0.75f, 0.875f, 1.0f, 1.125f, 1.25f };

        [Header("Current State")]
        [Tooltip("Current difficulty level index (0-4)")]
        [SerializeField] private int _currentLevelIndex = 2; // Start at Normal

        /// <summary>Current HP multiplier based on difficulty level.</summary>
        public float HPMultiplier => _difficultyLevels[_currentLevelIndex];

        /// <summary>Current damage multiplier based on difficulty level.</summary>
        public float DamageMultiplier => _difficultyLevels[_currentLevelIndex];

        /// <summary>Current difficulty level index (0-4).</summary>
        public int CurrentLevelIndex => _currentLevelIndex;

        /// <summary>Total number of difficulty levels.</summary>
        public int LevelCount => _difficultyLevels.Length;

        /// <summary>Increases difficulty by one level (max: Very Hard).</summary>
        public void IncreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Min(_currentLevelIndex + 1, _difficultyLevels.Length - 1);
        }

        /// <summary>Decreases difficulty by one level (min: Very Easy).</summary>
        public void DecreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Max(_currentLevelIndex - 1, 0);
        }

        /// <summary>Sets difficulty to specific level index.</summary>
        public void SetLevel(int levelIndex)
        {
            _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _difficultyLevels.Length - 1);
        }

        /// <summary>Resets difficulty to Normal (level 2).</summary>
        public void ResetToNormal()
        {
            _currentLevelIndex = 2;
        }

        /// <summary>Returns difficulty level normalized to 0-1 range.</summary>
        public float GetNormalizedDifficulty()
        {
            return (float)_currentLevelIndex / (_difficultyLevels.Length - 1);
        }

        /// <summary>Gets the difficulty level name for display.</summary>
        public string GetLevelName()
        {
            string[] names = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };
            return names[_currentLevelIndex];
        }

#if UNITY_EDITOR
        [ContextMenu("Reset to Normal")]
        private void ContextResetToNormal() => ResetToNormal();
#endif
    }
}