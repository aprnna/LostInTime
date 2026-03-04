using System.Collections.Generic;
using DDA;
using UnityEngine;

/// <summary>
/// CONTOH INTEGRASI QTableLogger ke DDA Agent kamu.
/// 
/// Ini bukan script lengkap — ini menunjukkan di mana dan bagaimana
/// memanggil QTableLogger dari DDA Q-Learning agent yang sudah ada.
/// Sesuaikan nama variabel dengan implementasi DDA-mu.
/// </summary>
public class DDAAgent_LoggerIntegration : MonoBehaviour
{
    // === Contoh variabel Q-table (sesuaikan dengan milikmu) ===
    // Biasanya berupa: Dictionary<StateKey, float[]> atau float[,]
    private Dictionary<State, float[]> _qTable = new Dictionary<State, float[]>();

    [Header("Logging")]
    [SerializeField] private int _snapshotEveryNEpisodes = 10;

    // Variabel tracking episode
    private float _episodeTotalReward = 0f;
    private int _episodeSteps = 0;
    private int _episodeCount = 0;

    // ============================================================
    // PANGGIL INI setiap kali reward diterima (setiap langkah/turn)
    // ============================================================
    public void OnRewardReceived(float reward)
    {
        _episodeTotalReward += reward;
        _episodeSteps++;
    }

    // ============================================================
    // PANGGIL INI di akhir setiap episode (saat battle selesai,
    // player menang/kalah, atau game over)
    // ============================================================
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

    // ============================================================
    // HELPER: Convert Q-table float[,] ke Dictionary (jika Q-table
    // kamu berupa 2D array)
    // stateNames: list nama state, actionCount: jumlah action
    // ============================================================
    private Dictionary<string, float[]> ConvertQTableToDict(float[,] qArray, string[] stateNames, int actionCount)
    {
        var dict = new Dictionary<string, float[]>();
        for (int s = 0; s < stateNames.Length; s++)
        {
            float[] actions = new float[actionCount];
            for (int a = 0; a < actionCount; a++)
                actions[a] = qArray[s, a];
            dict[stateNames[s]] = actions;
        }
        return dict;
    }

    // ============================================================
    // Tombol manual save (opsional, bisa dipanggil dari UI)
    // ============================================================
    public void ManualSave()
    {
        QTableLogger.Instance.SaveToFile();
        Debug.Log("[DDAAgent] Manual save triggered. File: " + QTableLogger.Instance.GetSavePath());
    }
}
