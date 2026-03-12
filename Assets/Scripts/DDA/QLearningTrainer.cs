using UnityEngine;

namespace DDA
{
    public class QLearningTrainer : MonoBehaviour
    {
        [Header("Agent")]
        public QLearningAgent agent;

        [Header("Training Settings")]
        public int episodes       = 5000;
        public float epsilonDecay = 0.9996f;
        public float minEpsilon   = 0.05f;

        [Header("Logging")]
        [Tooltip("Log setiap N episode ke Console")]
        public int logInterval = 500;

        private BattleSimulator _sim = new BattleSimulator();

        void Start()
        {
            // Load Q-table dari sesi sebelumnya sebagai warm-start (jika ada)
            // Agent sudah LoadQTable di Awake, tapi pastikan sudah dimuat
            if (!agent.HasQTable)
            {
                agent.LoadQTable();
            }

            QTableLogger.Instance.LogHyperparameters(agent.Alpha, agent.Gamma, agent.EpsilonStart, epsilonDecay, minEpsilon, episodes);
            Train();
            Evaluate(500);
            agent.SaveQTable();
            Debug.Log("[QLearningTrainer] Training selesai. Q-table tersimpan.");
        }
        void Evaluate(int evalEpisodes)
        {
            float savedEpsilon = agent.Epsilon;
            agent.Epsilon = 0f; // Greedy policy
    
            int wins = 0;
            float totalReward = 0f;
    
            for (int i = 0; i < evalEpisodes; i++)
            {
                State s = RandomState();
                DifficultyAction a = agent.ChooseAction(s);
                BattleResult r = _sim.Simulate(a, s);
                float reward = agent.CalculateReward(r.win, r.hp, r.time, r.damage, a);
                totalReward += reward;
                if (r.win) wins++;
            }
    
            Debug.Log($"[Evaluation] Win Rate: {(float)wins/evalEpisodes:P1} | " +
                      $"Avg Reward: {totalReward/evalEpisodes:F3}");
    
            agent.Epsilon = savedEpsilon; // Restore
        }
        void Train()
        {
            State s = RandomState();
            int resetInterval = 100;
            for (int i = 0; i < episodes; i++)
            {
                DifficultyAction a = agent.ChooseAction(s);
                BattleResult r = _sim.Simulate(a, s);
                float reward = agent.CalculateReward(r.win, r.hp, r.time, r.damage, a);
                State sNext = new State(r.hp, r.time, r.damage);
                agent.UpdateQ(s, a, reward, sNext);
                s = sNext;
                if (i % resetInterval == 0) s = RandomState();
                agent.OnEpisodeEnd(
                    playerHp:          r.playerHPRatio * 100f,
                    playerMaxHp:       100f,
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