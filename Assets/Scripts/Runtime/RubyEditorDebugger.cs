using UnityEngine;
using RubyEditor.Core;
using RubyEditor.Tools;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RubyEditor.Diagnostics
{
    public class RubyEditorDebugger : MonoBehaviour
    {
        [Header("Debug Info")]
        public bool showDebugInfo = true;
        public bool autoCreateTerrain = true;
        public bool autoStartEditor = true;

        [Header("Status")]
        public bool editorActive = false;
        public bool terrainExists = false;
        public bool toolsInitialized = false;
        public string currentMode = "None";


        private RubyEditorEnhanced editor;
        private TerrainSculptTool sculptTool;
        private Terrain currentTerrain;

        void Start()
        {
            if (autoStartEditor)
            {
                InitializeEditor();
            }
        }

        void Update()
        {
            UpdateDebugInfo();

            // Debug controls
            if (IsKeyDown(KeyCode.F1))
            {
                InitializeEditor();
            }

            if (IsKeyDown(KeyCode.F2))
            {
                CreateTestTerrain();
            }

            if (IsKeyDown(KeyCode.F3))
            {
                ForceTerrainMode();
            }

            if (IsKeyDown(KeyCode.F4))
            {
                TestTerrainSculpting();
            }
        }

        void InitializeEditor()
        {
            Debug.Log("🚀 Initializing RubyEditor Debugger...");

            // Find or create RubyEditorEnhanced
            editor = FindObjectOfType<RubyEditorEnhanced>();
            if (editor == null)
            {
                GameObject editorGO = new GameObject("RubyEditorEnhanced");
                editor = editorGO.AddComponent<RubyEditorEnhanced>();
                Debug.Log("✅ Created RubyEditorEnhanced");
            }
            else
            {
                Debug.Log("✅ Found existing RubyEditorEnhanced");
            }

            // Create terrain if needed
            if (autoCreateTerrain)
            {
                CreateTestTerrain();
            }

            // Initialize tools
            sculptTool = FindObjectOfType<TerrainSculptTool>();
            if (sculptTool == null)
            {
                sculptTool = editor.gameObject.GetComponent<TerrainSculptTool>();
                if (sculptTool == null)
                {
                    sculptTool = editor.gameObject.AddComponent<TerrainSculptTool>();
                }
            }

            editorActive = true;
            Debug.Log("✅ RubyEditor Debugger initialized!");
        }

        void CreateTestTerrain()
        {
            currentTerrain = Terrain.activeTerrain;
            if (currentTerrain == null)
            {
                Debug.Log("🌍 Creating test terrain...");

                // Create terrain
                GameObject terrainGO = Terrain.CreateTerrainGameObject(null);
                currentTerrain = terrainGO.GetComponent<Terrain>();
                TerrainData terrainData = currentTerrain.terrainData;

                // Configure terrain
                terrainData.heightmapResolution = 513;
                terrainData.size = new Vector3(200, 50, 200);
                terrainGO.transform.position = new Vector3(-100, 0, -100);

                // Add collider
                if (terrainGO.GetComponent<TerrainCollider>() == null)
                {
                    terrainGO.AddComponent<TerrainCollider>();
                }

                Debug.Log("✅ Test terrain created!");
            }
            else
            {
                Debug.Log("✅ Terrain already exists");
            }

            terrainExists = true;
        }

        void ForceTerrainMode()
        {
            if (editor != null)
            {
                Debug.Log("🔧 Forcing terrain sculpt mode...");
                editor.SetMode(EditorMode.Terrain);
                currentMode = "Terrain";
                Debug.Log("✅ Terrain mode activated!");
            }
        }

        void TestTerrainSculpting()
        {
            if (currentTerrain != null)
            {
                Debug.Log("🏔️ Testing terrain sculpting...");

                // Get terrain data
                TerrainData data = currentTerrain.terrainData;
                int resolution = data.heightmapResolution;

                // Create a small hill in the center
                int centerX = resolution / 2;
                int centerZ = resolution / 2;
                int radius = 20;

                float[,] heights = data.GetHeights(centerX - radius, centerZ - radius, radius * 2, radius * 2);

                for (int z = 0; z < radius * 2; z++)
                {
                    for (int x = 0; x < radius * 2; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, z), new Vector2(radius, radius));
                        if (distance <= radius)
                        {
                            float height = (1f - distance / radius) * 0.1f;
                            heights[z, x] = Mathf.Max(heights[z, x], height);
                        }
                    }
                }

                data.SetHeights(centerX - radius, centerZ - radius, heights);
                Debug.Log("✅ Test hill created!");
            }
        }

        void UpdateDebugInfo()
        {
            if (editor != null)
            {
                editorActive = editor.enabled;
                currentMode = editor.currentMode.ToString();
            }

            terrainExists = Terrain.activeTerrain != null;
            toolsInitialized = sculptTool != null;
        }

        void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("RubyEditor Debug Info", GUI.skin.label);
            GUILayout.Label($"Editor Active: {editorActive}");
            GUILayout.Label($"Terrain Exists: {terrainExists}");
            GUILayout.Label($"Tools Initialized: {toolsInitialized}");
            GUILayout.Label($"Current Mode: {currentMode}");

            GUILayout.Space(10);
            GUILayout.Label("Debug Controls:");
            GUILayout.Label("F1 - Initialize Editor");
            GUILayout.Label("F2 - Create Terrain");
            GUILayout.Label("F3 - Force Terrain Mode");
            GUILayout.Label("F4 - Test Sculpting");

            GUILayout.Space(10);
            GUILayout.Label("Terraform Controls:");
            GUILayout.Label("R - Terrain Mode");
            GUILayout.Label("0 - Drag Sculpt");
            GUILayout.Label("1-5 - Basic Tools");
            GUILayout.Label("Mouse + Drag - Sculpt");

            GUILayout.EndArea();
        }

        // Input compatibility methods
        bool IsKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && GetKeyFromKeyCode(key).wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Controls.KeyControl GetKeyFromKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F1: return Keyboard.current.f1Key;
                case KeyCode.F2: return Keyboard.current.f2Key;
                case KeyCode.F3: return Keyboard.current.f3Key;
                case KeyCode.F4: return Keyboard.current.f4Key;
                case KeyCode.R: return Keyboard.current.rKey;
                default: return Keyboard.current.spaceKey;
            }
        }
#endif
    }
}
