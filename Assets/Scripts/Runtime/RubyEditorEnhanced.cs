using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using RubyEditor.Core;
using RubyEditor.Tools;
using RubyEditor.Data;

namespace RubyEditor.Core
{
    public class RubyEditorEnhanced : MonoBehaviour
    {
        [Header("Editor Systems")]
        public EditorManager editorManager;
        public GridSystem gridSystem;
        public SelectionSystem selectionSystem;
        public EditorCameraController cameraController;
        public ObjectPlacementTool placementTool;
        public ZoneSaveSystem saveSystem;

        [Header("Current Zone")]
        public ZoneData currentZone;
        public bool hasUnsavedChanges = false;

        [Header("Editor State")]
        public EditorMode currentMode = EditorMode.Select;
        public bool isEditingTerrain = false;

        // Tool references
        private Dictionary<EditorMode, EditorTool> tools = new Dictionary<EditorMode, EditorTool>();
        private EditorTool currentTool;

        // UI References
        private RubyEditor.UI.EditorUIManager uiManager;

        public static RubyEditorEnhanced Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            InitializeEditor();
        }

        void InitializeEditor()
        {
            Debug.Log("🚀 RubyEditor wird initialisiert...");

            // Get or create systems
            SetupCoreSystems();

            // Initialize tools
            InitializeTools();

            // Setup UI
            SetupUI();

            // Load last zone if exists
            LoadLastZone();

            Debug.Log("✅ RubyEditor erfolgreich gestartet!");
        }

        void SetupCoreSystems()
        {
            // Editor Manager
            if (editorManager == null)
                editorManager = GetComponent<EditorManager>() ?? gameObject.AddComponent<EditorManager>();

            // Grid System
            if (gridSystem == null)
                gridSystem = GetComponent<GridSystem>() ?? gameObject.AddComponent<GridSystem>();

            // Selection System
            if (selectionSystem == null)
                selectionSystem = GetComponent<SelectionSystem>() ?? gameObject.AddComponent<SelectionSystem>();

            // Camera Controller
            if (cameraController == null)
            {
                GameObject camObj = GameObject.Find("EditorCamera");
                if (camObj == null)
                {
                    camObj = new GameObject("EditorCamera");
                    camObj.AddComponent<Camera>();
                }
                cameraController = camObj.GetComponent<EditorCameraController>() ??
                                 camObj.AddComponent<EditorCameraController>();
            }

            // Save System
            if (saveSystem == null)
                saveSystem = GetComponent<ZoneSaveSystem>() ?? gameObject.AddComponent<ZoneSaveSystem>();
        }

        void InitializeTools()
        {
            // Object Placement Tool
            if (placementTool == null)
                placementTool = GetComponent<ObjectPlacementTool>() ?? gameObject.AddComponent<ObjectPlacementTool>();

            tools[EditorMode.Place] = placementTool;

            // Terrain Tools
            var terrainSculptTool = GetComponent<TerrainSculptTool>() ?? gameObject.AddComponent<TerrainSculptTool>();
            tools[EditorMode.Terrain] = terrainSculptTool;

            var terrainPaintTool = GetComponent<TerrainPaintTool>() ?? gameObject.AddComponent<TerrainPaintTool>();
            tools[EditorMode.Paint] = terrainPaintTool;
        }

        void SetupUI()
        {
            uiManager = RubyEditor.UI.EditorUIManager.Instance;
            if (uiManager == null)
            {
                GameObject uiObj = new GameObject("EditorUI");
                uiManager = uiObj.AddComponent<RubyEditor.UI.EditorUIManager>();
            }
        }

