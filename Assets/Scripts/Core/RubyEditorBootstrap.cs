using UnityEngine;
using UnityEngine.UIElements;
using RubyEditor.Core;
using RubyEditor.Tools;
using RubyEditor.UI;
using RubyEditor.Data;

namespace RubyEditor.Core
{
    public class RubyEditorBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject editorManagerPrefab;
        [SerializeField] private UIDocument uiDocumentPrefab;

        [Header("Settings")]
        [SerializeField] private bool autoLoadLastZone = true;
        [SerializeField] private bool createDefaultAssets = true;
        [SerializeField] private string defaultZonePath = "Zones/DefaultZone";

        [Header("Camera Settings")]
        [SerializeField] private Vector3 defaultCameraPosition = new Vector3(0, 20, -20);
        [SerializeField] private Vector3 defaultCameraRotation = new Vector3(45, 0, 0);

        void Awake()
        {
            Debug.Log("🚀 Ruby GameEditor wird gestartet...");
            InitializeEditor();
        }

        void InitializeEditor()
        {
            // 1. Create Editor Manager
            GameObject editorGO = CreateEditorManager();

            // 2. Setup UI
            SetupUI();

            // 3. Create default assets if needed
            if (createDefaultAssets)
            {
                CreateDefaultAssets();
            }

            // 4. Setup Camera
            SetupEditorCamera();

            // 5. Load last zone or create new
            if (autoLoadLastZone)
            {
                LoadLastOrDefaultZone();
            }

            // 6. Show welcome message
            ShowWelcomeMessage();

            Debug.Log("✅ Ruby GameEditor erfolgreich initialisiert!");
        }

        GameObject CreateEditorManager()
        {
            GameObject managerGO = null;

            if (editorManagerPrefab != null)
            {
                managerGO = Instantiate(editorManagerPrefab);
            }
            else
            {
                // Create from scratch
                managerGO = new GameObject("RubyEditorManager");

                // Add core components
                managerGO.AddComponent<RubyEditorEnhanced>();
                managerGO.AddComponent<EditorManager>();
                managerGO.AddComponent<GridSystem>();
                managerGO.AddComponent<SelectionSystem>();
                managerGO.AddComponent<ZoneSaveSystem>();

                // Add tools
                managerGO.AddComponent<ObjectPlacementTool>();
                // managerGO.AddComponent<TerrainSculptTool>(); // Removed because TerrainSculptTool is missing
            }

            managerGO.name = "RubyEditorManager";
            return managerGO;
        }

