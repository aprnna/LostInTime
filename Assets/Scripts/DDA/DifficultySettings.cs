using UnityEngine;

namespace DDA
{
    /// <summary>
    /// ScriptableObject storing difficulty multipliers for DDA system.
    /// Supports 5 discrete levels: Very Easy (0.75x) to Very Hard (1.25x).
    ///
    /// IMPORTANT: CurrentLevelIndex is INSTANCE state for multi-env training.
    /// Each TrainingBattleSimulator should have its own DifficultySettings instance
    /// or use ResetToNormal() at run start to synchronize.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "DDA/DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        [Header("Difficulty Levels")]
        [Tooltip("Multiplier values for each difficulty level")]
        [SerializeField] private float[] _difficultyLevels = { 0.6f, 0.8f, 1.0f, 1.2f, 1.4f };

        [Header("Current State (Per-Instance)")]
        [Tooltip("Current difficulty level index (0-4). NOTE: For multi-env training, ensure each env has its own instance.")]
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

        /// <summary>
        /// Replaces the difficulty multiplier array at runtime.
        /// Used by the training simulator to force a widened range on its runtime copy,
        /// so a baked asset's serialized (possibly old) values do not constrain training.
        /// Clamps the current level index into the new array bounds.
        /// </summary>
        public void SetDifficultyLevels(float[] levels)
        {
            if (levels == null || levels.Length == 0) return;
            _difficultyLevels = levels;
            _currentLevelIndex = Mathf.Clamp(_currentLevelIndex, 0, levels.Length - 1);
        }

        /// <summary>Resets difficulty to Normal (level 2).</summary>
        public void ResetToNormal()
        {
            _currentLevelIndex = 2;
        }

        /// <summary>
        /// Creates a runtime copy of this DifficultySettings.
        /// IMPORTANT for multi-env training: Each environment should use its own copy
        /// to avoid shared state when agent changes difficulty.
        /// </summary>
        public DifficultySettings CreateRuntimeCopy()
        {
            var copy = CreateInstance<DifficultySettings>();
            copy._difficultyLevels = (float[])_difficultyLevels.Clone();
            copy._currentLevelIndex = _currentLevelIndex;
            return copy;
        }

        /// <summary>Returns difficulty level normalized to 0-1 range.</summary>
        public float GetNormalizedDifficulty()
        {
            return (float)_currentLevelIndex / (_difficultyLevels.Length - 1);
        }

        private static readonly string[] s_LevelNames = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

        /// <summary>Gets the difficulty level name for display.</summary>
        public string GetLevelName() => s_LevelNames[_currentLevelIndex];

#if UNITY_EDITOR
        [ContextMenu("Reset to Normal")]
        private void ContextResetToNormal() => ResetToNormal();
#endif
    }
}