        void Update()
        {
            HandleInput();
            UpdateCurrentTool();

            // Update UI
            if (uiManager != null && cameraController != null)
            {
                Ray ray = cameraController.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    uiManager.UpdateCoordinates(hit.point);
                }
            }
        }

        void HandleInput()
        {
            // Mode switching
            if (Input.GetKeyDown(KeyCode.Q)) SetMode(EditorMode.Select);
            if (Input.GetKeyDown(KeyCode.W)) SetMode(EditorMode.Place);
            if (Input.GetKeyDown(KeyCode.E)) SetMode(EditorMode.Paint);
            if (Input.GetKeyDown(KeyCode.R)) SetMode(EditorMode.Terrain);

            // Save/Load
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.S)) SaveZone();
                if (Input.GetKeyDown(KeyCode.O)) LoadZone();
                if (Input.GetKeyDown(KeyCode.N)) CreateNewZone();
            }

            // Grid toggle
            if (Input.GetKeyDown(KeyCode.G))
            {
                gridSystem.showGrid = !gridSystem.showGrid;
                gridSystem.snapToGrid = !gridSystem.snapToGrid;
            }

            // Undo/Redo (simplified)
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.Z)) Undo();
                if (Input.GetKeyDown(KeyCode.Y)) Redo();
            }
        }

        void UpdateCurrentTool()
        {
            if (currentTool != null)
            {
                currentTool.UpdateTool();
            }
        }

        public void SetMode(EditorMode mode)
        {
            // Deactivate current tool
            if (currentTool != null)
            {
                currentTool.OnToolDeactivated();
            }

            // Switch mode
            currentMode = mode;
            editorManager.currentMode = mode;

            // Activate new tool
            if (tools.ContainsKey(mode))
            {
                currentTool = tools[mode];
                currentTool.OnToolActivated();
            }
            else
            {
                currentTool = null;
            }

            Debug.Log($"Editor Mode: {mode}");
        }

        public void CreateNewZone()
        {
            if (hasUnsavedChanges)
            {
                if (!ShowSaveDialog()) return;
            }

            // Create new zone data
            currentZone = new ZoneData();
            currentZone.zoneName = "New Zone";
            currentZone.zoneDescription = "A new adventure awaits...";
            currentZone.author = System.Environment.UserName;

            // Clear scene
            ClearScene();

            // Create default terrain
            CreateDefaultTerrain();

            hasUnsavedChanges = false;
            Debug.Log("Neue Zone erstellt!");
        }

        void CreateDefaultTerrain()
        {
            GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
            terrainObj.name = "Zone_Terrain";

            Terrain terrain = terrainObj.GetComponent<Terrain>();
            TerrainData terrainData = terrain.terrainData;

            // Configure terrain
            terrainData.heightmapResolution = 257;
            terrainData.size = new Vector3(currentZone.zoneSize.x, 50, currentZone.zoneSize.y);

            // Center terrain
            float halfX = currentZone.zoneSize.x / 2;
            float halfZ = currentZone.zoneSize.y / 2;
            terrainObj.transform.position = new Vector3(-halfX, 0, -halfZ);

            // Add default texture
            ApplyDefaultTerrainTextures(terrain);
        }

        void ApplyDefaultTerrainTextures(Terrain terrain)
        {
            // This would apply default grass/dirt textures
            // Implementation depends on your texture setup
        }

        public void SaveZone()
        {
            if (currentZone == null)
            {
                Debug.LogWarning("Keine Zone zum Speichern!");
                return;
            }

            // Use the ZoneSaveSystem
            saveSystem.SaveZone(currentZone.zoneName);
            hasUnsavedChanges = false;

            ShowNotification("Zone gespeichert!", NotificationType.Success);
        }

        public void LoadZone()
        {
            if (hasUnsavedChanges)
            {
                if (!ShowSaveDialog()) return;
            }

            // Show zone selection dialog
            List<string> availableZones = saveSystem.GetAvailableZones();

            // For now, just load the first one
            if (availableZones.Count > 0)
            {
                currentZone = saveSystem.LoadZone(availableZones[0]);
                hasUnsavedChanges = false;
            }
            else
            {
                ShowNotification("Keine Zonen gefunden!", NotificationType.Warning);
            }
        }

        void LoadLastZone()
        {
            string lastZone = PlayerPrefs.GetString("RubyEditor_LastZone", "");
            if (!string.IsNullOrEmpty(lastZone))
            {
                var zones = saveSystem.GetAvailableZones();
                if (zones.Contains(lastZone))
                {
                    currentZone = saveSystem.LoadZone(lastZone);
                }
            }
        }

        void ClearScene()
        {
            // Clear all placed objects
            GameObject[] objects = GameObject.FindGameObjectsWithTag("EditorObject");
            foreach (var obj in objects)
            {
                DestroyImmediate(obj);
            }

            // Clear terrain
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var terrain in terrains)
            {
                DestroyImmediate(terrain.gameObject);
            }
        }

        bool ShowSaveDialog()
        {
            // In Unity runtime, you'd show a UI dialog
            // For now, auto-save
            SaveZone();
            return true;
        }

        void ShowNotification(string message, NotificationType type)
        {
            Debug.Log($"[{type}] {message}");
            // In real implementation, show UI notification
        }

        void Undo()
        {
            Debug.Log("Undo - Not implemented yet");
        }

        void Redo()
        {
            Debug.Log("Redo - Not implemented yet");
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && hasUnsavedChanges)
            {
                SaveZone();
            }
        }

        void OnApplicationQuit()
        {
            if (hasUnsavedChanges)
            {
                SaveZone();
            }

            // Save last zone
            if (currentZone != null)
            {
                PlayerPrefs.SetString("RubyEditor_LastZone", currentZone.zoneName);
            }
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}