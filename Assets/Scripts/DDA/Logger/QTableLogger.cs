using System;
using System.Collections.Generic;
using System.IO;
using DDA;
using UnityEngine;

public class QTableLogger : MonoBehaviour
{
    [Header("Logging Settings")]
    [Tooltip("Interval episode untuk snapshot Q-Table (untuk convergence test)")]
    public int snapshotInterval = 10;

    [Tooltip("Nama file output (tanpa ekstensi)")]
    public string logFileName = "qtable_evaluation";

    [Header("Auto Save")]
    [Tooltip("Otomatis simpan saat aplikasi quit")]
    public bool autoSaveOnQuit = true;

    // ===================== DATA STRUCTURES =====================

    [Serializable]
    private class EpisodeLog
    {
        public int episode;
        public float totalReward;
        public int episodeLength;    // jumlah langkah/turn
        public float playerHPRatio;  // HP player akhir / HP max (0-1)
        public string difficulty;    // "Increase", "Maintain", "Decrease"
        public long timestamp;
    }

    [Serializable]
    private class QTableSnapshot
    {
        public int episode;
        public float maxDeltaQ;   // perubahan Q value terbesar (untuk convergence)
        public float meanAbsQ;    // rata-rata absolut semua nilai Q
        public float stdQ;        // standard deviation nilai Q
        public List<QStateEntry> states = new List<QStateEntry>();
    }

    [Serializable]
    private class QStateEntry
    {
        /// <summary>
        /// Format: "HP:{HPState}_Time:{TimeState}_Dmg:{DamageState}"
        /// Contoh: "HP:High_Time:Normal_Dmg:Low"
        /// Sebelumnya selalu "DDA.State" karena State.ToString() tidak di-override.
        /// </summary>
        public string state;
        public List<float> qValues;   // nilai Q untuk tiap action [Maintain, Increase, Decrease]
        public int bestAction;        // index action dengan Q value tertinggi
        public string bestActionName; // nama action yang readable
    }

    [Serializable]
    private class HyperparameterLog
    {
        public float alpha;
        public float gamma;
        public float epsilonStart;
        public float epsilonEnd;
        public float epsilonDecay;
        public int totalEpisodes;
    }

    [Serializable]
    private class EvaluationData
    {
        public string sessionId;
        public string startTime;
        public HyperparameterLog hyperparameters = new HyperparameterLog();
        public List<EpisodeLog> episodes = new List<EpisodeLog>();
        public List<QTableSnapshot> qtableSnapshots = new List<QTableSnapshot>();
    }

    // ===================== PRIVATE STATE =====================
    private EvaluationData _data = new EvaluationData();
    private int _currentEpisode = 0;
    // Key: State.ToString() yang sudah readable, Value: Q values sebelumnya
    private Dictionary<string, float[]> _previousQTable = new Dictionary<string, float[]>();
    private string _savePath;

    // Nama action sesuai urutan DifficultyAction enum (Maintain=0, Increase=1, Decrease=2)
    private static readonly string[] ACTION_NAMES = { "Maintain", "Increase", "Decrease" };

    // ===================== SINGLETON =====================
    public static QTableLogger Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _data.sessionId = Guid.NewGuid().ToString("N")[..8];
        _data.startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _savePath = Path.Combine(
            Application.persistentDataPath,
            $"{logFileName}_{_data.sessionId}.json"
        );

