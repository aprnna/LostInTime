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
        [SerializeField] private TextMeshProUGUI _enemyText;

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

        [Header("Area Progress")]
        [SerializeField] private TextMeshProUGUI _areaText;
        [SerializeField] private TextMeshProUGUI _areaTypeText;
        [SerializeField] private TextMeshProUGUI _enemyListText;
        [SerializeField] private Slider _areaProgressSlider;

        [Header("Run Stats")]
        [SerializeField] private TextMeshProUGUI _runText;
        [SerializeField] private TextMeshProUGUI _playerHPText;
        [SerializeField] private TextMeshProUGUI _playerStatsText;
        [SerializeField] private TextMeshProUGUI _actionText;

        [Header("Damage Intervals")]
        [SerializeField] private TextMeshProUGUI _playerDamageText;
        [SerializeField] private TextMeshProUGUI _enemyDamageText;

        [Header("Run Result")]
        [SerializeField] private GameObject _runResultPanel;
        [SerializeField] private TextMeshProUGUI _runResultText;
        [SerializeField] private float _runResultDisplayTime = 2f;

        // State
        private int _lastEpisode;
        private int _lastArea = -1;
        private int _lastRun = -1;

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
            // Update HP bars using actual MaxHP from simulator
            if (_simulator != null)
            {
                if (_playerHPSlider != null)
                {
                    int maxHP = _simulator.Player?.MaxHP ?? 100;
                    _playerHPSlider.value = (float)playerHP / maxHP;
                }

                if (_enemyHPSlider != null)
                {
                    int maxEnemyHP = _simulator.CurrentEnemy?.MaxHP ?? 100;
                    _enemyHPSlider.value = maxEnemyHP > 0 ? (float)enemyHP / maxEnemyHP : 0f;
                }
            }

            if (_turnText != null)
            {
                _turnText.text = $"Turn: {turn}";
            }

            // Update enemy progress during battle
            if (_enemyListText != null && _simulator != null)
            {
                int total = _simulator.EnemiesInArea;
                int defeated = _simulator.EnemiesDefeatedInArea;
                if (total > 0)
                {
                    _enemyListText.text = $"Enemies ({defeated}/{total}): {_simulator.CurrentAreaEnemyList}";
                }
            }

            if (_enemyText != null && _simulator != null)
            {
                _enemyText.text = $"Fighting: {_simulator.CurrentEnemyName}";
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

            UpdateUI();
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
                    _flowStateText.text = "FLOW STATE";
                    _flowStateText.color = Color.cyan;
                }
                else if (winRate > 0.65f)
                {
                    _flowStateText.text = "Too Easy";
                    _flowStateText.color = _easyColor;
                }
                else
                {
                    _flowStateText.text = "Too Hard";
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

            // Update area type display
            if (_areaTypeText != null)
            {
                string areaTypeStr = GetAreaTypeString(_simulator.CurrentAreaType);
                _areaTypeText.text = $"Type: {areaTypeStr}";
                _areaTypeText.color = GetAreaTypeColor(_simulator.CurrentAreaType);
            }

            // Update enemy list display
            if (_enemyListText != null)
            {
                int total = _simulator.EnemiesInArea;
                int defeated = _simulator.EnemiesDefeatedInArea;

                if (total > 0)
                {
                    _enemyListText.text = $"Enemies ({defeated}/{total}): {_simulator.CurrentAreaEnemyList}";
                }
                else
                {
                    _enemyListText.text = _simulator.CurrentAreaType == MapType.Rest ? "Healing..."
                                        : _simulator.CurrentAreaType == MapType.Shop ? "Shopping..."
                                        : "No enemies";
                }
            }

            if (_enemyText != null)
            {
                _enemyText.text = $"Current: {_simulator.CurrentEnemyName}";
            }
        }

        private string GetAreaTypeString(MapType type)
        {
            return type switch
            {
                MapType.Enemy => "Enemy",
                MapType.Boss => "BOSS",
                MapType.Rest => "Rest",
                MapType.Shop => "Shop",
                _ => type.ToString()
            };
        }

        private Color GetAreaTypeColor(MapType type)
        {
            return type switch
            {
                MapType.Enemy => _normalColor,
                MapType.Boss => _hardColor,
                MapType.Rest => _easyColor,
                MapType.Shop => Color.cyan,
                _ => Color.white
            };
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

            // Player stats (live update)
            var player = _simulator.Player;
            if (_playerHPText != null && player != null)
            {
                _playerHPText.text = $"HP: {player.CurrentHP}/{player.MaxHP}";
            }

            if (_playerStatsText != null && player != null)
            {
                _playerStatsText.text = $"Lv.{player.Level} | Coin: {player.Coin}";
            }

            if (_actionText != null && player != null)
            {
                _actionText.text = $"Sword: {player.SwordUses}/{player.MaxSwordUses} | Gun: {player.GunUses}/{player.MaxGunUses} | Def: {player.DefendUses}/{player.MaxDefendUses}";
            }

            // Damage intervals
            if (_playerDamageText != null && player != null)
            {
                int punchMin = Mathf.RoundToInt(player.BaseDamage * player.PunchPercentage / 100f) - player.PunchInterval;
                int punchMax = Mathf.RoundToInt(player.BaseDamage * player.PunchPercentage / 100f) + player.PunchInterval;
                int swordMin = Mathf.RoundToInt(player.BaseDamage * player.SwordPercentage / 100f) - player.SwordInterval;
                int swordMax = Mathf.RoundToInt(player.BaseDamage * player.SwordPercentage / 100f) + player.SwordInterval;
                int gunMin = Mathf.RoundToInt(player.BaseDamage * player.GunPercentage / 100f) - player.GunInterval;
                int gunMax = Mathf.RoundToInt(player.BaseDamage * player.GunPercentage / 100f) + player.GunInterval;

                _playerDamageText.text = $"Player DMG:\n" +
                    $"Punch: {punchMin}-{punchMax}\n" +
                    $"Sword: {swordMin}-{swordMax}\n" +
                    $"Gun: {gunMin}-{gunMax}";
            }

            // Enemy damage interval
            if (_enemyDamageText != null)
            {
                var enemy = _simulator.CurrentEnemy;
                if (enemy != null)
                {
                    int enemyMin = enemy.GetMinDamage();
                    int enemyMax = enemy.GetMaxDamage();
                    _enemyDamageText.text = $"Enemy DMG: {enemyMin}-{enemyMax}";
                }
                else
                {
                    _enemyDamageText.text = "Enemy DMG: -";
                }
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
            UpdateUI();
        }
    }
}