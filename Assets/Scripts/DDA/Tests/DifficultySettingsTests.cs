using NUnit.Framework;
using UnityEngine;
using DDA;

namespace DDA.Tests
{
    [TestFixture]
    public class DifficultySettingsTests
    {
        private DifficultySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<DifficultySettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void InitialLevel_IsNormal()
        {
            // Default level should be 2 (Normal)
            Assert.AreEqual(2, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.0f, _settings.HPMultiplier);
            Assert.AreEqual(1.0f, _settings.DamageMultiplier);
        }

        [Test]
        public void IncreaseDifficulty_ClampsAtMaximum()
        {
            // Increase beyond maximum
            for (int i = 0; i < 10; i++)
            {
                _settings.IncreaseDifficulty();
            }

            // Should be at maximum level (4)
            Assert.AreEqual(4, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.25f, _settings.HPMultiplier);
        }

        [Test]
        public void DecreaseDifficulty_ClampsAtMinimum()
        {
            // Decrease beyond minimum
            for (int i = 0; i < 10; i++)
            {
                _settings.DecreaseDifficulty();
            }

            // Should be at minimum level (0)
            Assert.AreEqual(0, _settings.CurrentLevelIndex);
            Assert.AreEqual(0.75f, _settings.HPMultiplier);
        }

        [Test]
        public void SetLevel_ValidIndex_SetsCorrectly()
        {
            _settings.SetLevel(3);
            Assert.AreEqual(3, _settings.CurrentLevelIndex);
            Assert.AreEqual(1.125f, _settings.HPMultiplier);
        }

        [Test]
        public void SetLevel_NegativeIndex_ClampsToZero()
        {
            _settings.SetLevel(-5);
            Assert.AreEqual(0, _settings.CurrentLevelIndex);
        }

        [Test]
        public void SetLevel_TooHighIndex_ClampsToMaximum()
        {
            _settings.SetLevel(100);
            Assert.AreEqual(4, _settings.CurrentLevelIndex);
        }

        [Test]
        public void ResetToNormal_SetsLevelTwo()
        {
            _settings.SetLevel(0);
            _settings.ResetToNormal();
            Assert.AreEqual(2, _settings.CurrentLevelIndex);
        }

        [Test]
        public void GetNormalizedDifficulty_ReturnsCorrectRange()
        {
            // Level 0 -> 0.0
            _settings.SetLevel(0);
            Assert.AreEqual(0.0f, _settings.GetNormalizedDifficulty());

            // Level 2 -> 0.5
            _settings.SetLevel(2);
            Assert.AreEqual(0.5f, _settings.GetNormalizedDifficulty());

            // Level 4 -> 1.0
            _settings.SetLevel(4);
            Assert.AreEqual(1.0f, _settings.GetNormalizedDifficulty());
        }

        [Test]
        public void GetLevelName_ReturnsCorrectNames()
        {
            string[] expectedNames = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

            for (int i = 0; i < 5; i++)
            {
                _settings.SetLevel(i);
                Assert.AreEqual(expectedNames[i], _settings.GetLevelName());
            }
        }
    }
}