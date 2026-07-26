using System.Collections.Generic;
using DDA;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime debug overlay that displays DDA agent actions in real-time.
/// Shows current difficulty + decision log (difficulty changes).
/// Toggle with F9.
/// </summary>
public class DDADebugPanel : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float _panelWidth = 350f;
    [SerializeField] private int _fontSize = 14;
    [SerializeField] private int _maxLogEntries = 20;

    [Header("Toggle")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F9;

    private Canvas _canvas;
    private TMP_Text _headerText;
    private TMP_Text _logText;
    private TMP_Text _observationsText;
    private TMP_Text _lastDecisionText;
    private TMP_Text _statusText;
    private GameObject _panelRoot;

    private DDAIntegration _integration;
    private DDAAgent _agent;

    private readonly List<string> _logEntries = new List<string>();
    private bool _visible = true;

    // Rate-limit for retrying agent/integration resolution so Update() doesn't spam FindObjectOfType.
    private float _lastResolveAttempt = -999f;
    private const float ResolveRetryInterval = 1f;

    // Timestamp of the last received OnAgentDecision event (for staleness display).
    private string _lastDecisionTimestamp = "--";

    private void Start()
    {
        BuildUI();
        Invoke(nameof(ResolveReferences), 0.1f);
    }

    private void ResolveReferences()
    {
        _lastResolveAttempt = Time.unscaledTime;

        if (_integration == null)
            _integration = DDAIntegration.Instance;

        if (_agent == null)
        {
            var agent = FindObjectOfType<DDAAgent>();
            if (agent != null)
            {
                _agent = agent;
                _agent.OnDifficultyChanged += OnDifficultyChanged;
                _agent.OnAgentDecision += OnAgentDecision;
                AddLog("DDA Agent connected.");
            }
            // If still null, Update() will retry after ResolveRetryInterval.
        }
    }

    private void OnDestroy()
    {
        if (_agent != null)
        {
            _agent.OnDifficultyChanged -= OnDifficultyChanged;
            _agent.OnAgentDecision -= OnAgentDecision;
        }
        else
        {
            var agent = FindObjectOfType<DDAAgent>();
            if (agent != null)
                agent.OnDifficultyChanged -= OnDifficultyChanged;
        }
    }

    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(_toggleKey))
        {
            _visible = !_visible;
            if (_panelRoot != null) _panelRoot.SetActive(_visible);
        }

        // Retry resolving agent/integration references if not yet connected.
        // Uses a rate-limited retry (every ResolveRetryInterval seconds) instead of
        // spamming FindObjectOfType every frame — safe for DontDestroyOnLoad objects
        // that may not be available in the first few frames after scene load.
        if ((_agent == null || _integration == null) &&
            Time.unscaledTime - _lastResolveAttempt >= ResolveRetryInterval)
        {
            ResolveReferences();
        }

        if (_visible)
            RefreshDisplay();
    }

    // ----------------------------------------------------------------
    // UI Construction
    // ----------------------------------------------------------------

    private void BuildUI()
    {
        var canvasObj = new GameObject("DDA Debug Canvas");
        canvasObj.transform.SetParent(transform, false);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        _panelRoot = new GameObject("DDA Debug Panel");
        _panelRoot.transform.SetParent(canvasObj.transform, false);

        var panelRect = _panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(_panelWidth, 0f);

        var panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _headerText = CreateLabel("DDA DEBUG", _fontSize + 4, Color.white, FontStyles.Bold);

        CreateSeparator();

        // Status line (shows "Connecting..." until agent found)
        _statusText = CreateLabel("<color=#FFA726>Connecting to DDA Agent...</color>",
            _fontSize - 2, new Color(0.8f, 0.65f, 0.2f), FontStyles.Normal);

        CreateSeparator();

        CreateLabel("Last Decision:", _fontSize, new Color(0.4f, 0.9f, 1f), FontStyles.Bold);
        _lastDecisionText = CreateLabel("Waiting for first decision...", _fontSize - 1,
            new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);

        CreateSeparator();

        CreateLabel("Observations:", _fontSize, new Color(0.4f, 0.9f, 1f), FontStyles.Bold);
        _observationsText = CreateLabel("--", _fontSize - 1, new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);

        CreateSeparator();

        CreateLabel("Decision Log:", _fontSize, Color.yellow, FontStyles.Bold);
        _logText = CreateLabel("--", _fontSize - 1, new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);
    }

    private TMP_Text CreateLabel(string text, int fontSize, Color color, FontStyles style)
    {
        var obj = new GameObject("Label");
        obj.transform.SetParent(_panelRoot.transform, false);

        var rect = obj.AddComponent<RectTransform>();
        var fitter = obj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;
        tmp.lineSpacing = -5f;

        return tmp;
    }

    private void CreateSeparator()
    {
        var obj = new GameObject("Separator");
        obj.transform.SetParent(_panelRoot.transform, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 2f);

        var fitter = obj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;

        var img = obj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.2f);

        var le = obj.AddComponent<LayoutElement>();
        le.minHeight = 2f;
        le.preferredHeight = 2f;
    }

    // ----------------------------------------------------------------
    // Display Refresh
    // ----------------------------------------------------------------

    private void RefreshDisplay()
    {
        if (_integration == null)
            _integration = DDAIntegration.Instance;

        if (_integration == null)
        {
            _headerText.text = "<b>DDA DEBUG</b> (F9)\n<color=#F44336>Integration not found</color>";
            if (_statusText != null)
                _statusText.text = "<color=#F44336>● DDAIntegration missing</color>";
            return;
        }

        // Update status line
        if (_statusText != null)
        {
            if (_agent != null)
                _statusText.text = $"<color=#4CAF50>● Agent connected</color>  " +
                                   $"<color=#666>last decision: {_lastDecisionTimestamp}</color>";
            else
                _statusText.text = "<color=#FFA726>● Agent not found — retrying...</color>";
        }

        string diffName = _integration.GetCurrentDifficultyName();
        var (hpMult, dmgMult) = _integration.GetCurrentMultipliers();
        string diffColor = DifficultyColor(diffName);

        int areaIdx = MapSystem.Instance != null ? MapSystem.Instance.AreaIndex : 0;
        int areaTotal = MapSystem.Instance != null ? MapSystem.Instance.AreaTotal : 12;

        _headerText.text =
            $"<b>DDA DEBUG</b> (F9 toggle)\n" +
            $"Area: <b>{areaIdx}/{areaTotal}</b>   " +
            $"Difficulty: <color={diffColor}><b>{diffName}</b></color>\n" +
            $"HP x{hpMult:F2}  |  DMG x{dmgMult:F2}";

        // Live observations — what the agent is "seeing" right now (matches CollectObservations).
        if (_observationsText != null)
        {
            if (_agent != null)
            {
                float dmgRatio = _agent.GetDamageDealtRatio();
                int dmgRaw = _agent.GetDamageDealtRaw();
                int enemyHPTotal = _agent.GetAreaTotalEnemyHP();

                _observationsText.text =
                    $"HP Ratio     {_agent.GetHpRatio():F2}\n" +
                    $"Turns (nrm)  {_agent.GetTurnCountNormalized():F2}  ({_agent.GetTurnCount()} turns)\n" +
                    $"Player Lvl   {_agent.GetPlayerLevelNormalized():F2}\n" +
                    $"Dmg Dealt    {dmgRatio:F2}  ({dmgRaw}/{enemyHPTotal} hp)\n" +
                    $"QTE Acc      {_agent.GetQTEAccuracy():F2}  ({_agent.GetSuccessfulQTE()}/{_agent.GetTotalQTEOpportunities()})\n" +
                    $"Res Depl     {_agent.GetResourceDepletion():F2}";
            }
            else
            {
                _observationsText.text = "<color=#FFA726>Agent not connected yet</color>";
            }
        }

        if (_logEntries.Count > 0)
            _logText.text = string.Join("\n", _logEntries);
    }

    // ----------------------------------------------------------------
    // Events
    // ----------------------------------------------------------------

    private void OnDifficultyChanged(int newLevel)
    {
        string diffName = _integration != null ? _integration.GetCurrentDifficultyName() : $"Level {newLevel}";
        string color = DifficultyColor(diffName);
        AddLog($"<color={color}><b>→ {diffName}</b></color>");
    }

    /// <summary>
    /// Fired for EVERY agent decision (kept or changed). Shows the chosen action, the
    /// prev→new transition, and the observation snapshot at decision time — so the real
    /// game displays what the agent decided, not just the resulting difficulty change.
    /// </summary>
    private void OnAgentDecision(AgentDecisionInfo info)
    {
        _lastDecisionTimestamp = System.DateTime.Now.ToString("HH:mm:ss");

        string prevColor = DifficultyColor(info.prevLevelName);
        string newColor = DifficultyColor(info.newLevelName);

        // Update the always-visible "Last Decision" block.
        if (_lastDecisionText != null)
        {
            string arrow = info.changed ? "→" : "=";
            string keptTag = info.changed ? "" : "  <color=#888>(kept)</color>";
            _lastDecisionText.text =
                $"<color=#90CAF9>Area {info.areaIndex}</color>  " +
                $"action <b>{info.action}</b>\n" +
                $"<color={prevColor}>{info.prevLevelName}</color> {arrow} " +
                $"<color={newColor}><b>{info.newLevelName}</b></color>{keptTag}\n" +
                $"<size={_fontSize - 2}><color=#888>" +
                $"HP {info.hpRatio:F2}  Turn {info.turnCountNorm:F2}  Lvl {info.playerLevelNorm:F2}\n" +
                $"Dmg {info.damageDealtRatio:F2}  QTE {info.qteAccuracy:F2}  Res {info.resourceDepletion:F2}" +
                $"</color></size>";
        }

        // Also append a compact line to the scrollable decision log.
        string result = info.changed
            ? $"<color={prevColor}>{info.prevLevelName}</color>→<color={newColor}><b>{info.newLevelName}</b></color>"
            : $"<color=#888>stays {info.newLevelName}</color>";
        AddLog($"Area {info.areaIndex} a={info.action} {result}");
    }

    public void OnBattleEnd(bool won, int endHP, int startHP)
    {
        string result = won ? "<color=#4CAF50>WIN</color>" : "<color=#F44336>LOSS</color>";
        AddLog($"Battle {result}  HP {endHP}/{startHP}");
    }

    public void OnAreaEnter(int areaIndex, string areaType)
    {
        AddLog($"<color=#90CAF9>Area {areaIndex}: {areaType}</color>");
    }

    private void AddLog(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        _logEntries.Add($"<color=#666>{timestamp}</color> {message}");

        while (_logEntries.Count > _maxLogEntries)
            _logEntries.RemoveAt(0);

        if (_logText != null)
            _logText.text = string.Join("\n", _logEntries);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string DifficultyColor(string diffName)
    {
        return diffName switch
        {
            "Very Easy" => "#81C784",
            "Easy"      => "#AED581",
            "Normal"    => "#FFD54F",
            "Hard"      => "#FF8A65",
            "Very Hard" => "#E57373",
            _           => "#FFFFFF"
        };
    }
}
