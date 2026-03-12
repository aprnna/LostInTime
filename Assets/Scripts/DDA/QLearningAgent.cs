using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DDA
{
    public class QLearningAgent: MonoBehaviour
    {
        private Dictionary<State, float[]> _qTable = new Dictionary<State, float[]>();

        // Hyperparameters
        [SerializeField] private float _alpha = 0.1f;   // learning rate
        [SerializeField] private float _gamma = 0.9f; 
        [SerializeField] private float _epsilonStart = 0.3f;
        [Header("Logging")]
        [SerializeField] private int _snapshotEveryNEpisodes = 10;
        private Dictionary<State, int> _stateVisitCount = new Dictionary<State, int>();
        private float _episodeTotalReward = 0f;
        private int _episodeSteps = 0;
        private int _episodeCount = 0;

        public float Epsilon { get => _epsilon; set => _epsilon = value; }
        public float Alpha => _alpha;
        public float Gamma => _gamma;
        public float EpsilonStart => _epsilonStart;
        private float _epsilon; 

        private void Awake()
        {
            _epsilon = _epsilonStart;
            LoadQTable();
        }
        
        public bool HasQTable => _qTable.Count > 0;
        private void InitializeState(State s)
        {
            if (!_qTable.ContainsKey(s))
            {
                _qTable[s] = new float[3]; 
            }
        }
        public void TrackVisitation(State s)
        {
            if (!_stateVisitCount.ContainsKey(s)) _stateVisitCount[s] = 0;
            _stateVisitCount[s]++;
        }

        public Dictionary<State, int> GetVisitationStats() => _stateVisitCount;
        public DifficultyAction ChooseAction(State state)
        {
            InitializeState(state);
            TrackVisitation(state);
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
        public float CalculateReward(bool win, HPState hp, TimeState time, DamageState damage,
            DifficultyAction action = DifficultyAction.Maintain)
        {
            float reward = win ? 1.5f : -1.5f;

            if (win) {
                reward += hp switch { HPState.Medium => 1.0f, HPState.High => -0.8f, HPState.Low => 0.3f, _ => 0f };
            } else {
                reward += hp switch { HPState.Low => 0.5f, HPState.Medium => -0.3f, HPState.High => -0.8f, _ => 0f };
            }

            reward += time switch { TimeState.Normal => 0.3f, _ => -0.2f };
            if (win) {
                reward += damage switch {
                    DamageState.Medium => 0.2f,  // ideal
                    DamageState.High   => -0.1f, // susah tapi menang
                    DamageState.Low    => -0.1f, // terlalu mudah
                    _ => 0f
                };
            }

            // ── Action-based reward shaping ──────────────────────────────────
            // Penalti untuk Decrease agar agent tidak selalu menurunkan difficulty
            // Bonus untuk Increase saat player menang (challenge tercapai)
            reward += action switch
            {
                DifficultyAction.Decrease => -0.3f,            // penalti: terlalu mudah
                DifficultyAction.Increase => win ? 0.5f : 0f,  // bonus: menang meski sulit = ideal
                DifficultyAction.Maintain => 0.1f,             // sedikit bonus: stabilitas
                _ => 0f
            };

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

            // Manual max — Mathf.Max() tidak menerima array
            float maxNextQ = nextQ[0];
            for (int i = 1; i < nextQ.Length; i++)
            {
                if (nextQ[i] > maxNextQ) maxNextQ = nextQ[i];
            }

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
        public void OnEpisodeEnd(float playerHp, float playerMaxHp, string currentDifficulty)
        {
            _episodeCount++;

            // 1. Log data episode
            float hpRatio = playerMaxHp > 0 ? playerHp / playerMaxHp : 0f;
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