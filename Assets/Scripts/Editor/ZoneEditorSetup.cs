#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace RubyMMO.Editor
{
    public static class ZoneEditorSetup
    {
        [MenuItem("Ruby MMO/Setup/Initialize Zone Editor")]
        public static void InitializeZoneEditor()
        {
            Debug.Log("=== Initializing Ruby MMO Zone Editor ===");

            // Create folder structure
            CreateFolderStructure();

            // Create prop database
            CreatePropDatabase();

            // Create default materials
            CreateDefaultMaterials();

            // Create zone template
            CreateZoneTemplate();

            // Create example props
            CreateExampleProps();

            // Open Zone Editor
            ZoneEditorWindow.ShowWindow();

            Debug.Log("=== Zone Editor Setup Complete! ===");
            EditorUtility.DisplayDialog("Setup Complete",
                "Zone Editor has been initialized!\n\n" +
                "• Folder structure created\n" +
                "• Prop database created\n" +
                "• Example assets generated\n\n" +
                "The Zone Editor window is now open.",
                "OK");
        }

        [MenuItem("Ruby MMO/Setup/Create Test Zone")]
        public static void CreateTestZone()
        {
            // Create a test zone with some basic props
            GameObject zoneRoot = new GameObject("TestZone_ElwynnForest");

            // Create terrain
            GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
            terrainObj.transform.parent = zoneRoot.transform;
            terrainObj.name = "Terrain_Elwynn";

            Terrain terrain = terrainObj.GetComponent<Terrain>();
            TerrainData terrainData = terrain.terrainData;
            terrainData.heightmapResolution = 257;
            terrainData.size = new Vector3(200, 30, 200);
            terrainObj.transform.position = new Vector3(-100, 0, -100);

            // Add some trees
            for (int i = 0; i < 20; i++)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-90f, 90f),
                    0,
                    Random.Range(-90f, 90f)
                );

                GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tree.name = $"Tree_{i}";
                tree.transform.parent = zoneRoot.transform;
                tree.transform.position = randomPos;
                tree.transform.localScale = new Vector3(1, 3, 1);

                // Add leaves
                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaves.name = "Leaves";
                leaves.transform.parent = tree.transform;
                leaves.transform.localPosition = new Vector3(0, 1.5f, 0);
                leaves.transform.localScale = new Vector3(3, 2, 3);

                // Color
                tree.GetComponent<Renderer>().material.color = new Color(0.4f, 0.2f, 0.1f);
                leaves.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 0.2f);
            }

            // Add player spawn
            GameObject playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.parent = zoneRoot.transform;
            playerSpawn.transform.position = Vector3.zero;

            // Add directional light
            GameObject lightObj = new GameObject("Sun");
            lightObj.transform.parent = zoneRoot.transform;
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0);
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.8f);

            // Setup fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 150f;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);

            Debug.Log("Test zone 'Elwynn Forest' created!");
        }

        static void CreateFolderStructure()
        {
            string basePath = "Assets/RubyMMO";

            string[] folders = new string[]
            {
                basePath,
                $"{basePath}/Zones",
                $"{basePath}/Zones/Outdoor",
                $"{basePath}/Zones/Dungeons",
                $"{basePath}/Zones/Cities",
                $"{basePath}/Props",
                $"{basePath}/Props/Nature",
                $"{basePath}/Props/Buildings",
                $"{basePath}/Props/Decorations",
                $"{basePath}/Props/Lights",
                $"{basePath}/Props/Primitives",
                $"{basePath}/Props/Thumbnails",
                $"{basePath}/Materials",
                $"{basePath}/Materials/Terrain",
                $"{basePath}/Materials/Props",
                $"{basePath}/Textures",
                $"{basePath}/Textures/Terrain",
                $"{basePath}/Textures/Props",
                $"{basePath}/Editor",
                $"{basePath}/Editor/Icons",
                $"{basePath}/Resources"
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder);
                    string newFolder = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, newFolder);
                    Debug.Log($"Created folder: {folder}");
                }
            }

            AssetDatabase.Refresh();
        }

        static void CreatePropDatabase()
        {
            string dbPath = "Assets/RubyMMO/Resources/PropDatabase.asset";

            if (!File.Exists(dbPath))
            {
                PropDatabase database = ScriptableObject.CreateInstance<PropDatabase>();
                AssetDatabase.CreateAsset(database, dbPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created Prop Database");
            }
        }

        static void CreateDefaultMaterials()
        {
            // Grass material
            CreateMaterial("Grass", new Color(0.3f, 0.6f, 0.2f), "Assets/RubyMMO/Materials/Terrain/");

            // Stone material  
            CreateMaterial("Stone", new Color(0.5f, 0.5f, 0.5f), "Assets/RubyMMO/Materials/Terrain/");

            // Wood material
            CreateMaterial("Wood", new Color(0.4f, 0.25f, 0.1f), "Assets/RubyMMO/Materials/Props/");

            // Water material (transparent)
            Material water = CreateMaterial("Water", new Color(0.2f, 0.4f, 0.6f, 0.8f), "Assets/RubyMMO/Materials/Terrain/");
            if (water != null)
            {
                water.SetFloat("_Mode", 3); // Transparent
                water.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                water.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                water.SetInt("_ZWrite", 0);
                water.DisableKeyword("_ALPHATEST_ON");
                water.EnableKeyword("_ALPHABLEND_ON");
                water.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                water.renderQueue = 3000;
            }
        }

        static Material CreateMaterial(string name, Color color, string path)
        {
            string fullPath = $"{path}Mat_{name}.mat";

            if (!File.Exists(fullPath))
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = color;

                AssetDatabase.CreateAsset(mat, fullPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created material: {name}");
                return mat;
            }
            return null;
        }

        static void CreateZoneTemplate()
        {
            string templatePath = "Assets/RubyMMO/Zones/ZoneTemplate.asset";

            if (!File.Exists(templatePath))
            {
                ZoneData template = ScriptableObject.CreateInstance<ZoneData>();
                template.zoneName = "Template Zone";
                template.zoneID = 1000;
                template.zoneType = ZoneType.Outdoor;
                template.zoneSize = new Vector2(256, 256);
                template.ambientColor = new Color(0.5f, 0.5f, 0.5f);
                template.fogColor = new Color(0.7f, 0.8f, 0.9f);
                template.fogDensity = 0.01f;

                AssetDatabase.CreateAsset(template, templatePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created zone template");
            }
        }

        static void CreateExampleProps()
        {
            // Tree
            GameObject tree = CreateTreeProp();
            SaveAsPrefab(tree, "Assets/RubyMMO/Props/Nature/Prop_Tree_Basic.prefab");

            // Rock
            GameObject rock = CreateRockProp();
            SaveAsPrefab(rock, "Assets/RubyMMO/Props/Nature/Prop_Rock_Basic.prefab");

            // House
            GameObject house = CreateHouseProp();
            SaveAsPrefab(house, "Assets/RubyMMO/Props/Buildings/Prop_House_Basic.prefab");

            // Lamp post
            GameObject lamp = CreateLampProp();
            SaveAsPrefab(lamp, "Assets/RubyMMO/Props/Lights/Prop_LampPost.prefab");
        }

        static GameObject CreateTreeProp()
        {
            GameObject tree = new GameObject("Tree");

            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.parent = tree.transform;
            trunk.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            trunk.transform.localPosition = new Vector3(0, 1, 0);

            // Leaves
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.parent = tree.transform;
            leaves.transform.localScale = new Vector3(2f, 1.5f, 2f);
            leaves.transform.localPosition = new Vector3(0, 2.5f, 0);

            // Materials
            trunk.GetComponent<Renderer>().material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RubyMMO/Materials/Props/Mat_Wood.mat");
            leaves.GetComponent<Renderer>().material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RubyMMO/Materials/Terrain/Mat_Grass.mat");

            return tree;
        }

        static GameObject CreateRockProp()
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.localScale = new Vector3(1.5f, 1f, 1.2f);

            rock.GetComponent<Renderer>().material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RubyMMO/Materials/Terrain/Mat_Stone.mat");

            return rock;
        }

        static GameObject CreateHouseProp()
        {
            GameObject house = new GameObject("House");

            // Base
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Base";
            baseObj.transform.parent = house.transform;
            baseObj.transform.localScale = new Vector3(4f, 3f, 4f);
            baseObj.transform.localPosition = new Vector3(0, 1.5f, 0);

            // Roof
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.parent = house.transform;
            roof.transform.localScale = new Vector3(5f, 1f, 5f);
            roof.transform.localPosition = new Vector3(0, 3.5f, 0);
            roof.transform.localRotation = Quaternion.Euler(0, 45, 0);

            // Door
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.parent = house.transform;
            door.transform.localScale = new Vector3(1f, 2f, 0.1f);
            door.transform.localPosition = new Vector3(0, 1f, 2f);

            return house;
        }

        static GameObject CreateLampProp()
        {
            GameObject lamp = new GameObject("LampPost");

            // Post
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.parent = lamp.transform;
            post.transform.localScale = new Vector3(0.2f, 2f, 0.2f);
            post.transform.localPosition = new Vector3(0, 1f, 0);

            // Light holder
            GameObject holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            holder.name = "Holder";
            holder.transform.parent = lamp.transform;
            holder.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            holder.transform.localPosition = new Vector3(0, 2.5f, 0);

            // Add Light component
            GameObject lightObj = new GameObject("Light");
            lightObj.transform.parent = lamp.transform;
            lightObj.transform.localPosition = new Vector3(0, 2.5f, 0);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.9f, 0.7f);

            return lamp;
        }

        static void SaveAsPrefab(GameObject obj, string path)
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Save prefab - HIER WAR DER FEHLER
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
            UnityEngine.Object.DestroyImmediate(obj);

            Debug.Log($"Created prefab: {Path.GetFileName(path)}");

            // Add to prop database
            PropDatabase db = PropDatabase.GetDatabase();
            if (db != null)
            {
                // This would require adding the prop to the database
                // For now, just log
                Debug.Log($"Remember to add {prefab.name} to the Prop Database!");
            }
        }

        [MenuItem("Ruby MMO/Documentation/Zone Editor Guide")]
        public static void OpenDocumentation()
        {
            EditorUtility.DisplayDialog("Zone Editor Guide",
                "ZONE EDITOR - Quick Start:\n\n" +
                "1. CREATE NEW ZONE:\n" +
                "   • Click 'New Zone' in toolbar\n" +
                "   • Choose a template (Forest, Desert, etc.)\n" +
                "   • Set zone name and size\n\n" +
                "2. PLACE PROPS:\n" +
                "   • Browse Prop Library\n" +
                "   • Click prop to enter placement mode\n" +
                "   • Click in scene to place\n" +
                "   • Press R to rotate, ESC to cancel\n\n" +
                "3. TERRAIN TOOLS:\n" +
                "   • Create terrain for outdoor zones\n" +
                "   • Use paint mode for textures\n" +
                "   • Adjust brush size with slider\n\n" +
                "4. SPAWN POINTS:\n" +
                "   • Add player/NPC spawn points\n" +
                "   • Position them in scene view\n" +
                "   • Set respawn timers\n\n" +
                "5. SAVE YOUR WORK:\n" +
                "   • Click 'Save Zone' regularly\n" +
                "   • Zones saved as ScriptableObjects\n\n" +
                "HOTKEYS:\n" +
                "   • G: Toggle grid snap\n" +
                "   • R: Rotate prop\n" +
                "   • ESC: Cancel placement\n" +
                "   • Shift+Click: Multi-place",
                "Got it!");
        }
    }
}
#endif