using UnityEngine;
using UnityEditor;
using DDA;

namespace DDA.Editor
{
    /// <summary>
    /// Editor utilities for setting up DDA training environment.
    /// </summary>
    public static class DDATrainingSetup
    {
        private const string SETTINGS_PATH = "Assets/Resources/DDA/DefaultDifficultySettings.asset";

        [MenuItem("DDA/Setup Training Scene", false, 1)]
        public static void SetupTrainingScene()
        {
            // Create DifficultySettings asset if not exists
            EnsureDifficultySettings();

            // Find or create simulator
            TrainingBattleSimulator simulator = Object.FindObjectOfType<TrainingBattleSimulator>();
            if (simulator == null)
            {
                GameObject simObj = new GameObject("TrainingBattleSimulator");
                simulator = simObj.AddComponent<TrainingBattleSimulator>();
                Debug.Log("[DDA Setup] Created TrainingBattleSimulator");
            }

            // Find or create DDAAgent
            DDAAgent agent = Object.FindObjectOfType<DDAAgent>();
            if (agent == null)
            {
                GameObject agentObj = new GameObject("DDAAgent");
                agent = agentObj.AddComponent<DDAAgent>();
                Debug.Log("[DDA Setup] Created DDAAgent");
            }

            // Find or create DifficultyApplier
            DifficultyApplier applier = DifficultyApplier.Instance;
            if (applier == null)
            {
                GameObject applierObj = new GameObject("DifficultyApplier");
                applier = applierObj.AddComponent<DifficultyApplier>();
                Debug.Log("[DDA Setup] Created DifficultyApplier");
            }

            // Load and assign settings
            DifficultySettings settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>(SETTINGS_PATH);
            if (settings != null)
            {
                // Use reflection to set private fields
                System.Reflection.FieldInfo settingsField = typeof(TrainingBattleSimulator)
                    .GetField("_difficultySettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                settingsField?.SetValue(simulator, settings);

                System.Reflection.FieldInfo applierSettingsField = typeof(DifficultyApplier)
                    .GetField("_difficultySettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                applierSettingsField?.SetValue(applier, settings);

                Debug.Log("[DDA Setup] Assigned DifficultySettings to components");
            }

            Debug.Log("[DDA Setup] Training scene setup complete!");
            Debug.Log("Next steps:\n" +
                      "1. Add TrainingUIDisplay component and assign UI references\n" +
                      "2. Add Behavior Parameters to DDAAgent GameObject (Behavior Name: ddqn_dda)\n" +
                      "3. Press Play to start training simulation");
        }

        [MenuItem("DDA/Create DifficultySettings Asset", false, 2)]
        public static void EnsureDifficultySettings()
        {
            // Check if asset exists
            DifficultySettings settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>(SETTINGS_PATH);

            if (settings == null)
            {
                // Create directory
                string folderPath = "Assets/Resources/DDA";
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder("Assets/Resources", "DDA");
                    Debug.Log("[DDA Setup] Created Assets/Resources/DDA folder");
                }

                // Create asset
                settings = ScriptableObject.CreateInstance<DifficultySettings>();
                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();

                Debug.Log($"[DDA Setup] Created DifficultySettings at {SETTINGS_PATH}");
            }
            else
            {
                Debug.Log($"[DDA Setup] DifficultySettings already exists at {SETTINGS_PATH}");
            }

            Selection.activeObject = settings;
        }

        [MenuItem("DDA/Create Training UI Prefab", false, 3)]
        public static void CreateTrainingUIPrefab()
        {
            // Create Canvas if needed
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Training UI Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Create main panel
            GameObject panel = new GameObject("Training Panel");
            panel.transform.SetParent(canvas.transform, false);

            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0.35f, 1);
            panelRect.offsetMin = new Vector2(10, 10);
            panelRect.offsetMax = new Vector2(-10, -10);

            var panelImage = panel.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            Debug.Log("[DDA Setup] Created Training UI Panel");
            Debug.Log("Next: Add TextMeshProUGUI components for:\n" +
                      "- Episode count\n" +
                      "- Difficulty\n" +
                      "- Win Rate\n" +
                      "- Reward\n" +
                      "- Battle Status\n" +
                      "- Player/Enemy HP Sliders\n" +
                      "- Stats summary");
        }

        [MenuItem("DDA/Validate Setup", false, 100)]
        public static void ValidateSetup()
        {
            bool allValid = true;

            // Check DifficultySettings
            DifficultySettings settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>(SETTINGS_PATH);
            if (settings == null)
            {
                Debug.LogError("[DDA Validation] Missing DifficultySettings asset!");
                allValid = false;
            }
            else
            {
                Debug.Log("[DDA Validation] ✓ DifficultySettings found");
            }

            // Check TrainingBattleSimulator
            TrainingBattleSimulator simulator = Object.FindObjectOfType<TrainingBattleSimulator>();
            if (simulator == null)
            {
                Debug.LogError("[DDA Validation] Missing TrainingBattleSimulator in scene!");
                allValid = false;
            }
            else
            {
                Debug.Log("[DDA Validation] ✓ TrainingBattleSimulator found");
            }

            // Check DDAAgent
            DDAAgent agent = Object.FindObjectOfType<DDAAgent>();
            if (agent == null)
            {
                Debug.LogError("[DDA Validation] Missing DDAAgent in scene!");
                allValid = false;
            }
            else
            {
                Debug.Log("[DDA Validation] ✓ DDAAgent found");

                // Check if Behavior Parameters exist
                var behaviorParams = agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                if (behaviorParams == null)
                {
                    Debug.LogWarning("[DDA Validation] DDAAgent missing BehaviorParameters! Add via ML-Agents component.");
                    allValid = false;
                }
                else
                {
                    Debug.Log($"[DDA Validation] ✓ BehaviorParameters found (Behavior: {behaviorParams.BehaviorName})");
                }
            }

            // Check DifficultyApplier
            DifficultyApplier applier = DifficultyApplier.Instance;
            if (applier == null)
            {
                Debug.LogError("[DDA Validation] Missing DifficultyApplier in scene!");
                allValid = false;
            }
            else
            {
                Debug.Log("[DDA Validation] ✓ DifficultyApplier found");
            }

            if (allValid)
            {
                Debug.Log("[DDA Validation] ✓✓✓ All components valid! Ready for training.");
            }
            else
            {
                Debug.LogError("[DDA Validation] ✗✗✗ Setup incomplete. Run 'DDA > Setup Training Scene' to fix.");
            }
        }
    }
}