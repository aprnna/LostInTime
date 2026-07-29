using UnityEngine;

namespace DDA
{
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "DDA/DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        [Header("Difficulty Levels")]
        [Tooltip("Multiplier values for each difficulty level")]
        [SerializeField] private float[] _difficultyLevels = { 0.6f, 0.8f, 1.0f, 1.2f, 1.4f };

        [Header("Current State (Per-Instance)")]
        [Tooltip("Current difficulty level index (0-4). NOTE: For multi-env training, ensure each env has its own instance.")]
        [SerializeField] private int _currentLevelIndex = 2; 

        public float HPMultiplier => _difficultyLevels[_currentLevelIndex];

        public float DamageMultiplier => _difficultyLevels[_currentLevelIndex];

        public int CurrentLevelIndex => _currentLevelIndex;

        public int LevelCount => _difficultyLevels.Length;

        public void IncreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Min(_currentLevelIndex + 1, _difficultyLevels.Length - 1);
        }

        public void DecreaseDifficulty()
        {
            _currentLevelIndex = Mathf.Max(_currentLevelIndex - 1, 0);
        }

        public void SetLevel(int levelIndex)
        {
            _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _difficultyLevels.Length - 1);
        }

        public void SetDifficultyLevels(float[] levels)
        {
            if (levels == null || levels.Length == 0) return;
            _difficultyLevels = levels;
            _currentLevelIndex = Mathf.Clamp(_currentLevelIndex, 0, levels.Length - 1);
        }

        public void ResetToNormal()
        {
            _currentLevelIndex = 2;
        }

        public DifficultySettings CreateRuntimeCopy()
        {
            var copy = CreateInstance<DifficultySettings>();
            copy._difficultyLevels = (float[])_difficultyLevels.Clone();
            copy._currentLevelIndex = _currentLevelIndex;
            return copy;
        }

        public float GetNormalizedDifficulty()
        {
            return (float)_currentLevelIndex / (_difficultyLevels.Length - 1);
        }

        private static readonly string[] s_LevelNames = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

        public string GetLevelName() => s_LevelNames[_currentLevelIndex];

#if UNITY_EDITOR
        [ContextMenu("Reset to Normal")]
        private void ContextResetToNormal() => ResetToNormal();
#endif
    }
}