        Debug.Log($"[QTableLogger] Session: {_data.sessionId} | Save path: {_savePath}");
    }

    // ===================== PUBLIC API =====================

    /// <summary>
    /// Catat hyperparameter training untuk dokumentasi di JSON output.
    /// Panggil sekali di awal training dari QLearningTrainer.
    /// </summary>
    public void LogHyperparameters(float alpha, float gamma, float epsilonStart,
                                    float epsilonDecay, float minEpsilon, int totalEpisodes)
    {
        _data.hyperparameters = new HyperparameterLog
        {
            alpha         = alpha,
            gamma         = gamma,
            epsilonStart  = epsilonStart,
            epsilonEnd    = minEpsilon,
            epsilonDecay  = epsilonDecay,
            totalEpisodes = totalEpisodes
        };
        Debug.Log($"[QTableLogger] Hyperparameters logged: α={alpha} γ={gamma} ε={epsilonStart}→{minEpsilon} decay={epsilonDecay}");
    }

    /// <summary>
    /// Panggil di akhir setiap episode dari DDA/Battle system.
    /// </summary>
    public void LogEpisode(float totalReward, int episodeLength,
                            float playerHPRatio = 0f, string difficultyLevel = "Maintain")
    {
        _currentEpisode++;

        _data.episodes.Add(new EpisodeLog
        {
            episode       = _currentEpisode,
            totalReward   = totalReward,
            episodeLength = episodeLength,
            playerHPRatio = playerHPRatio,
            difficulty    = difficultyLevel,
            timestamp     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        if (_currentEpisode % snapshotInterval == 0)
            Debug.Log($"[QTableLogger] Ep {_currentEpisode} | Reward: {totalReward:F3} | HP: {playerHPRatio:P0} | Difficulty: {difficultyLevel}");
    }

    /// <summary>
    /// Panggil setiap N episode dari DDA agent, dengan Q-table saat ini.
    /// State key akan tampil sebagai "HP:High_Time:Normal_Dmg:Low" (readable).
    /// Pastikan State.ToString() sudah di-override di State.cs.
    /// </summary>
    public void LogQTableSnapshot(Dictionary<State, float[]> currentQTable)
    {
        float maxDelta   = 0f;
        float sumAbsQ    = 0f;
        float sumSqQ     = 0f;
        int totalEntries = 0;

        var snapshot = new QTableSnapshot { episode = _currentEpisode };

        foreach (var kvp in currentQTable)
        {
            // FIX UTAMA: State.ToString() sekarang return "HP:High_Time:Normal_Dmg:Low"
            // bukan "DDA.State" seperti sebelumnya
            string stateKey = kvp.Key.ToString();
            float[] qValues = kvp.Value;

            // Hitung delta dari snapshot sebelumnya (untuk convergence check)
            if (_previousQTable.TryGetValue(stateKey, out float[] prevValues))
            {
                for (int i = 0; i < qValues.Length; i++)
                {
                    float delta = Mathf.Abs(qValues[i] - prevValues[i]);
                    maxDelta = Mathf.Max(maxDelta, delta);
                }
            }

            // Hitung statistik dan cari best action
            int   bestAction = 0;
            float bestQ      = float.NegativeInfinity;

            for (int i = 0; i < qValues.Length; i++)
            {
                float q = qValues[i];
                sumAbsQ += Mathf.Abs(q);
                sumSqQ  += q * q;
                totalEntries++;

                if (q > bestQ)
                {
                    bestQ      = q;
                    bestAction = i;
                }
            }

            snapshot.states.Add(new QStateEntry
            {
                state          = stateKey,
                qValues        = new List<float>(qValues),
                bestAction     = bestAction,
                bestActionName = bestAction < ACTION_NAMES.Length
                                    ? ACTION_NAMES[bestAction]
                                    : bestAction.ToString()
            });
        }

        snapshot.maxDeltaQ = maxDelta;
        snapshot.meanAbsQ  = totalEntries > 0 ? sumAbsQ / totalEntries : 0f;
        snapshot.stdQ      = totalEntries > 0
            ? Mathf.Sqrt(sumSqQ / totalEntries - (snapshot.meanAbsQ * snapshot.meanAbsQ))
            : 0f;

        _data.qtableSnapshots.Add(snapshot);

        // Simpan Q-table saat ini sebagai "previous" untuk delta berikutnya
        _previousQTable.Clear();
        foreach (var kvp in currentQTable)
            _previousQTable[kvp.Key.ToString()] = (float[])kvp.Value.Clone();

        Debug.Log($"[QTableLogger] Snapshot ep{_currentEpisode} | " +
                  $"MaxDelta: {maxDelta:F5} | MeanAbsQ: {snapshot.meanAbsQ:F3} | " +
                  $"States: {snapshot.states.Count}");
    }

    /// <summary>
    /// Simpan semua data ke file JSON. Dipanggil otomatis saat quit jika autoSaveOnQuit=true.
    /// </summary>
    public void SaveToFile()
    {
        string json = JsonUtility.ToJson(_data, prettyPrint: true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"[QTableLogger] Saved {_data.episodes.Count} episodes, " +
                  $"{_data.qtableSnapshots.Count} snapshots → {_savePath}");
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit) SaveToFile();
    }

    public string GetSavePath() => _savePath;
}