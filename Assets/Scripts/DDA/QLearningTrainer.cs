namespace DDA
{
    using UnityEngine;

    public class QLearningTrainer : MonoBehaviour
    {
        public QLearningAgent agent;
        public int episodes = 5000;
        public float epsilonDecay = 0.995f;
        public float minEpsilon = 0.05f;

        BattleSimulator sim = new BattleSimulator();

        void Start()
        {
            Train();
            agent.SaveQTable();
        }

        void Train()
        {
            for (int i = 0; i < episodes; i++)
            {
                State s = RandomState();
                DifficultyAction a = agent.ChooseAction(s);

                BattleResult r = sim.Simulate(a);

                float reward = agent.CalculateReward(r.win, r.hp, r.time);
                State sNext = new State(r.hp, r.time, r.damage);
                agent.OnEpisodeEnd();

                agent.UpdateQ(s, a, reward, sNext);

                agent.Epsilon = Mathf.Max(minEpsilon, agent.Epsilon * epsilonDecay);

                if (i % 500 == 0)
                    Debug.Log($"Episode {i}, ε={agent.Epsilon:F2}");
            }
        }

        State RandomState()
        {
            return new State(
                (HPState)Random.Range(0, 3),
                (TimeState)Random.Range(0, 3),
                (DamageState)Random.Range(0, 3)
            );
        }
    }

}