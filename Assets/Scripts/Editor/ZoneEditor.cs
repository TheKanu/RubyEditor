#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;

namespace RubyMMO.Editor
{
    public class ZoneEditorWindow : EditorWindow
    {
        // Window State
        private Vector2 scrollPos;
        private string searchFilter = "";
        private ZoneData currentZone;
        private bool showZoneSettings = true;
        private bool showSpawnPoints = true;
        private bool showPropLibrary = true;
        private bool showTerrainTools = true;

        // Editing State
        private Tool lastTool;
        private bool isPlacingMode = false;
        private GameObject previewObject;
        private PropCategory selectedCategory = PropCategory.All;

        // Terrain Painting
        private bool isTerrainPainting = false;
        private float brushSize = 10f;
        private float brushStrength = 0.5f;
        private Texture2D selectedTexture;

        // Grid Snapping
        private bool useGridSnap = true;
        private float gridSize = 1f;

        // Zone Templates
        private readonly string[] zoneTemplates = new string[] {
            "Empty Zone",
            "Forest Zone",
            "Desert Zone",
            "Snow Zone",
            "Dungeon Interior",
            "City Hub"
        };

        [MenuItem("Ruby MMO/Zone Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ZoneEditorWindow>("Zone Editor");
            window.minSize = new Vector2(400, 600);
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadZoneData();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreview();
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            EditorGUILayout.Space(10);

            if (showZoneSettings) DrawZoneSettings();
            if (showSpawnPoints) DrawSpawnPointsSection();
            if (showPropLibrary) DrawPropLibrary();
            if (showTerrainTools) DrawTerrainTools();

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New Zone", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                try
                {
                    CreateNewZone();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error creating new zone: " + e.Message);
                }
            }

            if (GUILayout.Button("Load Zone", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                LoadZone();
            }

            if (GUILayout.Button("Save Zone", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SaveZone();
            }

            GUILayout.FlexibleSpace();

            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
        }

        void DrawZoneSettings()
        {
            showZoneSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showZoneSettings, "Zone Settings");
            if (showZoneSettings)
            {
                EditorGUI.indentLevel++;

                if (currentZone == null)
                {
                    EditorGUILayout.HelpBox("No zone loaded. Create or load a zone to begin.", MessageType.Info);
                }
                else
                {
                    currentZone.zoneName = EditorGUILayout.TextField("Zone Name", currentZone.zoneName);
                    currentZone.zoneID = EditorGUILayout.IntField("Zone ID", currentZone.zoneID);
                    currentZone.zoneType = (ZoneType)EditorGUILayout.EnumPopup("Zone Type", currentZone.zoneType);

                    EditorGUILayout.Space(5);

                    EditorGUILayout.LabelField("Zone Bounds", EditorStyles.boldLabel);
                    currentZone.zoneSize = EditorGUILayout.Vector2IntField("Size (X,Z)", currentZone.zoneSize);

                    EditorGUILayout.Space(5);

                    EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
                    currentZone.ambientColor = EditorGUILayout.ColorField("Ambient Color", currentZone.ambientColor);
                    currentZone.fogColor = EditorGUILayout.ColorField("Fog Color", currentZone.fogColor);
                    currentZone.fogDensity = EditorGUILayout.Slider("Fog Density", currentZone.fogDensity, 0f, 0.1f);

                    EditorGUILayout.Space(5);

                    useGridSnap = EditorGUILayout.Toggle("Grid Snap", useGridSnap);
                    if (useGridSnap)
                    {
                        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(10);
        }

        void DrawSpawnPointsSection()
        {
            showSpawnPoints = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawnPoints, "Spawn Points");
            if (showSpawnPoints)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Player Spawn", GUILayout.Height(30)))
                {
                    AddSpawnPoint(SpawnType.Player);
                }
                if (GUILayout.Button("Add NPC Spawn", GUILayout.Height(30)))
                {
                    AddSpawnPoint(SpawnType.NPC);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // List existing spawn points
                if (currentZone != null && currentZone.spawnPoints.Count > 0)
                {
                    EditorGUILayout.LabelField($"Spawn Points ({currentZone.spawnPoints.Count})", EditorStyles.boldLabel);

                    for (int i = 0; i < currentZone.spawnPoints.Count; i++)
                    {
                        var spawn = currentZone.spawnPoints[i];
                        EditorGUILayout.BeginHorizontal();

                        spawn.name = EditorGUILayout.TextField(spawn.name, GUILayout.Width(150));
                        EditorGUILayout.LabelField(spawn.type.ToString(), GUILayout.Width(60));

                        if (GUILayout.Button("Select", GUILayout.Width(50)))
                        {
                            SelectSpawnPoint(spawn);
                        }

                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            RemoveSpawnPoint(i);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(10);
        }

        void DrawPropLibrary()
        {
            showPropLibrary = EditorGUILayout.BeginFoldoutHeaderGroup(showPropLibrary, "Prop Library");
            if (showPropLibrary)
            {
                EditorGUI.indentLevel++;

                // Category Filter
                selectedCategory = (PropCategory)EditorGUILayout.EnumPopup("Category", selectedCategory);

                // Prop Grid
                var props = GetPropsForCategory(selectedCategory);
                int columns = 3;
                int rows = Mathf.CeilToInt(props.Count / (float)columns);

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.imagePosition = ImagePosition.ImageAbove;
                buttonStyle.fixedHeight = 80;

                for (int row = 0; row < rows; row++)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index < props.Count)
                        {
                            var prop = props[index];

                            if (GUILayout.Button(new GUIContent(prop.name, prop.icon), buttonStyle))
                            {
                                StartPlacingProp(prop);
                            }
                        }
                        else
                        {
                            GUILayout.Space(buttonStyle.fixedHeight);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(10);
        }

        void DrawTerrainTools()
        {
            showTerrainTools = EditorGUILayout.BeginFoldoutHeaderGroup(showTerrainTools, "Terrain Tools");
            if (showTerrainTools)
            {
                EditorGUI.indentLevel++;

                if (GUILayout.Button("Create Terrain", GUILayout.Height(30)))
                {
                    if (currentZone != null)
                    {
                        CreateTerrain();
                    }
                    else
                    {
                        Debug.LogWarning("Cannot create terrain: No zone selected. Create a new zone first.");
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Terrain Painting", EditorStyles.boldLabel);

                isTerrainPainting = EditorGUILayout.Toggle("Paint Mode", isTerrainPainting);

                if (isTerrainPainting)
                {
                    brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 1f, 50f);
                    brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

                    // Texture selection would go here
                    if (GUILayout.Button("Select Texture"))
                    {
                        // Open texture picker
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawFooter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label($"Zone: {(currentZone != null ? currentZone.zoneName : "None")}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (isPlacingMode)
            {
                GUILayout.Label("Placing Mode Active - Click to place, ESC to cancel", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        void OnSceneGUI(SceneView sceneView)
        {
            // Handle prop placement
            if (isPlacingMode && previewObject != null)
            {
                HandlePropPlacement();
            }

            // Handle terrain painting
            if (isTerrainPainting)
            {
                HandleTerrainPainting();
            }

            // Draw zone bounds
            if (currentZone != null)
            {
                DrawZoneBounds();
            }

            // Draw spawn points
            if (showSpawnPoints && currentZone != null)
            {
                DrawSpawnPointGizmos();
            }
        }

        void HandlePropPlacement()
        {
            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                Vector3 position = hit.point;

                if (useGridSnap)
                {
                    position.x = Mathf.Round(position.x / gridSize) * gridSize;
                    position.z = Mathf.Round(position.z / gridSize) * gridSize;
                }

                previewObject.transform.position = position;

                // Rotate with R key
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
                {
                    previewObject.transform.Rotate(0, 45, 0);
                    e.Use();
                }

                // Place on click
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlaceProp();
                    e.Use();
                }

                // Cancel with ESC
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    CancelPlacement();
                    e.Use();
                }
            }

            SceneView.RepaintAll();
        }

        void CreateNewZone()
        {
            // Show template selection
            int template = EditorUtility.DisplayDialogComplex(
                "Create New Zone",
                "Select a zone template:",
                "Forest",
                "Cancel",
                "Empty"
            );

            if (template != 1) // Not cancelled
            {
                currentZone = new ZoneData();
                currentZone.zoneName = "New Zone";
                currentZone.zoneID = Random.Range(1000, 9999);

                // Apply template
                if (template == 0) // Forest
                {
                    SetupForestTemplate();
                }

                CreateZoneGameObject();
            }
        }

        void SetupForestTemplate()
        {
            currentZone.zoneType = ZoneType.Outdoor;
            currentZone.ambientColor = new Color(0.4f, 0.5f, 0.4f);
            currentZone.fogColor = new Color(0.6f, 0.7f, 0.6f);
            currentZone.fogDensity = 0.02f;

            // Create terrain
            CreateTerrain();

            // Add some default spawn points
            AddSpawnPoint(SpawnType.Player, new Vector3(0, 0, 0));
        }

        void CreateTerrain()
        {
            try
            {
                if (currentZone == null)
                {
                    Debug.LogError("Cannot create terrain: currentZone is null");
                    return;
                }
                
                GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
                if (terrainObj == null)
                {
                    Debug.LogError("Failed to create terrain GameObject");
                    return;
                }
                
                terrainObj.name = $"Terrain_{currentZone.zoneName ?? "Unknown"}";

                Terrain terrain = terrainObj.GetComponent<Terrain>();
                if (terrain == null)
                {
                    Debug.LogError("Terrain component not found on terrain GameObject");
                    return;
                }
                
                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                {
                    Debug.LogError("TerrainData is null");
                    return;
                }

                // Set terrain size based on zone size (with fallback)
                Vector2 zoneSize = new Vector2(currentZone.zoneSize.x, currentZone.zoneSize.y);
                if (zoneSize.x <= 0) zoneSize.x = 200f;
                if (zoneSize.y <= 0) zoneSize.y = 200f;
                
                terrainData.heightmapResolution = 513;
                terrainData.size = new Vector3(zoneSize.x, 100, zoneSize.y);

                // Center terrain
                terrainObj.transform.position = new Vector3(-zoneSize.x / 2, 0, -zoneSize.y / 2);

                // Apply basic textures if available
                ApplyDefaultTerrainTextures(terrain);
                
                Debug.Log($"Terrain created successfully: {terrainObj.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating terrain: {e.Message}\n{e.StackTrace}");
            }
        }

        void ApplyDefaultTerrainTextures(Terrain terrain)
        {
            if (terrain == null)
            {
                Debug.LogWarning("Cannot apply textures: terrain is null");
                return;
            }
            
            // This would load default textures from Resources or a database
            // For now, just a placeholder
            Debug.Log("Default terrain textures applied (placeholder)");
        }

        void StartPlacingProp(PropData prop)
        {
            if (prop == null)
            {
                Debug.LogWarning("Cannot start placing prop: prop is null");
                return;
            }
            
            isPlacingMode = true;
            lastTool = Tools.current;
            Tools.current = Tool.None;

            // Create preview
            if (prop.prefab != null)
            {
                previewObject = Instantiate(prop.prefab);
                previewObject.name = "Preview_" + (prop.name ?? "Unknown");

                // Make preview semi-transparent
                SetPreviewMaterial(previewObject);
            }
            else
            {
                Debug.LogWarning($"Cannot create preview: prop.prefab is null for {prop.name}");
            }
        }

        void SetPreviewMaterial(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Cannot set preview material: object is null");
                return;
            }
            
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers == null) return;
            
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                
                var materials = renderer.sharedMaterials;
                if (materials == null) continue;
                
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        Material previewMat = new Material(materials[i]);
                        previewMat.color = new Color(previewMat.color.r, previewMat.color.g, previewMat.color.b, 0.5f);
                        materials[i] = previewMat;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        void PlaceProp()
        {
            if (previewObject != null)
            {
                try
                {
                    // Create actual prop
                    GameObject prop = Instantiate(previewObject);
                    if (prop != null)
                    {
                        prop.name = previewObject.name.Replace("Preview_", "");

                        // Register undo
                        Undo.RegisterCreatedObjectUndo(prop, "Place Prop");

                        // Continue placing or finish
                        if (Event.current == null || !Event.current.shift)
                        {
                            CancelPlacement();
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to instantiate prop");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error placing prop: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("Cannot place prop: previewObject is null");
            }
        }

        void CancelPlacement()
        {
            isPlacingMode = false;
            Tools.current = lastTool;
            CleanupPreview();
        }

        void CleanupPreview()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
            }
        }

        void DrawZoneBounds()
        {
            if (currentZone == null) return;
            
            Handles.color = new Color(1, 1, 0, 0.3f);

            Vector3[] corners = new Vector3[] {
                new Vector3(-currentZone.zoneSize.x/2, 0, -currentZone.zoneSize.y/2),
                new Vector3(currentZone.zoneSize.x/2, 0, -currentZone.zoneSize.y/2),
                new Vector3(currentZone.zoneSize.x/2, 0, currentZone.zoneSize.y/2),
                new Vector3(-currentZone.zoneSize.x/2, 0, currentZone.zoneSize.y/2)
            };

            Handles.DrawSolidRectangleWithOutline(corners, new Color(1, 1, 0, 0.1f), Color.yellow);
        }

        void DrawSpawnPointGizmos()
        {
            if (currentZone == null || currentZone.spawnPoints == null) return;
            
            foreach (var spawn in currentZone.spawnPoints)
            {
                if (spawn == null) continue;
                
                Vector3 pos = spawn.position;

                // Different colors for different spawn types
                Handles.color = spawn.type == SpawnType.Player ? Color.green : Color.cyan;

                // Draw spawn marker
                Handles.DrawWireArc(pos, Vector3.up, Vector3.forward, 360, 1f);
                Handles.DrawLine(pos, pos + Vector3.up * 2f);

                // Draw label
                Handles.Label(pos + Vector3.up * 2.5f, spawn.name ?? "Unnamed Spawn");
            }
        }

        void AddSpawnPoint(SpawnType type, Vector3? position = null)
        {
            if (currentZone == null)
            {
                Debug.LogWarning("Cannot add spawn point: currentZone is null");
                return;
            }
            
            if (currentZone.spawnPoints == null)
            {
                currentZone.spawnPoints = new List<SpawnPoint>();
            }

            SpawnPoint spawn = new SpawnPoint();
            spawn.type = type;
            spawn.name = $"{type} Spawn {currentZone.spawnPoints.Count + 1}";
            spawn.position = position ?? GetSceneViewCenter();
            spawn.rotation = Quaternion.identity;

            currentZone.spawnPoints.Add(spawn);

            // Create visual marker
            GameObject marker = new GameObject(spawn.name);
            marker.transform.position = spawn.position;

            // Add icon/gizmo component
            marker.AddComponent<SpawnPointMarker>().spawnPoint = spawn;
        }

        Vector3 GetSceneViewCenter()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null && sceneView.camera.transform != null)
            {
                return sceneView.camera.transform.position + sceneView.camera.transform.forward * 10f;
            }
            return Vector3.zero;
        }

        void SelectSpawnPoint(SpawnPoint spawn)
        {
            if (spawn == null || string.IsNullOrEmpty(spawn.name))
            {
                Debug.LogWarning("Cannot select spawn point: spawn is null or has no name");
                return;
            }
            
            GameObject marker = GameObject.Find(spawn.name);
            if (marker != null)
            {
                Selection.activeGameObject = marker;
                SceneView.FrameLastActiveSceneView();
            }
            else
            {
                Debug.LogWarning($"Spawn point marker not found: {spawn.name}");
            }
        }

        void RemoveSpawnPoint(int index)
        {
            if (currentZone == null || currentZone.spawnPoints == null)
            {
                Debug.LogWarning("Cannot remove spawn point: currentZone or spawnPoints is null");
                return;
            }
            
            if (index < 0 || index >= currentZone.spawnPoints.Count)
            {
                Debug.LogWarning($"Invalid spawn point index: {index}");
                return;
            }
            
            var spawn = currentZone.spawnPoints[index];
            if (spawn != null && !string.IsNullOrEmpty(spawn.name))
            {
                GameObject marker = GameObject.Find(spawn.name);
                if (marker != null)
                {
                    DestroyImmediate(marker);
                }
            }
            
            currentZone.spawnPoints.RemoveAt(index);
        }

        void SaveZone()
        {
            if (currentZone == null)
            {
                EditorUtility.DisplayDialog("Error", "No zone to save!", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Zone",
                currentZone.zoneName,
                "asset",
                "Save zone data"
            );

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(currentZone, path);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success", "Zone saved successfully!", "OK");
            }
        }

        void LoadZone()
        {
            string path = EditorUtility.OpenFilePanel("Load Zone", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                currentZone = AssetDatabase.LoadAssetAtPath<ZoneData>(path);

                if (currentZone != null)
                {
                    LoadZoneIntoScene();
                }
            }
        }

        void LoadZoneIntoScene()
        {
            if (currentZone == null)
            {
                Debug.LogError("Cannot load zone: currentZone is null");
                return;
            }
            
            try
            {
                // Clear existing zone objects
                GameObject[] zoneObjects = GameObject.FindGameObjectsWithTag("ZoneObject");
                foreach (var obj in zoneObjects)
                {
                    if (obj != null)
                    {
                        DestroyImmediate(obj);
                    }
                }

                // Recreate zone
                CreateZoneGameObject();

                // Load spawn points
                if (currentZone.spawnPoints != null)
                {
                    foreach (var spawn in currentZone.spawnPoints)
                    {
                        if (spawn != null)
                        {
                            GameObject marker = new GameObject(spawn.name ?? "Unnamed Spawn");
                            marker.transform.position = spawn.position;
                            marker.transform.rotation = spawn.rotation;
                            marker.AddComponent<SpawnPointMarker>().spawnPoint = spawn;
                        }
                    }
                }

                // Apply environment settings
                RenderSettings.ambientLight = currentZone.ambientColor;
                RenderSettings.fogColor = currentZone.fogColor;
                RenderSettings.fogDensity = currentZone.fogDensity;
                RenderSettings.fog = currentZone.fogDensity > 0;
                
                Debug.Log($"Zone loaded into scene: {currentZone.zoneName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading zone into scene: {e.Message}\n{e.StackTrace}");
            }
        }

        void CreateZoneGameObject()
        {
            try
            {
                if (currentZone == null)
                {
                    Debug.LogError("Cannot create zone GameObject: currentZone is null");
                    return;
                }
                
                string zoneName = currentZone.zoneName ?? "Unknown";
                GameObject zoneRoot = new GameObject($"Zone_{zoneName}");
                
                // Set tag (will be created automatically by RubyEditorSetup)
                try
                {
                    zoneRoot.tag = "ZoneObject";
                }
                catch (UnityException)
                {
                    Debug.LogWarning("ZoneObject tag not found, using default tag");
                }

                // Add zone bounds visualizer
                GameObject boundsObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (boundsObj == null)
                {
                    Debug.LogError("Failed to create bounds visualizer");
                    return;
                }
                
                boundsObj.name = "ZoneBounds";
                boundsObj.transform.parent = zoneRoot.transform;
                boundsObj.transform.position = new Vector3(0, -0.05f, 0);

                // Make it transparent
                Renderer renderer = boundsObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Transparent/Diffuse");
                    if (shader != null)
                    {
                        Material mat = new Material(shader);
                        mat.color = new Color(1, 1, 0, 0.1f);
                        renderer.material = mat;
                    }
                    else
                    {
                        Debug.LogWarning("Transparent/Diffuse shader not found");
                    }
                }
                
                // Add safety check for zoneSize
                Vector2 zoneSize = new Vector2(currentZone.zoneSize.x, currentZone.zoneSize.y);
                if (zoneSize.x <= 0) zoneSize.x = 200f;
                if (zoneSize.y <= 0) zoneSize.y = 200f;
                
                boundsObj.transform.localScale = new Vector3(zoneSize.x, 0.1f, zoneSize.y);

                // Remove collider
                Collider collider = boundsObj.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }
                
                Debug.Log($"Zone GameObject created: {zoneRoot.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating zone GameObject: {e.Message}\n{e.StackTrace}");
            }
        }

        void LoadZoneData()
        {
            // Auto-load last zone if exists
            string lastZonePath = EditorPrefs.GetString("LastZonePath", "");
            if (!string.IsNullOrEmpty(lastZonePath))
            {
                currentZone = AssetDatabase.LoadAssetAtPath<ZoneData>(lastZonePath);
            }
        }

        void HandleTerrainPainting()
        {
            // Terrain painting logic would go here
            Event e = Event.current;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    Terrain terrain = hit.collider.GetComponent<Terrain>();
                    if (terrain != null)
                    {
                        // Paint terrain at hit point
                        // This would use Unity's terrain painting API
                    }
                }
            }
        }

        List<PropData> GetPropsForCategory(PropCategory category)
        {
            List<PropData> props = new List<PropData>();

            try
            {
                // Load props from PropDatabase
                PropDatabase database = PropDatabase.GetDatabase();
                if (database != null)
                {
                    List<PropDatabase.PropEntry> entries;
                    
                    if (category == PropCategory.All)
                    {
                        entries = database.GetPropsInCategory(PropCategory.All);
                    }
                    else
                    {
                        entries = database.GetPropsInCategory(category);
                    }

                    // Convert PropDatabase entries to PropData
                    foreach (var entry in entries)
                    {
                        if (entry != null && entry.prefab != null)
                        {
                            props.Add(new PropData
                            {
                                name = entry.displayName ?? entry.id ?? "Unknown",
                                prefab = entry.prefab,
                                icon = entry.thumbnail,
                                category = entry.category
                            });
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("PropDatabase not found. Please create one using Ruby MMO/Setup/Initialize Zone Editor");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading props from database: {e.Message}");
            }

            // Fallback dummy data if database is empty or missing
            if (props.Count == 0)
            {
                props.Add(new PropData { name = "Tree_01", category = PropCategory.Nature });
                props.Add(new PropData { name = "Rock_01", category = PropCategory.Nature });
                props.Add(new PropData { name = "House_01", category = PropCategory.Buildings });
                
                if (category != PropCategory.All)
                {
                    props = props.Where(p => p.category == category).ToList();
                }
            }

            return props;
        }
    }

    // Data Classes
    [System.Serializable]
    public class ZoneData : ScriptableObject
    {
        public string zoneName = "New Zone";
        public int zoneID = 1000;
        public ZoneType zoneType = ZoneType.Outdoor;
        public Vector2Int zoneSize = new Vector2Int(256, 256);

        public Color ambientColor = Color.gray;
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f;

        public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        public List<PropPlacement> props = new List<PropPlacement>();
    }

    [System.Serializable]
    public class SpawnPoint
    {
        public string name;
        public SpawnType type;
        public Vector3 position;
        public Quaternion rotation;
        public float respawnTime = 30f;
        public int maxCount = 1;
    }

    [System.Serializable]
    public class PropPlacement
    {
        public string propID;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale = Vector3.one;
    }

    public class PropData
    {
        public string name;
        public GameObject prefab;
        public Texture2D icon;
        public PropCategory category;
    }

    public enum ZoneType
    {
        Outdoor,
        Indoor,
        Dungeon,
        City,
        PvP
    }

    public enum SpawnType
    {
        Player,
        NPC,
        Monster,
        Resource,
        Quest
    }

    public enum PropCategory
    {
        All,
        Nature,
        Buildings,
        Props,
        Lights,
        Effects,
        Gameplay
    }

    // Marker Component
    public class SpawnPointMarker : MonoBehaviour
    {
        public SpawnPoint spawnPoint;

        void OnDrawGizmos()
        {
            if (spawnPoint != null)
            {
                Gizmos.color = spawnPoint.type == SpawnType.Player ? Color.green : Color.cyan;
                Gizmos.DrawWireSphere(transform.position, 1f);
                Gizmos.DrawIcon(transform.position, "spawn_icon.png", true);
            }
        }
    }
}
#endif