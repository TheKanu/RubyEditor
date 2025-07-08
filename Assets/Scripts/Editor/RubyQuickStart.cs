using UnityEngine;
using RubyEditor.Core;

public class RubyEditorQuickStart : MonoBehaviour
{
    [Header("Quick Start Settings")]
    [SerializeField] private bool autoStartEditor = true;
    [SerializeField] private bool createTestTerrain = true;
    [SerializeField] private bool addTestProps = true;

    void Start()
    {
        if (autoStartEditor)
        {
            StartEditor();
        }
    }

    [ContextMenu("Start Ruby Editor")]
    public void StartEditor()
    {
        Debug.Log("🚀 Starting Ruby Editor...");

        // 1. Find or create Bootstrap
        RubyEditorBootstrap bootstrap = FindObjectOfType<RubyEditorBootstrap>();
        if (bootstrap == null)
        {
            GameObject bootstrapGO = new GameObject("RubyEditorBootstrap");
            bootstrap = bootstrapGO.AddComponent<RubyEditorBootstrap>();
        }

        // 2. Create test environment if needed
        if (createTestTerrain)
        {
            CreateTestEnvironment();
        }

        // 3. Add test props
        if (addTestProps)
        {
            AddTestProps();
        }

        // 4. Show controls
        ShowQuickHelp();
    }

    void CreateTestEnvironment()
    {
        // Check if terrain exists
        if (Terrain.activeTerrain == null)
        {
            // Create terrain
            GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            TerrainData terrainData = terrain.terrainData;

            // Configure
            terrainData.heightmapResolution = 257;
            terrainData.size = new Vector3(100, 20, 100);
            terrainObj.transform.position = new Vector3(-50, 0, -50);

            Debug.Log("✅ Created test terrain");
        }

        // Add lighting
        if (FindObjectOfType<Light>() == null)
        {
            GameObject lightGO = new GameObject("Sun");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0);
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.8f);

            Debug.Log("✅ Created sun light");
        }
    }

    void AddTestProps()
    {
        // Create prop container
        GameObject propContainer = GameObject.Find("TestProps");
        if (propContainer == null)
        {
            propContainer = new GameObject("TestProps");
        }

        // Create some basic props for testing
        CreateTestProp("TestCube", PrimitiveType.Cube, new Vector3(0, 1, 5), propContainer.transform);
        CreateTestProp("TestSphere", PrimitiveType.Sphere, new Vector3(5, 1, 0), propContainer.transform);
        CreateTestProp("TestCylinder", PrimitiveType.Cylinder, new Vector3(-5, 1, 0), propContainer.transform);

        Debug.Log("✅ Created test props");
    }

    void CreateTestProp(string name, PrimitiveType type, Vector3 position, Transform parent)
    {
        GameObject prop = GameObject.CreatePrimitive(type);
        prop.name = name;
        prop.transform.position = position;
        prop.transform.parent = parent;

        // Add random color
        Renderer renderer = prop.GetComponent<Renderer>();
        renderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
    }

    void ShowQuickHelp()
    {
        string helpText = @"
🎮 RUBY EDITOR CONTROLS
=======================
MODES:
Q - Select Mode
W - Place Objects  
E - Paint Terrain
R - Sculpt Terrain

CAMERA:
WASD - Move
Right Mouse - Look Around
Scroll - Zoom

TERRAIN TOOLS:
[ ] - Brush Size
- = - Brush Strength
1-5 - Tool Modes

GENERAL:
G - Toggle Grid
Ctrl+S - Save Zone
Ctrl+Z - Undo
ESC - Cancel Tool
=======================";

        Debug.Log(helpText);

        // Optional: Show in-game UI
        ShowInGameHelp(helpText);
    }

    void ShowInGameHelp(string text)
    {
        GameObject helpPanel = new GameObject("HelpPanel");
        helpPanel.transform.SetParent(FindObjectOfType<Canvas>()?.transform);

        // This would create an actual UI panel
        // For now just log it
    }

    void Update()
    {
        // Quick toggle help
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ShowQuickHelp();
        }

        // Quick save
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S))
        {
            QuickSave();
        }
    }

    void QuickSave()
    {
        RubyEditorEnhanced editor = RubyEditorEnhanced.Instance;
        if (editor != null)
        {
            editor.SaveZone();
            Debug.Log("💾 Zone saved!");
        }
    }
}

// Extension for easy testing in Inspector
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(RubyEditorQuickStart))]
public class RubyEditorQuickStartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        RubyEditorQuickStart quickStart = (RubyEditorQuickStart)target;

        if (GUILayout.Button("🚀 Start Editor", GUILayout.Height(40)))
        {
            quickStart.StartEditor();
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Terrain"))
        {
            CreateTerrain();
        }
        if (GUILayout.Button("Add Props"))
        {
            quickStart.AddTestProps();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Show Help (F1)"))
        {
            quickStart.ShowQuickHelp();
        }
    }

    void CreateTerrain()
    {
        GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
        terrainObj.name = "EditorTerrain";
        Selection.activeGameObject = terrainObj;
    }
}
#endif