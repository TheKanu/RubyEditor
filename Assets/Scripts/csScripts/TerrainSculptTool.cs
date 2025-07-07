using UnityEngine;
using System.Collections.Generic;

namespace RubyEditor.Tools
{
    public class TerrainSculptTool : EditorTool
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float brushFalloff = 0.5f;
        [SerializeField] private AnimationCurve brushCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Tool Mode")]
        [SerializeField] private TerrainToolMode currentMode = TerrainToolMode.Raise;

        [Header("Texture Painting")]
        [SerializeField] private int selectedTextureIndex = 0;
        [SerializeField] private List<TerrainLayer> terrainLayers = new List<TerrainLayer>();

        [Header("Smoothing")]
        [SerializeField] private int smoothIterations = 1;

        [Header("Visual")]
        [SerializeField] private Color brushColor = new Color(0, 1, 0, 0.5f);
        [SerializeField] private GameObject brushVisualizer;

        // Private
        private Terrain activeTerrain;
        private TerrainData terrainData;
        private float[,] heights;
        private float[,,] alphamaps;
        private bool isPainting = false;
        private Vector3 lastBrushPosition;

        public enum TerrainToolMode
        {
            Raise,
            Lower,
            Smooth,
            Flatten,
            Paint,
            Path
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();

            // Find terrain
            activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                terrainData = activeTerrain.terrainData;
                LoadTerrainLayers();
            }