        void SetupUI()
        {
            // Find or create UI Document
            UIDocument uiDocument = FindFirstObjectByType<UIDocument>();

            if (uiDocument == null)
            {
                GameObject uiGO = new GameObject("EditorUI");
                uiDocument = uiGO.AddComponent<UIDocument>();

                // Add UI Manager
                uiGO.AddComponent<EditorUIManager>();
            }

            // Create Canvas for runtime UI if needed
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                CreateRuntimeUI();
            }
        }

        void CreateRuntimeUI()
        {
            // Create Canvas
            GameObject canvasGO = new GameObject("EditorCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create HUD Panel
            CreateHUDPanel(canvasGO.transform);

            // Create Tool Palette
            CreateToolPalette(canvasGO.transform);

            // Create Status Bar
            CreateStatusBar(canvasGO.transform);
        }

        void CreateHUDPanel(Transform parent)
        {
            GameObject hudPanel = new GameObject("HUDPanel");
            hudPanel.transform.SetParent(parent, false);

            RectTransform rect = hudPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(300, 100);

            // Add background
            UnityEngine.UI.Image bg = hudPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // Add info text
            GameObject infoText = new GameObject("InfoText");
            infoText.transform.SetParent(hudPanel.transform, false);

            UnityEngine.UI.Text text = infoText.AddComponent<UnityEngine.UI.Text>();
            text.text = "Ruby Editor v1.0\nMode: Select\nGrid: ON";
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;

            RectTransform textRect = infoText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
        }

        void CreateToolPalette(Transform parent)
        {
            GameObject toolPalette = new GameObject("ToolPalette");
            toolPalette.transform.SetParent(parent, false);

            RectTransform rect = toolPalette.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(60, 0);
            rect.sizeDelta = new Vector2(100, 400);

            // Add background
            UnityEngine.UI.Image bg = toolPalette.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Add Vertical Layout
            UnityEngine.UI.VerticalLayoutGroup layout = toolPalette.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Create tool buttons
            CreateToolButton(toolPalette.transform, "Select", KeyCode.Q, EditorMode.Select);
            CreateToolButton(toolPalette.transform, "Place", KeyCode.W, EditorMode.Place);
            CreateToolButton(toolPalette.transform, "Paint", KeyCode.E, EditorMode.Paint);
            CreateToolButton(toolPalette.transform, "Terrain", KeyCode.R, EditorMode.Terrain);
        }

        void CreateToolButton(Transform parent, string label, KeyCode hotkey, EditorMode mode)
        {
            GameObject buttonGO = new GameObject($"Tool_{label}");
            buttonGO.transform.SetParent(parent, false);

            UnityEngine.UI.Button button = buttonGO.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image buttonImage = buttonGO.AddComponent<UnityEngine.UI.Image>();
            buttonImage.color = new Color(0.3f, 0.3f, 0.3f);

            // Set size
            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90, 40);

            // Add text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);

            UnityEngine.UI.Text text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = $"{label}\n({hotkey})";
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;

            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Add click handler
            button.onClick.AddListener(() =>
            {
                if (RubyEditorEnhanced.Instance != null)
                {
                    RubyEditorEnhanced.Instance.SetMode(mode);
                }
            });
        }

        void CreateStatusBar(Transform parent)
        {
            GameObject statusBar = new GameObject("StatusBar");
            statusBar.transform.SetParent(parent, false);

            RectTransform rect = statusBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(0, 15);
            rect.sizeDelta = new Vector2(0, 30);

            // Add background
            UnityEngine.UI.Image bg = statusBar.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Add status text
            GameObject statusText = new GameObject("StatusText");
            statusText.transform.SetParent(statusBar.transform, false);

            UnityEngine.UI.Text text = statusText.AddComponent<UnityEngine.UI.Text>();
            text.text = "Ready | Grid: ON | Snap: 1.0 | FPS: 60";
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleLeft;

            RectTransform textRect = statusText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
        }

        void SetupEditorCamera()
        {
            // Find or create editor camera
            GameObject camGO = GameObject.Find("EditorCamera");
            if (camGO == null)
            {
                camGO = new GameObject("EditorCamera");
                Camera cam = camGO.AddComponent<Camera>();
                cam.tag = "MainCamera";

                // Configure camera
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.fieldOfView = 60;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1000f;
            }

            // Add controller if missing
            EditorCameraController controller = camGO.GetComponent<EditorCameraController>();
            if (controller == null)
            {
                controller = camGO.AddComponent<EditorCameraController>();
            }

            // Set default position
            camGO.transform.position = defaultCameraPosition;
            camGO.transform.rotation = Quaternion.Euler(defaultCameraRotation);
        }

        void CreateDefaultAssets()
        {
            // Create folders if they don't exist
            string[] folders = {
                "Zones",
                "Props",
                "Props/Nature",
                "Props/Buildings",
                "Materials",
                "Textures"
            };

            foreach (string folder in folders)
            {
                string path = Application.streamingAssetsPath + "/" + folder;
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
            }

            Debug.Log("Default asset folders created");
        }

        void LoadLastOrDefaultZone()
        {
            RubyEditorEnhanced editor = RubyEditorEnhanced.Instance;
            if (editor == null) return;

            string lastZone = PlayerPrefs.GetString("RubyEditor_LastZone", "");

            if (!string.IsNullOrEmpty(lastZone))
            {
                // Try to load last zone
                ZoneSaveSystem saveSystem = FindFirstObjectByType<ZoneSaveSystem>();
                if (saveSystem != null)
                {
                    var zones = saveSystem.GetAvailableZones();
                    if (zones.Contains(lastZone))
                    {
                        editor.currentZone = saveSystem.LoadZone(lastZone);
                        Debug.Log($"Loaded last zone: {lastZone}");
                        return;
                    }
                }
            }

            // Create new zone if no last zone
            editor.CreateNewZone();
            Debug.Log("Created new default zone");
        }

        void ShowWelcomeMessage()
        {
            string message = @"
===== Ruby GameEditor =====
Hotkeys:
Q - Select Mode
W - Place Mode  
E - Paint Mode
R - Terrain Mode

G - Toggle Grid
Ctrl+S - Save Zone
Ctrl+O - Open Zone
Ctrl+N - New Zone

[ ] - Brush Size
- + - Brush Strength
=========================";

            Debug.Log(message);
        }
    }
}