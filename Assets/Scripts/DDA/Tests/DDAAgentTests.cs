using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace DDA.Tests
{
    [TestFixture]
    public class DDAAgentRewardTests
    {
        private GameObject _testObject;
        private DDAAgent _agent;
        private DifficultySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestAgent");
            _agent = _testObject.AddComponent<DDAAgent>();
            _settings = ScriptableObject.CreateInstance<DifficultySettings>();

            // Use reflection to set private field
            var field = typeof(DDAAgent).GetField("_difficultySettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(_agent, _settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
            }
            if (_settings != null)
            {
                Object.DestroyImmediate(_settings);
            }
        }

        [Test]
        public void CalculateReward_WinWithOptimalHP_ReturnsMaximumReward()
        {
            // Win with 50% HP (optimal)
            float reward = InvokeCalculateReward(_agent, true, 50, 100, 10);

            // Expected: 1.0 (win) + 0.5 (optimal HP) = 1.5
            Assert.AreEqual(1.5f, reward, 0.01f);
        }

        [Test]
        public void CalculateReward_WinWithLowHP_ReturnsWinWithPenalty()
        {
            // Win with 20% HP (too hard)
            float reward = InvokeCalculateReward(_agent, true, 20, 100, 10);

            // Penalty: 0.1 * (0.4 - 0.2) = 0.02
            // Reward = 1.0 - 0.02 = 0.98
            Assert.AreEqual(0.98f, reward, 0.05f);
        }

        [Test]
        public void CalculateReward_Loss_ReturnsNegativeReward()
        {
            // Loss
            float reward = InvokeCalculateReward(_agent, false, 0, 100, 10);

            Assert.AreEqual(-1.0f, reward, 0.01f);
        }

        [Test]
        public void CalculateReward_WinWithSlowClear_AppliesEfficiencyPenalty()
        {
            // Win but took 20 turns (expected 10)
            float reward = InvokeCalculateReward(_agent, true, 50, 100, 20);

            // Expected: 1.5 (win + optimal HP) - 0.1 (10 extra turns * 0.01)
            Assert.AreEqual(1.4f, reward, 0.05f);
        }

        [Test]
        public void CalculateReward_WinWithTooEasy_AppliesPenalty()
        {
            // Win with 80% HP (too easy)
            float reward = InvokeCalculateReward(_agent, true, 80, 100, 10);

            // Penalty: 0.1 * (0.8 - 0.6) = 0.02
            // Reward = 1.0 - 0.02 = 0.98
            Assert.AreEqual(0.98f, reward, 0.05f);
        }

        private float InvokeCalculateReward(DDAAgent agent, bool playerWon, int playerEndHP,
                                            int playerStartHP, int turns)
        {
            var method = typeof(DDAAgent).GetMethod("CalculateReward",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)method.Invoke(agent, new object[] { playerWon, playerEndHP, playerStartHP, turns });
        }
    }
}