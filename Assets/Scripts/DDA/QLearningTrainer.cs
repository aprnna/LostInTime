using UnityEngine;

namespace DDA
{
    public class QLearningTrainer : MonoBehaviour
    {
        [Header("Agent")]
        public QLearningAgent agent;

        [Header("Training Settings")]
        public int episodes       = 5000;
        public float epsilonDecay = 0.995f;
        public float minEpsilon   = 0.05f;

        [Header("Logging")]
        [Tooltip("Log setiap N episode ke Console")]
        public int logInterval = 500;

        private BattleSimulator _sim = new BattleSimulator();

        void Start()
        {
            agent.ResetEpsilon();
            Train();
            QTableLogger.Instance.LogHyperparameters(agent.Alpha, agent.Gamma, agent.EpsilonStart, epsilonDecay, minEpsilon, episodes);
            agent.SaveQTable();
            Debug.Log("[QLearningTrainer] Training selesai. Q-table tersimpan.");
        }

        void Train()
        {
            for (int i = 0; i < episodes; i++)
            {
                State s = RandomState();

                DifficultyAction a = agent.ChooseAction(s);

                BattleResult r = _sim.Simulate(a);

                float reward = agent.CalculateReward(r.win, r.hp, r.time);

                State sNext = new State(r.hp, r.time, r.damage);

                agent.UpdateQ(s, a, reward, sNext);

                agent.OnEpisodeEnd(
                    playerHP:          r.playerHPRatio * 100f,
                    playerMaxHP:       100f,
                    currentDifficulty: a.ToString()
                );

                agent.Epsilon = Mathf.Max(minEpsilon, agent.Epsilon * epsilonDecay);

                if (i % logInterval == 0)
                {
                    Debug.Log($"[Trainer] Episode {i}/{episodes} | " +
                              $"ε={agent.Epsilon:F3} | " +
                              $"Action={a} | Win={r.win} | " +
                              $"HP={r.playerHPRatio:P0} | " +
                              $"Turns={r.totalTurns} | " +
                              $"Reward={reward:F2}");
                }
            }
        }
        State RandomState()
        {
            return new State(
                (HPState)    Random.Range(0, 3),
                (TimeState)  Random.Range(0, 3),
                (DamageState)Random.Range(0, 3)
            );
        }
    }
}