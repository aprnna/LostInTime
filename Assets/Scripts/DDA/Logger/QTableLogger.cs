using System;
using System.Collections.Generic;
using System.IO;
using DDA;
using UnityEngine;

/// <summary>
/// QTableLogger: Logs Q-table snapshots and episode rewards to JSON files
/// untuk dianalisis di Jupyter Notebook (convergence test & cumulative reward plot).
///
/// Cara pakai:
/// 1. Attach script ini ke GameObject yang sama dengan DDA/QLearning agent kamu
/// 2. Panggil LogEpisode() di akhir setiap episode
/// 3. Panggil LogQTableSnapshot() setiap N episode untuk convergence test
/// 4. File akan disimpan di Application.persistentDataPath
/// </summary>
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
        public int episodeLength;   // jumlah langkah/turn
        public float playerHPRatio; // HP player akhir / HP max (0-1)
        public string difficulty;   // "Easy", "Normal", "Hard" - level DDA saat itu
        public long timestamp;
    }

    [Serializable]
    private class QTableSnapshot
    {
        public int episode;
        public float maxDeltaQ;     // perubahan Q value terbesar (untuk convergence)
        public float meanAbsQ;      // rata-rata absolut semua nilai Q
        public float stdQ;          // standard deviation nilai Q
        public List<QStateEntry> states = new List<QStateEntry>();
    }

    [Serializable]
    private class QStateEntry
    {
        public string state;        // representasi state sebagai string
        public List<float> qValues; // nilai Q untuk tiap action
        public int bestAction;      // action dengan Q value tertinggi
    }

    [Serializable]
    private class EvaluationData
    {
        public string gameVersion = Application.version;
        public string sessionId;
        public string startTime;
        public List<EpisodeLog> episodes = new List<EpisodeLog>();
        public List<QTableSnapshot> qtableSnapshots = new List<QTableSnapshot>();
    }

    // ===================== PRIVATE STATE =====================
    private EvaluationData _data = new EvaluationData();
    private int _currentEpisode = 0;
    private Dictionary<string, float[]> _previousQTable = new Dictionary<string, float[]>();
    private string _savePath;

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
        _savePath = Path.Combine(Application.persistentDataPath, $"{logFileName}_{_data.sessionId}.json");

        Debug.Log($"[QTableLogger] Save path: {_savePath}");
    }

    // ===================== PUBLIC API =====================

    /// <summary>
    /// Panggil di akhir setiap episode dari DDA/Battle system kamu.
    /// </summary>
    /// <param name="totalReward">Total reward yang didapat di episode ini</param>
    /// <param name="episodeLength">Jumlah langkah/turn di episode ini</param>
    /// <param name="playerHPRatio">HP player akhir dibagi HP maksimum (0.0 - 1.0)</param>
    /// <param name="difficultyLevel">Level kesulitan saat ini sebagai string</param>
    public void LogEpisode(float totalReward, int episodeLength, float playerHPRatio = 0f, string difficultyLevel = "Normal")
    {
        _currentEpisode++;

        var log = new EpisodeLog
        {
            episode = _currentEpisode,
            totalReward = totalReward,
            episodeLength = episodeLength,
            playerHPRatio = playerHPRatio,
            difficulty = difficultyLevel,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _data.episodes.Add(log);

        // Otomatis ambil snapshot Q-table setiap N episode
        if (_currentEpisode % snapshotInterval == 0)
        {
            Debug.Log($"[QTableLogger] Episode {_currentEpisode} - Reward: {totalReward:F3}");
        }
    }

    /// <summary>
    /// Panggil setiap N episode dari DDA agent kamu, dengan Q-table saat ini.
    /// qTable: Dictionary state_string -> float array of Q values per action
    /// </summary>
    public void LogQTableSnapshot(Dictionary<State, float[]> currentQTable)
    {
        float maxDelta = 0f;
        float sumAbsQ = 0f;
        float sumSqQ = 0f;
        int totalEntries = 0;

        var snapshot = new QTableSnapshot
        {
            episode = _currentEpisode
        };

        foreach (var kvp in currentQTable)
        {
            string stateKey = kvp.Key.ToString();
            float[] qValues = kvp.Value;

            // Hitung delta dari snapshot sebelumnya (convergence check)
            if (_previousQTable.TryGetValue(stateKey, out float[] prevValues))
            {
                for (int i = 0; i < qValues.Length; i++)
                {
                    float delta = Mathf.Abs(qValues[i] - prevValues[i]);
                    maxDelta = Mathf.Max(maxDelta, delta);
                }
            }

            // Statistik Q values
            int bestAction = 0;
            float bestQ = float.NegativeInfinity;
            for (int i = 0; i < qValues.Length; i++)
            {
                float q = qValues[i];
                sumAbsQ += Mathf.Abs(q);
                sumSqQ += q * q;
                totalEntries++;

                if (q > bestQ)
                {
                    bestQ = q;
                    bestAction = i;
                }
            }

            snapshot.states.Add(new QStateEntry
            {
                state = stateKey,
                qValues = new List<float>(qValues),
                bestAction = bestAction
            });
        }

        snapshot.maxDeltaQ = maxDelta;
        snapshot.meanAbsQ = totalEntries > 0 ? sumAbsQ / totalEntries : 0f;
        snapshot.stdQ = totalEntries > 0
            ? Mathf.Sqrt(sumSqQ / totalEntries - (snapshot.meanAbsQ * snapshot.meanAbsQ))
            : 0f;

        _data.qtableSnapshots.Add(snapshot);

        // Simpan Q-table saat ini sebagai "previous" untuk perbandingan berikutnya
        _previousQTable.Clear();
        foreach (var kvp in currentQTable)
        {
            _previousQTable[kvp.Key.ToString()] = (float[])kvp.Value.Clone();
        }

        Debug.Log($"[QTableLogger] Q-Table Snapshot ep{_currentEpisode} | MaxDelta: {maxDelta:F5} | MeanAbsQ: {snapshot.meanAbsQ:F3}");
    }

    /// <summary>
    /// Simpan semua data ke file JSON. Bisa dipanggil kapan saja.
    /// </summary>
    public void SaveToFile()
    {
        string json = JsonUtility.ToJson(_data, prettyPrint: true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"[QTableLogger] Data saved: {_data.episodes.Count} episodes, {_data.qtableSnapshots.Count} snapshots -> {_savePath}");
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit)
            SaveToFile();
    }

    // ===================== HELPER: Path Info =====================
    public string GetSavePath() => _savePath;
}
