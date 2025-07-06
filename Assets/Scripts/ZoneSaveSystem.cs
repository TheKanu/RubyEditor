using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace RubyEditor.Data
{
    [System.Serializable]
    public class ZoneData
    {
        public string zoneName;
        public string zoneDescription;
        public Vector2Int zoneSize = new Vector2Int(100, 100);
        public float[,] heightmap;
        public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
        public List<TerrainTextureData> terrainTextures = new List<TerrainTextureData>();
        public EnvironmentSettings environment = new EnvironmentSettings();
        public DateTime lastModified;
        public string author;
        public int version = 1;
    }

    [System.Serializable]
    public class PlacedObjectData
    {
        public string prefabPath;
        public string objectName;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Dictionary<string, object> customProperties = new Dictionary<string, object>();
    }

    [System.Serializable]
    public class TerrainTextureData
    {
        public string texturePath;
        public int textureIndex;
        public float tiling = 15f;
        public Vector2 offset = Vector2.zero;
    }

    [System.Serializable]
    public class EnvironmentSettings
    {
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f;
        public Color ambientLight = Color.white;
        public float sunIntensity = 1f;
        public Vector3 sunRotation = new Vector3(50, -30, 0);
    }

    public class ZoneSaveSystem : MonoBehaviour
    {
        [Header("Save Settings")]
        [SerializeField] private string saveDirectory = "Zones";
        [SerializeField] private bool useCompression = true;
        [SerializeField] private bool createBackups = true;
        [SerializeField] private int maxBackups = 5;

        private string fullSavePath;

        public static ZoneSaveSystem Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeSaveSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void InitializeSaveSystem()
        {
            // Create save directory
            fullSavePath = Path.Combine(Application.streamingAssetsPath, saveDirectory);
            if (!Directory.Exists(fullSavePath))
            {
                Directory.CreateDirectory(fullSavePath);
                Debug.Log($"Created save directory: {fullSavePath}");
            }
        }

        public void SaveZone(string zoneName)
        {
            try
            {
                ZoneData zoneData = CollectZoneData(zoneName);
                string json = JsonUtility.ToJson(zoneData, true);

                if (useCompression)
                {
                    json = CompressString(json);
                }

                // Create backup if file exists
                string filePath = GetZoneFilePath(zoneName);
                if (File.Exists(filePath) && createBackups)
                {
                    CreateBackup(filePath);
                }

                // Save the file
                File.WriteAllText(filePath, json);

                Debug.Log($"✅ Zone saved successfully: {zoneName}");
                ShowNotification("Zone Saved", $"Successfully saved {zoneName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to save zone: {e.Message}");
                ShowNotification("Save Failed", e.Message, NotificationType.Error);
            }
        }

        public ZoneData LoadZone(string zoneName)
        {
            try
            {
                string filePath = GetZoneFilePath(zoneName);

                if (!File.Exists(filePath))
                {
                    Debug.LogError($"Zone file not found: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);

                if (useCompression)
                {
                    json = DecompressString(json);
                }

                ZoneData zoneData = JsonUtility.FromJson<ZoneData>(json);
                ApplyZoneData(zoneData);

                Debug.Log($"✅ Zone loaded successfully: {zoneName}");
                ShowNotification("Zone Loaded", $"Successfully loaded {zoneName}");

                return zoneData;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to load zone: {e.Message}");
                ShowNotification("Load Failed", e.Message, NotificationType.Error);
                return null;
            }
        }

        ZoneData CollectZoneData(string zoneName)
        {
            ZoneData data = new ZoneData();
            data.zoneName = zoneName;
            data.lastModified = DateTime.Now;
            data.author = SystemInfo.deviceName; // Or use actual user system

            // Collect terrain data
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                TerrainData terrainData = terrain.terrainData;
                data.zoneSize = new Vector2Int(
                    (int)terrainData.size.x,
                    (int)terrainData.size.z
                );

                // Save heightmap (simplified - you'd want to optimize this)
                int resolution = terrainData.heightmapResolution;
                data.heightmap = terrainData.GetHeights(0, 0, resolution, resolution);
            }

            // Collect placed objects
            GameObject placedObjectsContainer = GameObject.Find("PlacedObjects");
            if (placedObjectsContainer != null)
            {
                foreach (Transform child in placedObjectsContainer.transform)
                {
                    PlacedObjectData objData = new PlacedObjectData();
                    objData.objectName = child.name;
                    objData.position = child.position;
                    objData.rotation = child.rotation;
                    objData.scale = child.localScale;

                    // Try to find prefab path (simplified)
                    objData.prefabPath = GetPrefabPath(child.gameObject);

                    data.placedObjects.Add(objData);
                }
            }

            // Collect environment settings
            CollectEnvironmentSettings(data.environment);

            return data;
        }

        void ApplyZoneData(ZoneData data)
        {
            // Clear existing objects
            ClearZone();

            // Apply terrain
            if (data.heightmap != null)
            {
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    terrain.terrainData.SetHeights(0, 0, data.heightmap);
                }
            }

            // Recreate objects
            GameObject container = GameObject.Find("PlacedObjects");
            if (container == null)
            {
                container = new GameObject("PlacedObjects");
            }

            foreach (PlacedObjectData objData in data.placedObjects)
            {
                // Load prefab from path
                GameObject prefab = LoadPrefabFromPath(objData.prefabPath);
                if (prefab != null)
                {
                    GameObject newObj = Instantiate(prefab, objData.position, objData.rotation);
                    newObj.transform.localScale = objData.scale;
                    newObj.name = objData.objectName;
                    newObj.transform.parent = container.transform;
                }
            }

            // Apply environment
            ApplyEnvironmentSettings(data.environment);
        }

        void ClearZone()
        {
            // Clear placed objects
            GameObject container = GameObject.Find("PlacedObjects");
            if (container != null)
            {
                foreach (Transform child in container.transform)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        void CollectEnvironmentSettings(EnvironmentSettings settings)
        {
            // Collect fog settings
            settings.fogColor = RenderSettings.fogColor;
            settings.fogDensity = RenderSettings.fogDensity;
            settings.ambientLight = RenderSettings.ambientLight;

            // Collect sun settings
            Light sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                settings.sunIntensity = sun.intensity;
                settings.sunRotation = sun.transform.eulerAngles;
            }
        }

        void ApplyEnvironmentSettings(EnvironmentSettings settings)
        {
            // Apply fog
            RenderSettings.fog = true;
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.ambientLight = settings.ambientLight;

            // Apply sun
            Light sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.intensity = settings.sunIntensity;
                sun.transform.eulerAngles = settings.sunRotation;
            }
        }

        string GetPrefabPath(GameObject obj)
        {
            // In a real implementation, you'd track the source prefab
            // For now, return a placeholder
            return "Prefabs/" + obj.name.Split('_')[0];
        }

        GameObject LoadPrefabFromPath(string path)
        {
            // In a real implementation, load from Resources or Addressables
            return Resources.Load<GameObject>(path);
        }

        void CreateBackup(string originalPath)
        {
            string backupDir = Path.Combine(Path.GetDirectoryName(originalPath), "Backups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(backupDir, $"{fileName}_backup_{timestamp}.zone");

            File.Copy(originalPath, backupPath);

            // Clean old backups
            CleanOldBackups(backupDir, fileName);
        }

        void CleanOldBackups(string backupDir, string fileName)
        {
            var backups = Directory.GetFiles(backupDir, $"{fileName}_backup_*.zone");
            if (backups.Length > maxBackups)
            {
                Array.Sort(backups);
                for (int i = 0; i < backups.Length - maxBackups; i++)
                {
                    File.Delete(backups[i]);
                }
            }
        }

        string GetZoneFilePath(string zoneName)
        {
            return Path.Combine(fullSavePath, zoneName + ".zone");
        }

        // Compression helpers (simplified)
        string CompressString(string text)
        {
            // In production, use proper compression
            return text;
        }

        string DecompressString(string compressedText)
        {
            // In production, use proper decompression
            return compressedText;
        }

        public List<string> GetAvailableZones()
        {
            List<string> zones = new List<string>();
            string[] files = Directory.GetFiles(fullSavePath, "*.zone");

            foreach (string file in files)
            {
                zones.Add(Path.GetFileNameWithoutExtension(file));
            }

            return zones;
        }

        void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            // In Unity Editor, you'd use EditorUtility.DisplayDialog
            // For runtime, implement a UI notification system
            Debug.Log($"[{type}] {title}: {message}");
        }

        public enum NotificationType
        {
            Info,
            Warning,
            Error,
            Success
        }
    }
}