            CreateBrushVisualizer();
        }

        public override void OnToolDeactivated()
        {
            base.OnToolDeactivated();

            if (brushVisualizer != null)
            {
                GameObject.Destroy(brushVisualizer);
            }
        }

        public override void UpdateTool()
        {
            if (activeTerrain == null)
            {
                FindTerrain();
                return;
            }

            HandleBrushVisualization();
            HandleTerrainEditing();
            HandleShortcuts();
        }

        void FindTerrain()
        {
            activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                terrainData = activeTerrain.terrainData;
                LoadTerrainLayers();
            }
        }

        void HandleBrushVisualization()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.GetComponent<Terrain>() != null)
                {
                    brushVisualizer.SetActive(true);
                    brushVisualizer.transform.position = hit.point;
                    brushVisualizer.transform.localScale = Vector3.one * brushSize * 2;

                    // Update color based on mode
                    UpdateBrushColor();
                }
                else
                {
                    brushVisualizer.SetActive(false);
                }
            }
        }

        void HandleTerrainEditing()
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    Terrain terrain = hit.collider.GetComponent<Terrain>();
                    if (terrain != null)
                    {
                        isPainting = true;

                        switch (currentMode)
                        {
                            case TerrainToolMode.Raise:
                                RaiseTerrain(hit.point);
                                break;
                            case TerrainToolMode.Lower:
                                LowerTerrain(hit.point);
                                break;
                            case TerrainToolMode.Smooth:
                                SmoothTerrain(hit.point);
                                break;
                            case TerrainToolMode.Flatten:
                                FlattenTerrain(hit.point);
                                break;
                            case TerrainToolMode.Paint:
                                PaintTexture(hit.point);
                                break;
                            case TerrainToolMode.Path:
                                CreatePath(hit.point);
                                break;
                        }

                        lastBrushPosition = hit.point;
                    }
                }
            }
            else
            {
                isPainting = false;
            }
        }

        void RaiseTerrain(Vector3 worldPos)
        {
            int x, z;
            float strength;

            // Get terrain coordinates
            WorldToTerrainCoords(worldPos, out x, out z);

            // Get heights
            int size = (int)(brushSize / terrainData.size.x * terrainData.heightmapResolution);
            int halfSize = size / 2;

            heights = terrainData.GetHeights(x - halfSize, z - halfSize, size, size);

            // Modify heights
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(halfSize, halfSize));
                    if (distance <= halfSize)
                    {
                        float falloff = brushCurve.Evaluate(distance / halfSize);
                        strength = brushStrength * (1 - falloff * brushFalloff) * Time.deltaTime;
                        heights[j, i] += strength;
                        heights[j, i] = Mathf.Clamp01(heights[j, i]);
                    }
                }
            }

            // Apply heights
            terrainData.SetHeights(x - halfSize, z - halfSize, heights);

            // Mark as changed
            if (RubyEditorEnhanced.Instance != null)
            {
                RubyEditorEnhanced.Instance.hasUnsavedChanges = true;
            }
        }

        void LowerTerrain(Vector3 worldPos)
        {
            int x, z;
            WorldToTerrainCoords(worldPos, out x, out z);

            int size = (int)(brushSize / terrainData.size.x * terrainData.heightmapResolution);
            int halfSize = size / 2;

            heights = terrainData.GetHeights(x - halfSize, z - halfSize, size, size);

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(halfSize, halfSize));
                    if (distance <= halfSize)
                    {
                        float falloff = brushCurve.Evaluate(distance / halfSize);
                        float strength = brushStrength * (1 - falloff * brushFalloff) * Time.deltaTime;
                        heights[j, i] -= strength;
                        heights[j, i] = Mathf.Clamp01(heights[j, i]);
                    }
                }
            }

            terrainData.SetHeights(x - halfSize, z - halfSize, heights);
            RubyEditorEnhanced.Instance.hasUnsavedChanges = true;
        }

        void SmoothTerrain(Vector3 worldPos)
        {
            int x, z;
            WorldToTerrainCoords(worldPos, out x, out z);

            int size = (int)(brushSize / terrainData.size.x * terrainData.heightmapResolution);
            int halfSize = size / 2;

            heights = terrainData.GetHeights(x - halfSize, z - halfSize, size, size);

            // Smooth iterations
            for (int iteration = 0; iteration < smoothIterations; iteration++)
            {
                float[,] smoothedHeights = new float[size, size];

                for (int i = 1; i < size - 1; i++)
                {
                    for (int j = 1; j < size - 1; j++)
                    {
                        float distance = Vector2.Distance(new Vector2(i, j), new Vector2(halfSize, halfSize));
                        if (distance <= halfSize)
                        {
                            // Average neighboring heights
                            float avgHeight = 0;
                            int count = 0;

                            for (int di = -1; di <= 1; di++)
                            {
                                for (int dj = -1; dj <= 1; dj++)
                                {
                                    avgHeight += heights[j + dj, i + di];
                                    count++;
                                }
                            }

                            avgHeight /= count;

                            float falloff = brushCurve.Evaluate(distance / halfSize);
                            float blend = brushStrength * (1 - falloff * brushFalloff) * Time.deltaTime * 10;
                            smoothedHeights[j, i] = Mathf.Lerp(heights[j, i], avgHeight, blend);
                        }
                        else
                        {
                            smoothedHeights[j, i] = heights[j, i];
                        }
                    }
                }

                heights = smoothedHeights;
            }

            terrainData.SetHeights(x - halfSize, z - halfSize, heights);
            RubyEditorEnhanced.Instance.hasUnsavedChanges = true;
        }

        void FlattenTerrain(Vector3 worldPos)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Sample height at click position
                int sx, sz;
                WorldToTerrainCoords(worldPos, out sx, out sz);
                float targetHeight = terrainData.GetHeight(sx, sz) / terrainData.size.y;

                PlayerPrefs.SetFloat("FlattenHeight", targetHeight);
            }

            float flattenHeight = PlayerPrefs.GetFloat("FlattenHeight", 0.5f);

            int x, z;
            WorldToTerrainCoords(worldPos, out x, out z);

            int size = (int)(brushSize / terrainData.size.x * terrainData.heightmapResolution);
            int halfSize = size / 2;

            heights = terrainData.GetHeights(x - halfSize, z - halfSize, size, size);

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(halfSize, halfSize));
                    if (distance <= halfSize)
                    {
                        float falloff = brushCurve.Evaluate(distance / halfSize);
                        float strength = brushStrength * (1 - falloff * brushFalloff) * Time.deltaTime * 5;
                        heights[j, i] = Mathf.Lerp(heights[j, i], flattenHeight, strength);
                    }
                }
            }

            terrainData.SetHeights(x - halfSize, z - halfSize, heights);
            RubyEditorEnhanced.Instance.hasUnsavedChanges = true;
        }

        void PaintTexture(Vector3 worldPos)
        {
            int x, z;
            WorldToTerrainCoords(worldPos, out x, out z);

            int size = (int)(brushSize / terrainData.size.x * terrainData.alphamapResolution);
            int halfSize = size / 2;

            // Get alphamaps
            alphamaps = terrainData.GetAlphamaps(x - halfSize, z - halfSize, size, size);
            int numTextures = alphamaps.GetLength(2);

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(halfSize, halfSize));
                    if (distance <= halfSize)
                    {
                        float falloff = brushCurve.Evaluate(distance / halfSize);
                        float strength = brushStrength * (1 - falloff * brushFalloff);

                        // Normalize other textures
                        float sum = 0;
                        for (int t = 0; t < numTextures; t++)
                        {
                            if (t == selectedTextureIndex)
                            {
                                alphamaps[j, i, t] = Mathf.Lerp(alphamaps[j, i, t], 1, strength);
                            }
                            else
                            {
                                alphamaps[j, i, t] = Mathf.Lerp(alphamaps[j, i, t], 0, strength);
                            }
                            sum += alphamaps[j, i, t];
                        }

                        // Normalize
                        for (int t = 0; t < numTextures; t++)
                        {
                            alphamaps[j, i, t] /= sum;
                        }
                    }
                }
            }

            terrainData.SetAlphamaps(x - halfSize, z - halfSize, alphamaps);
            RubyEditorEnhanced.Instance.hasUnsavedChanges = true;
        }

        void CreatePath(Vector3 worldPos)
        {
            // Flatten terrain for path
            FlattenTerrain(worldPos);

            // Paint path texture
            selectedTextureIndex = 1; // Assuming index 1 is path texture
            PaintTexture(worldPos);
        }

        void WorldToTerrainCoords(Vector3 worldPos, out int x, out int z)
        {
            Vector3 terrainPos = worldPos - activeTerrain.transform.position;
            Vector3 normalizedPos = new Vector3(
                terrainPos.x / terrainData.size.x,
                0,
                terrainPos.z / terrainData.size.z
            );

            x = (int)(normalizedPos.x * terrainData.heightmapResolution);
            z = (int)(normalizedPos.z * terrainData.heightmapResolution);

            // Clamp to valid range
            x = Mathf.Clamp(x, 0, terrainData.heightmapResolution - 1);
            z = Mathf.Clamp(z, 0, terrainData.heightmapResolution - 1);
        }

        void HandleShortcuts()
        {
            // Brush size
            if (Input.GetKey(KeyCode.LeftBracket))
            {
                brushSize = Mathf.Max(1, brushSize - Time.deltaTime * 10);
            }
            if (Input.GetKey(KeyCode.RightBracket))
            {
                brushSize = Mathf.Min(50, brushSize + Time.deltaTime * 10);
            }

            // Brush strength
            if (Input.GetKey(KeyCode.Minus))
            {
                brushStrength = Mathf.Max(0, brushStrength - Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.Equals))
            {
                brushStrength = Mathf.Min(1, brushStrength + Time.deltaTime);
            }

            // Mode switching
            if (Input.GetKeyDown(KeyCode.Alpha1)) currentMode = TerrainToolMode.Raise;
            if (Input.GetKeyDown(KeyCode.Alpha2)) currentMode = TerrainToolMode.Lower;
            if (Input.GetKeyDown(KeyCode.Alpha3)) currentMode = TerrainToolMode.Smooth;
            if (Input.GetKeyDown(KeyCode.Alpha4)) currentMode = TerrainToolMode.Flatten;
            if (Input.GetKeyDown(KeyCode.Alpha5)) currentMode = TerrainToolMode.Paint;
            if (Input.GetKeyDown(KeyCode.Alpha6)) currentMode = TerrainToolMode.Path;

            // Texture selection
            if (currentMode == TerrainToolMode.Paint)
            {
                if (Input.GetAxis("Mouse ScrollWheel") > 0)
                {
                    selectedTextureIndex = (selectedTextureIndex + 1) % terrainLayers.Count;
                }
                else if (Input.GetAxis("Mouse ScrollWheel") < 0)
                {
                    selectedTextureIndex--;
                    if (selectedTextureIndex < 0) selectedTextureIndex = terrainLayers.Count - 1;
                }
            }
        }

        void CreateBrushVisualizer()
        {
            brushVisualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            brushVisualizer.name = "BrushVisualizer";

            // Remove collider
            GameObject.Destroy(brushVisualizer.GetComponent<Collider>());

            // Make transparent
            Renderer renderer = brushVisualizer.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Transparent/Diffuse"));
            mat.color = brushColor;
            renderer.material = mat;

            brushVisualizer.SetActive(false);
        }

        void UpdateBrushColor()
        {
            Color color = brushColor;

            switch (currentMode)
            {
                case TerrainToolMode.Raise:
                    color = Color.green;
                    break;
                case TerrainToolMode.Lower:
                    color = Color.red;
                    break;
                case TerrainToolMode.Smooth:
                    color = Color.blue;
                    break;
                case TerrainToolMode.Flatten:
                    color = Color.yellow;
                    break;
                case TerrainToolMode.Paint:
                    color = new Color(1f, 0.5f, 0f);
                    break;
                case TerrainToolMode.Path:
                    color = Color.gray;
                    break;
            }

            color.a = 0.5f;
            brushVisualizer.GetComponent<Renderer>().material.color = color;
        }

        void LoadTerrainLayers()
        {
            if (terrainData == null) return;

            terrainLayers.Clear();
            terrainLayers.AddRange(terrainData.terrainLayers);

            // If no layers, create defaults
            if (terrainLayers.Count == 0)
            {
                CreateDefaultTerrainLayers();
            }
        }

        void CreateDefaultTerrainLayers()
        {
            // Create grass layer
            TerrainLayer grassLayer = new TerrainLayer();
            grassLayer.diffuseTexture = CreateSolidColorTexture(new Color(0.3f, 0.6f, 0.2f));
            grassLayer.name = "Grass";
            terrainLayers.Add(grassLayer);

            // Create dirt layer
            TerrainLayer dirtLayer = new TerrainLayer();
            dirtLayer.diffuseTexture = CreateSolidColorTexture(new Color(0.5f, 0.35f, 0.2f));
            dirtLayer.name = "Dirt";
            terrainLayers.Add(dirtLayer);

            // Create stone layer
            TerrainLayer stoneLayer = new TerrainLayer();
            stoneLayer.diffuseTexture = CreateSolidColorTexture(new Color(0.5f, 0.5f, 0.5f));
            stoneLayer.name = "Stone";
            terrainLayers.Add(stoneLayer);

            // Apply to terrain
            terrainData.terrainLayers = terrainLayers.ToArray();
        }

        Texture2D CreateSolidColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        public void SetBrushSize(float size)
        {
            brushSize = Mathf.Clamp(size, 1f, 50f);
        }

        public void SetBrushStrength(float strength)
        {
            brushStrength = Mathf.Clamp01(strength);
        }

        public void SetToolMode(TerrainToolMode mode)
        {
            currentMode = mode;
        }

        public void SetSelectedTexture(int index)
        {
            selectedTextureIndex = Mathf.Clamp(index, 0, terrainLayers.Count - 1);
        }

        void OnDrawGizmos()
        {
            if (!isActive || !isPainting) return;

            // Draw brush influence area
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(lastBrushPosition, brushSize);
        }
    }
}