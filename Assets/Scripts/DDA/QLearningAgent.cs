using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

namespace DDA
{
    public class QLearningAgent: MonoBehaviour
    {
        private Dictionary<State, float[]> _qTable = new Dictionary<State, float[]>();

        // Hyperparameters
        [SerializeField] private float _alpha = 0.1f;   // learning rate
        [SerializeField] private float _gamma = 0.9f;   // discount factor
        [SerializeField] private float _epsilon = 0.2f; // exploration rate
        
        [Header("Logging")]
        [SerializeField] private int _snapshotEveryNEpisodes = 10;

        private float _episodeTotalReward = 0f;
        private int _episodeSteps = 0;
        private int _episodeCount = 0;

        public float Epsilon { get => _epsilon; set => _epsilon = value; }
        public float Gamma => _gamma;
        public float Alpha => _alpha;
        
        private void InitializeState(State s)
        {
            if (!_qTable.ContainsKey(s))
            {
                _qTable[s] = new float[3]; 
            }
        }
        public DifficultyAction ChooseAction(State state)
        {
            InitializeState(state);

            // Exploration
            if (Random.value < _epsilon)
            {
                return (DifficultyAction)Random.Range(0, 3);
            }

            // Exploitation
            float[] qValues = _qTable[state];
            int bestAction = 0;
            float maxQ = qValues[0];

            for (int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > maxQ)
                {
                    maxQ = qValues[i];
                    bestAction = i;
                }
            }

            return (DifficultyAction)bestAction;
        }
        public float CalculateReward(
            bool win,
            HPState hpState,
            TimeState timeState)
        {
            float reward = 0f;

            // Outcome reward
            
            // reward += win ? 1.0f : -1.0f;
            if (win && hpState == HPState.High)
                reward -= 0.5f;   // terlalu mudah

            if (win && hpState == HPState.Medium)
                reward += 0.5f;   // ideal

            if (win && hpState == HPState.Low)
                reward += 0.2f;   // menang tapi susah

            if (!win && hpState == HPState.Low)
                reward -= 0.2f;   // wajar kalah

            if (!win && hpState == HPState.High)
                reward -= 0.8f;   // terlalu sulit
            
            // HP reward
            // switch (hpState)
            // {
            //     case HPState.Low: reward -= 0.3f; break;
            //     case HPState.Medium: reward += 0.2f; break;
            //     case HPState.High: reward += 0.1f; break;
            // }

            // Time reward
            switch (timeState)
            {
                case TimeState.Fast: reward -= 0.2f; break;
                case TimeState.Normal: reward += 0.2f; break;
                case TimeState.Slow: reward -= 0.2f; break;
            }
            _episodeTotalReward += reward;
            _episodeSteps++;

            return reward;
        }

        
        public void UpdateQ(
            State currentState,
            DifficultyAction action,
            float reward,
            State nextState)
        {
            InitializeState(currentState);
            InitializeState(nextState);

            float[] currentQ = _qTable[currentState];
            float[] nextQ = _qTable[nextState];

            float maxNextQ = Mathf.Max(nextQ);

            currentQ[(int)action] += _alpha *
                                     (reward + _gamma * maxNextQ - currentQ[(int)action]);
        }
        
        public void SaveQTable(string file = "qtable.json")
        {
            QTableData data = new QTableData();
            foreach (var kv in _qTable)
            {
                data.entries.Add(new QTableEntry
                {
                    hp = kv.Key.hp,
                    time = kv.Key.time,
                    damage = kv.Key.damage,
                    qValues = kv.Value
                });
            }

            string path = Path.Combine(Application.persistentDataPath, file);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log("Q-table saved to " + path);
        }

        public void LoadQTable(string file = "qtable.json")
        {
            string path = Path.Combine(Application.persistentDataPath, file);
            if (!File.Exists(path)) return;

            _qTable.Clear();
            var data = JsonUtility.FromJson<QTableData>(File.ReadAllText(path));

            foreach (var e in data.entries)
            {
                State s = new State(e.hp, e.time, e.damage);
                _qTable[s] = e.qValues;
            }

            Debug.Log("Q-table loaded");
        }
        public void OnEpisodeEnd(float playerHP, float playerMaxHP, string currentDifficulty)
        {
            _episodeCount++;

            // 1. Log data episode
            float hpRatio = playerMaxHP > 0 ? playerHP / playerMaxHP : 0f;
            QTableLogger.Instance.LogEpisode(
                totalReward: _episodeTotalReward,
                episodeLength: _episodeSteps,
                playerHPRatio: hpRatio,
                difficultyLevel: currentDifficulty
            );

            // 2. Snapshot Q-table setiap N episode
            if (_episodeCount % _snapshotEveryNEpisodes == 0)
            {
                QTableLogger.Instance.LogQTableSnapshot(_qTable);
            }

            // 3. Reset tracking
            _episodeTotalReward = 0f;
            _episodeSteps = 0;
        }
    }
}