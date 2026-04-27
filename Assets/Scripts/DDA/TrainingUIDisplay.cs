using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DDA
{
    /// <summary>
    /// UI display for training mode.
    /// Shows real-time learning progress, episode stats, difficulty changes.
    /// </summary>
    public class TrainingUIDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TrainingBattleSimulator _simulator;
        [SerializeField] private DDAAgent _agent;

        [Header("Episode Display")]
        [SerializeField] private TextMeshProUGUI _episodeText;
        [SerializeField] private TextMeshProUGUI _difficultyText;
        [SerializeField] private TextMeshProUGUI _winRateText;
        [SerializeField] private TextMeshProUGUI _rewardText;

        [Header("Battle Status")]
        [SerializeField] private Slider _playerHPSlider;
        [SerializeField] private Slider _enemyHPSlider;
        [SerializeField] private TextMeshProUGUI _turnText;
        [SerializeField] private TextMeshProUGUI _battleStatusText;

        [Header("Statistics")]
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private TextMeshProUGUI _flowStateText;

        [Header("Visual Indicators")]
        [SerializeField] private Image _difficultyIndicator;
        [SerializeField] private Color _easyColor = Color.green;
        [SerializeField] private Color _normalColor = Color.yellow;
        [SerializeField] private Color _hardColor = Color.red;
        [SerializeField] private GameObject _winFlash;
        [SerializeField] private GameObject _lossFlash;
        [SerializeField] private float _flashDuration = 0.3f;

        [Header("Learning Graph")]
        [SerializeField] private bool _showGraph = true;
        [SerializeField] private int _graphHistorySize = 100;
        [SerializeField] private RectTransform _graphContainer;
        [SerializeField] private GameObject _graphBarPrefab;

        [Header("Area Progress")]
        [SerializeField] private TextMeshProUGUI _areaText;
        [SerializeField] private TextMeshProUGUI _enemyText;
        [SerializeField] private Slider _areaProgressSlider;

        [Header("Run Stats")]
        [SerializeField] private TextMeshProUGUI _runText;
        [SerializeField] private TextMeshProUGUI _playerHPText;
        [SerializeField] private TextMeshProUGUI _playerStatsText;
        [SerializeField] private TextMeshProUGUI _actionText;

        [Header("Run Result")]
        [SerializeField] private GameObject _runResultPanel;
        [SerializeField] private TextMeshProUGUI _runResultText;
        [SerializeField] private float _runResultDisplayTime = 2f;

        // State
        private int _lastEpisode;
        private int _lastArea = -1;
        private int _lastRun = -1;
        private float[] _rewardHistory;
        private int _historyIndex;
        private float _maxReward = 2f;
        private float _minReward = -2f;

        private void Start()
        {
            if (_simulator == null)
            {
                _simulator = TrainingBattleSimulator.Instance;
            }

            if (_agent == null)
            {
                _agent = FindObjectOfType<DDAAgent>();
            }

            // Initialize history
            _rewardHistory = new float[_graphHistorySize];

            // Subscribe to events
            if (_simulator != null)
            {
                _simulator.OnBattleStateChanged += UpdateBattleState;
                _simulator.OnBattleEnded += HandleBattleEnd;
                _simulator.OnDifficultyChanged += HandleDifficultyChange;
                _simulator.OnStatsUpdated += UpdateStats;
                _simulator.OnAreaChanged += HandleAreaChange;
                _simulator.OnRunComplete += HandleRunComplete;
            }

            // Initialize UI
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (_simulator != null)
            {
                _simulator.OnBattleStateChanged -= UpdateBattleState;
                _simulator.OnBattleEnded -= HandleBattleEnd;
                _simulator.OnDifficultyChanged -= HandleDifficultyChange;
                _simulator.OnStatsUpdated -= UpdateStats;
                _simulator.OnAreaChanged -= HandleAreaChange;
                _simulator.OnRunComplete -= HandleRunComplete;
            }
        }

        private void Update()
        {
            if (_simulator == null) return;

            // Update episode count if changed
            if (_simulator.EpisodeCount != _lastEpisode)
            {
                _lastEpisode = _simulator.EpisodeCount;
                UpdateUI();
            }
        }

        private void UpdateBattleState(int playerHP, int enemyHP, int turn)
        {
            // Update HP bars
            if (_playerHPSlider != null)
            {
                _playerHPSlider.value = playerHP / 100f; // Assuming 100 base HP
            }

            if (_enemyHPSlider != null)
            {
                float maxEnemyHP = enemyHP > _enemyHPSlider.value * 80f ? enemyHP : 80f;
                _enemyHPSlider.value = enemyHP / maxEnemyHP;
            }

            if (_turnText != null)
            {
                _turnText.text = $"Turn: {turn}";
            }
        }

        private void HandleBattleEnd(bool won, float reward, int episode)
        {
            // Flash effect
            if (won && _winFlash != null)
            {
                StartCoroutine(FlashEffect(_winFlash));
                if (_battleStatusText != null)
                {
                    _battleStatusText.text = "WIN!";
                    _battleStatusText.color = _easyColor;
                }
            }
            else if (!won && _lossFlash != null)
            {
                StartCoroutine(FlashEffect(_lossFlash));
                if (_battleStatusText != null)
                {
                    _battleStatusText.text = "LOSS";
                    _battleStatusText.color = _hardColor;
                }
            }

            // Add to history
            _rewardHistory[_historyIndex] = reward;
            _historyIndex = (_historyIndex + 1) % _graphHistorySize;

            // Update max/min for graph scaling
            if (reward > _maxReward) _maxReward = reward;
            if (reward < _minReward) _minReward = reward;

            UpdateUI();
            UpdateGraph();
        }

        private void HandleDifficultyChange(int newLevel)
        {
            UpdateDifficultyIndicator(newLevel);
        }

        private void UpdateStats(TrainingStats stats)
        {
            if (_statsText != null)
            {
                _statsText.text = $"Episodes: {stats.EpisodeCount}\n" +
                                  $"Wins: {stats.WinCount}\n" +
                                  $"Win Rate: {stats.WinRate:P1}\n" +
                                  $"Avg Reward: {stats.AvgReward:F3}\n" +
                                  $"Avg Turns: {stats.AvgTurnsPerBattle:F1}\n" +
                                  $"Streak: +{stats.ConsecutiveWins}/-{stats.ConsecutiveLosses}";
            }

            // Flow state indicator
            if (_flowStateText != null)
            {
                float winRate = stats.WinRate;
                if (winRate >= 0.55f && winRate <= 0.65f)
                {
                    _flowStateText.text = "🎯 FLOW STATE";
                    _flowStateText.color = Color.cyan;
                }
                else if (winRate > 0.65f)
                {
                    _flowStateText.text = "😊 Too Easy";
                    _flowStateText.color = _easyColor;
                }
                else
                {
                    _flowStateText.text = "😰 Too Hard";
                    _flowStateText.color = _hardColor;
                }
            }
        }

        private void HandleAreaChange(int areaIndex, int totalAreas)
        {
            if (_areaText != null)
            {
                _areaText.text = $"Area: {areaIndex}/{totalAreas}";
            }

            if (_areaProgressSlider != null)
            {
                _areaProgressSlider.value = (float)areaIndex / totalAreas;
            }

            if (_enemyText != null)
            {
                _enemyText.text = $"Enemy: {_simulator.CurrentEnemyName}";
            }
        }

        private void HandleRunComplete(RunResult result)
        {
            if (_runText != null)
            {
                _runText.text = $"Run: {result.RunNumber}";
            }

            if (_playerHPText != null)
            {
                _playerHPText.text = $"HP: {result.FinalHP}";
            }

            if (_playerStatsText != null)
            {
                _playerStatsText.text = $"Lv.{result.Level} | Coin: {result.Coin}";
            }

            // Show run result
            if (_runResultPanel != null && _runResultText != null)
            {
                _runResultPanel.SetActive(true);
                string resultStr = result.Won ? "RUN COMPLETE!" : "GAME OVER";
                _runResultText.text = $"{resultStr}\n" +
                                      $"Areas: {result.AreasCompleted}/{result.TotalAreas}\n" +
                                      $"HP: {result.FinalHP} | Lv.{result.Level}";
                _runResultText.color = result.Won ? _easyColor : _hardColor;

                StartCoroutine(HideRunResultAfterDelay(_runResultDisplayTime));
            }

            Debug.Log($"[TrainingUI] Run {result.RunNumber}: Won={result.Won}, " +
                      $"Areas={result.AreasCompleted}/{result.TotalAreas}");
        }

        private System.Collections.IEnumerator HideRunResultAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_runResultPanel != null)
            {
                _runResultPanel.SetActive(false);
            }
        }

        private void UpdateUI()
        {
            if (_simulator == null) return;

            // Episode count
            if (_episodeText != null)
            {
                _episodeText.text = $"Episode: {_simulator.EpisodeCount}";
            }

            // Difficulty
            if (_difficultyText != null)
            {
                _difficultyText.text = $"Difficulty: {_simulator.DifficultyName}";
            }

            // Win rate
            if (_winRateText != null)
            {
                _winRateText.text = $"Win Rate: {_simulator.WinRate:P1}";
            }

            // Reward
            if (_rewardText != null)
            {
                _rewardText.text = $"Reward: {_simulator.LastReward:F3} (Avg: {_simulator.AvgReward:F3})";
            }

            // Difficulty indicator color
            UpdateDifficultyIndicator(_simulator.CurrentDifficulty);

            // Player stats
            var player = _simulator.Player;
            if (_playerHPText != null && player != null)
            {
                _playerHPText.text = $"HP: {player.CurrentHP}/{player.MaxHP}";
            }

            if (_actionText != null && player != null)
            {
                _actionText.text = $"Sword: {player.SwordUses}/{player.MaxSwordUses} | Gun: {player.GunUses}/{player.MaxGunUses} | Def: {player.DefendUses}/{player.MaxDefendUses}";
            }
        }

        private void UpdateDifficultyIndicator(int level)
        {
            if (_difficultyIndicator == null) return;

            // Map level 0-4 to color gradient
            // 0-1: Easy (green)
            // 2: Normal (yellow)
            // 3-4: Hard (red)
            Color color;
            if (level <= 1)
            {
                color = Color.Lerp(_normalColor, _easyColor, (2 - level) / 2f);
            }
            else if (level == 2)
            {
                color = _normalColor;
            }
            else
            {
                color = Color.Lerp(_normalColor, _hardColor, (level - 2) / 2f);
            }

            _difficultyIndicator.color = color;
        }

        private void UpdateGraph()
        {
            if (!_showGraph || _graphContainer == null || _graphBarPrefab == null) return;

            // Clear existing bars
            foreach (Transform child in _graphContainer)
            {
                Destroy(child.gameObject);
            }

            // Create bars for history
            float containerHeight = _graphContainer.rect.height;
            float barWidth = _graphContainer.rect.width / _graphHistorySize;

            for (int i = 0; i < _graphHistorySize; i++)
            {
                int historyIdx = (_historyIndex + i) % _graphHistorySize;
                float reward = _rewardHistory[historyIdx];

                if (reward == 0 && i > 0) continue; // Skip uninitialized

                GameObject bar = Instantiate(_graphBarPrefab, _graphContainer);
                RectTransform rt = bar.GetComponent<RectTransform>();

                // Position
                rt.anchorMin = new Vector2(i / (float)_graphHistorySize, 0);
                rt.anchorMax = new Vector2((i + 1) / (float)_graphHistorySize, 1);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // Height based on reward
                float normalizedReward = (reward - _minReward) / (_maxReward - _minReward);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, containerHeight * normalizedReward);

                // Color based on positive/negative
                Image img = bar.GetComponent<Image>();
                if (img != null)
                {
                    img.color = reward >= 0 ? _easyColor : _hardColor;
                }
            }
        }

        private System.Collections.IEnumerator FlashEffect(GameObject flash)
        {
            flash.SetActive(true);
            yield return new WaitForSeconds(_flashDuration);
            flash.SetActive(false);
        }

        /// <summary>
        /// Resets the display.
        /// </summary>
        public void ResetDisplay()
        {
            _lastEpisode = 0;
            _rewardHistory = new float[_graphHistorySize];
            _historyIndex = 0;
            UpdateUI();
        }
    }
}