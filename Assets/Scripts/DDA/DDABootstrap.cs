using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Actuators; // ActionSpec

namespace DDA
{
    /// Creates DontDestroyOnLoad GameObjects:
    ///  1. "DDA Agent"       — BehaviorParameters + DDAAgent (no DecisionRequester — explicit decisions only)
    ///  2. "DDA Integration" — DDAIntegration (bridge to BattleSystem)
    ///  3. "DDA Applier"     — DifficultyApplier (applies multipliers to enemies)
    ///  4. "DDA Debug Panel" — right-side overlay for screen recording (optional)
    /// </summary>
    public class DDABootstrap : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("Path under Resources/ to the .onnx model (no extension).")]
        [SerializeField] private string _modelResourcePath = "DDA/Models/ddqn_dda5";

        [Header("Configuration")]
        [Tooltip("Enable DDA at startup.")]
        [SerializeField] private bool _enableDDA = true;

        [Header("Debug / Recording")]
        [Tooltip("Show real-time DDA overlay on screen (toggle with F9).")]
        [SerializeField] private bool _enableDebugPanel = true;

        private void Awake()
        {
            if (DDAIntegration.Instance != null)
            {
                Debug.Log("[DDABootstrap] DDAIntegration already exists — skipping bootstrap.");
                return;
            }
            if (FindObjectOfType<DDAAgent>() != null)
            {
                Debug.Log("[DDABootstrap] DDAAgent already exists in scene — deferring to hand-placed setup.");
                return;
            }

            if (!_enableDDA)
            {
                Debug.Log("[DDABootstrap] DDA disabled via inspector.");
                return;
            }

            Bootstrap();
        }

        private void Bootstrap()
        {
            // 1. Load difficulty settings and create a RUNTIME COPY.
            DifficultySettings settingsAsset = Resources.Load<DifficultySettings>("DDA/DefaultDifficultySettings");
            if (settingsAsset == null)
            {
                Debug.LogError("[DDABootstrap] DefaultDifficultySettings not found in Resources/DDA/. DDA will not work.");
                return;
            }
            DifficultySettings runtimeSettings = settingsAsset.CreateRuntimeCopy();

            // 2. Load ONNX model (ModelAsset from Unity.InferenceEngine / Sentis)
            ModelAsset model = Resources.Load<ModelAsset>(_modelResourcePath);
            if (model == null)
            {
                Debug.LogWarning($"[DDABootstrap] Model not found at Resources/{_modelResourcePath}. " +
                                 "Agent will use HeuristicOnly mode.");
            }

            // --- Create DDA Agent ---
            var agentObj = new GameObject("DDA Agent");
            DontDestroyOnLoad(agentObj);

            // 1st: BehaviorParameters (needed by agent's Initialize/OnEnable)
            var behaviorParams = agentObj.AddComponent<BehaviorParameters>();
            behaviorParams.BehaviorName = "ddqn_dda";
            behaviorParams.BrainParameters.VectorObservationSize = 6;
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5);

            if (model != null)
            {
                behaviorParams.Model = model;
                behaviorParams.BehaviorType = BehaviorType.InferenceOnly;
                Debug.Log($"[DDABootstrap] Model set: {model.name}, BehaviorType=InferenceOnly");
            }
            else
            {
                behaviorParams.BehaviorType = BehaviorType.HeuristicOnly;
                Debug.LogWarning("[DDABootstrap] No model found! BehaviorType=HeuristicOnly (will always pick Normal)");
            }

            // 2nd: DDAAgent (triggers OnEnable → Initialize — BehaviorParameters already exists)
            var agent = agentObj.AddComponent<DDAAgent>();
            agent.SetTrainingMode(false);
            agent.SetDifficultySettings(runtimeSettings);

            // --- Create DDA Integration ---
            var integrationObj = new GameObject("DDA Integration");
            DontDestroyOnLoad(integrationObj);
            var integration = integrationObj.AddComponent<DDAIntegration>();
            integration.SetDifficultySettings(runtimeSettings);

            // --- Create Difficulty Applier ---
            var applierObj = new GameObject("DDA Applier");
            DontDestroyOnLoad(applierObj);
            var applier = applierObj.AddComponent<DifficultyApplier>();
            applier.SetDifficultySettings(runtimeSettings);

            // --- Create Debug Panel (right-side overlay) ---
            if (_enableDebugPanel)
            {
                var debugPanelObj = new GameObject("DDA Debug Panel");
                DontDestroyOnLoad(debugPanelObj);
                debugPanelObj.AddComponent<DDADebugPanel>();
            }

            Debug.Log("[DDABootstrap] DDA system initialized for inference mode.");
            Debug.Log($"  Model: {(model != null ? model.name : "NONE — HeuristicOnly")}");
            Debug.Log($"  Behavior: ddqn_dda, Discrete(5)");
            Debug.Log($"  Difficulty: {runtimeSettings.GetLevelName()}");
            Debug.Log($"  Debug Panel: {_enableDebugPanel}");
        }
    }
}
