#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace RubyMMO.Editor
{
    [CreateAssetMenu(fileName = "PropDatabase", menuName = "Ruby MMO/Prop Database")]
    public class PropDatabase : ScriptableObject
    {
        [System.Serializable]
        public class PropEntry
        {
            public string id;
            public string displayName;
            public GameObject prefab;
            public Texture2D thumbnail;
            public PropCategory category = PropCategory.Props;
            public string[] tags;
            public Vector3 defaultScale = Vector3.one;
            public bool snapToGround = true;
            public float placementOffset = 0f;

            // Preview settings
            public Vector3 previewRotation = Vector3.zero;
            public float previewDistance = 3f;
        }

        [SerializeField] private List<PropEntry> props = new List<PropEntry>();
        [SerializeField] private string assetPath = "Assets/RubyMMO/Props/";

        private Dictionary<string, PropEntry> propLookup;

        public void OnEnable()
        {
            RebuildLookup();
        }

        void RebuildLookup()
        {
            propLookup = new Dictionary<string, PropEntry>();
            foreach (var prop in props)
            {
                if (!string.IsNullOrEmpty(prop.id))
                {
                    propLookup[prop.id] = prop;
                }
            }
        }

        public PropEntry GetProp(string id)
        {
            if (propLookup == null) RebuildLookup();
            return propLookup.ContainsKey(id) ? propLookup[id] : null;
        }

        public List<PropEntry> GetPropsInCategory(PropCategory category)
        {
            return props.Where(p => p.category == category).ToList();
        }

        public List<PropEntry> SearchProps(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm)) return props;

            searchTerm = searchTerm.ToLower();
            return props.Where(p =>
                p.displayName.ToLower().Contains(searchTerm) ||
                p.id.ToLower().Contains(searchTerm) ||
                (p.tags != null && p.tags.Any(t => t.ToLower().Contains(searchTerm)))
            ).ToList();
        }

        public void AutoGenerateThumbnails()
        {
            foreach (var prop in props)
            {
                if (prop.prefab != null && prop.thumbnail == null)
                {
                    prop.thumbnail = GenerateThumbnail(prop.prefab);
                }
            }
            EditorUtility.SetDirty(this);
        }

        Texture2D GenerateThumbnail(GameObject prefab)
        {
            // Create preview
            var preview = AssetPreview.GetAssetPreview(prefab);
            if (preview != null)
            {
                // Save as asset
                string path = $"{assetPath}Thumbnails/{prefab.name}_thumb.png";
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                byte[] bytes = preview.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        public static PropDatabase GetDatabase()
        {
            var db = Resources.Load<PropDatabase>("PropDatabase");
            if (db == null)
            {
                // Try to find in project
                string[] guids = AssetDatabase.FindAssets("t:PropDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    db = AssetDatabase.LoadAssetAtPath<PropDatabase>(path);
                }
            }
            return db;
        }
    }

    // Custom Editor
    [CustomEditor(typeof(PropDatabase))]
    public class PropDatabaseEditor : Editor
    {
        private PropDatabase database;
        private Vector2 scrollPos;
        private string searchFilter = "";
        private PropCategory filterCategory = PropCategory.All;
        private bool showImportTools = false;

        void OnEnable()
        {
            database = (PropDatabase)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Prop Database", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Toolbar
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Prop", GUILayout.Width(100)))
            {
                AddNewProp();
            }

            if (GUILayout.Button("Auto Scan", GUILayout.Width(100)))
            {
                AutoScanForProps();
            }

            if (GUILayout.Button("Generate Thumbs", GUILayout.Width(120)))
            {
                database.AutoGenerateThumbnails();
            }

            GUILayout.FlexibleSpace();

            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Category filter
            filterCategory = (PropCategory)EditorGUILayout.EnumPopup("Filter Category", filterCategory);

            // Import tools
            showImportTools = EditorGUILayout.Foldout(showImportTools, "Import Tools");
            if (showImportTools)
            {
                DrawImportTools();
            }

            EditorGUILayout.Space();

            // Props list
            var props = database.SearchProps(searchFilter);
            if (filterCategory != PropCategory.All)
            {
                props = props.Where(p => p.category == filterCategory).ToList();
            }

            EditorGUILayout.LabelField($"Props ({props.Count})", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(400));

            foreach (var prop in props)
            {
                DrawPropEntry(prop);
            }

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(database);
            }
        }

        void DrawPropEntry(PropDatabase.PropEntry prop)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Thumbnail
            if (prop.thumbnail != null)
            {
                GUILayout.Label(prop.thumbnail, GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Box("No Thumb", GUILayout.Width(64), GUILayout.Height(64));
            }

            EditorGUILayout.BeginVertical();

            // ID and Name
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
            prop.id = EditorGUILayout.TextField(prop.id, GUILayout.Width(150));
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Name:", GUILayout.Width(45));
            prop.displayName = EditorGUILayout.TextField(prop.displayName);
            EditorGUILayout.EndHorizontal();

            // Prefab and Category
            EditorGUILayout.BeginHorizontal();
            prop.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab:", prop.prefab, typeof(GameObject), false);
            prop.category = (PropCategory)EditorGUILayout.EnumPopup(prop.category, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Settings
            EditorGUILayout.BeginHorizontal();
            prop.snapToGround = EditorGUILayout.Toggle("Snap Ground", prop.snapToGround, GUILayout.Width(100));
            EditorGUILayout.LabelField("Scale:", GUILayout.Width(40));
            prop.defaultScale = EditorGUILayout.Vector3Field("", prop.defaultScale);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Delete button
            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(64)))
            {
                RemoveProp(prop);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        void DrawImportTools()
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Batch import props from a folder", MessageType.Info);

            if (GUILayout.Button("Import from Folder"))
            {
                string folder = EditorUtility.OpenFolderPanel("Select Props Folder", "Assets", "");
                if (!string.IsNullOrEmpty(folder))
                {
                    ImportPropsFromFolder(folder);
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Default Categories"))
            {
                CreateDefaultProps();
            }

            EditorGUI.indentLevel--;
        }

        void AddNewProp()
        {
            var newProp = new PropDatabase.PropEntry();
            newProp.id = $"prop_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            newProp.displayName = "New Prop";

            var props = serializedObject.FindProperty("props");
            props.InsertArrayElementAtIndex(props.arraySize);
            var element = props.GetArrayElementAtIndex(props.arraySize - 1);

            serializedObject.ApplyModifiedProperties();
        }

        void RemoveProp(PropDatabase.PropEntry prop)
        {
            var props = serializedObject.FindProperty("props");
            for (int i = 0; i < props.arraySize; i++)
            {
                var element = props.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("id").stringValue == prop.id)
                {
                    props.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        void AutoScanForProps()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int addedCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && !database.GetPropsInCategory(PropCategory.All).Any(p => p.prefab == prefab))
                {
                    var prop = new PropDatabase.PropEntry();
                    prop.id = $"prop_{Path.GetFileNameWithoutExtension(path).ToLower()}";
                    prop.displayName = prefab.name;
                    prop.prefab = prefab;
                    prop.category = GuessCategory(path);

                    var props = serializedObject.FindProperty("props");
                    props.InsertArrayElementAtIndex(props.arraySize);

                    addedCount++;
                }
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Added {addedCount} new props to database");
        }

        PropCategory GuessCategory(string path)
        {
            path = path.ToLower();
            if (path.Contains("tree") || path.Contains("plant") || path.Contains("rock")) return PropCategory.Nature;
            if (path.Contains("building") || path.Contains("house") || path.Contains("wall")) return PropCategory.Buildings;
            if (path.Contains("light") || path.Contains("lamp")) return PropCategory.Lights;
            if (path.Contains("fx") || path.Contains("effect")) return PropCategory.Effects;
            return PropCategory.Props;
        }

        void ImportPropsFromFolder(string folderPath)
        {
            // Convert to relative path
            if (folderPath.StartsWith(Application.dataPath))
            {
                folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    var prop = new PropDatabase.PropEntry();
                    prop.id = $"prop_{Path.GetFileNameWithoutExtension(path).ToLower()}";
                    prop.displayName = prefab.name;
                    prop.prefab = prefab;
                    prop.category = GuessCategory(path);

                    var props = serializedObject.FindProperty("props");
                    props.InsertArrayElementAtIndex(props.arraySize);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void CreateDefaultProps()
        {
            // Create basic primitive props for testing
            CreatePrimitiveProp("Cube", PrimitiveType.Cube, PropCategory.Props);
            CreatePrimitiveProp("Sphere", PrimitiveType.Sphere, PropCategory.Props);
            CreatePrimitiveProp("Cylinder", PrimitiveType.Cylinder, PropCategory.Props);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        void CreatePrimitiveProp(string name, PrimitiveType type, PropCategory category)
        {
            // Create prefab
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = $"Prop_{name}";

            string prefabPath = $"Assets/RubyMMO/Props/Primitives/{obj.name}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
            DestroyImmediate(obj);

            // Add to database
            var prop = new PropDatabase.PropEntry();
            prop.id = $"prop_{name.ToLower()}";
            prop.displayName = name;
            prop.prefab = prefab;
            prop.category = category;

            var props = serializedObject.FindProperty("props");
            props.InsertArrayElementAtIndex(props.arraySize);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif