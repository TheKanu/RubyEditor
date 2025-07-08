using UnityEngine;
using System.Collections.Generic;
using RubyEditor.Core;

namespace RubyEditor.Tools
{
    public class TerrainPaintTool : EditorTool
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float brushRotation = 0f;
        [SerializeField] private AnimationCurve brushFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Texture Settings")]
        [SerializeField] private int selectedTextureIndex = 0;
        [SerializeField] private List<TerrainLayer> terrainLayers = new List<TerrainLayer>();

        [Header("Paint Mode")]
        [SerializeField] private PaintMode currentMode = PaintMode.Paint;

        [Header("Visual Settings")]
        [SerializeField] private bool showTexturePreview = true;
        [SerializeField] private float previewSize = 64f;

        // Terrain references
        private Terrain targetTerrain;
        private TerrainData terrainData;
        private int alphamapResolution;
        private float[,,] alphamaps;

        // Brush visualization
        private GameObject brushVisualizer;
        private LineRenderer brushCircle;
        private Material previewMaterial;

        // Undo system
        private Stack<TextureUndoState> undoStack = new Stack<TextureUndoState>();
        private const int maxUndoSteps = 10;

        public enum PaintMode
        {
            Paint,      // Normal painting
            Erase,      // Remove texture
            Smooth,     // Smooth between textures
            Replace     // Replace one texture with another
        }

        private class TextureUndoState
        {
            public float[,,] alphamaps;
            public int xBase, yBase, width, height;

            public TextureUndoState(float[,,] alpha, int x, int y, int w, int h)
            {
                alphamaps = (float[,,])alpha.Clone();
                xBase = x;
                yBase = y;
                width = w;
                height = h;
            }
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();
            FindTerrain();
            CreateBrushVisualizer();
            LoadTerrainTextures();
        }

        public override void OnToolDeactivated()
        {
            base.OnToolDeactivated();
            if (brushVisualizer != null)
            {
                DestroyImmediate(brushVisualizer);
            }
        }

        void FindTerrain()
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                targetTerrain = FindObjectOfType<Terrain>();
            }

            if (targetTerrain != null)
            {
                terrainData = targetTerrain.terrainData;
                alphamapResolution = terrainData.alphamapResolution;
                RefreshAlphamaps();
            }
        }

        void RefreshAlphamaps()
        {
            if (terrainData != null)
            {
                alphamaps = terrainData.GetAlphamaps(0, 0, alphamapResolution, alphamapResolution);
            }
        }

        void LoadTerrainTextures()
        {
            if (terrainData == null) return;

            terrainLayers.Clear();
            terrainLayers.AddRange(terrainData.terrainLayers);

            // If no layers, create defaults
            if (terrainLayers.Count == 0)
            {
                CreateDefaultTextures();
            }
        }

        void CreateDefaultTextures()
        {
            // Create basic terrain layers
            string[] defaultTextures = { "Grass", "Dirt", "Rock", "Sand" };
            Color[] defaultColors = {
                new Color(0.3f, 0.6f, 0.2f),  // Grass
                new Color(0.4f, 0.3f, 0.2f),  // Dirt
                new Color(0.5f, 0.5f, 0.5f),  // Rock
                new Color(0.8f, 0.7f, 0.5f)   // Sand
            };

            List<TerrainLayer> newLayers = new List<TerrainLayer>();

            for (int i = 0; i < defaultTextures.Length; i++)
            {
                TerrainLayer layer = new TerrainLayer();
                layer.name = defaultTextures[i];

                // Create simple colored texture
                Texture2D tex = new Texture2D(512, 512);
                Color[] pixels = new Color[512 * 512];

                // Add some noise for variation
                for (int p = 0; p < pixels.Length; p++)
                {
                    float noise = Mathf.PerlinNoise(p % 512 * 0.01f, p / 512 * 0.01f);
                    pixels[p] = Color.Lerp(defaultColors[i] * 0.8f, defaultColors[i] * 1.2f, noise);
                }

                tex.SetPixels(pixels);
                tex.Apply();

                layer.diffuseTexture = tex;
                layer.tileSize = new Vector2(15, 15);

                newLayers.Add(layer);
            }

            terrainData.terrainLayers = newLayers.ToArray();
            terrainLayers = newLayers;
        }

        void CreateBrushVisualizer()
        {
            brushVisualizer = new GameObject("TerrainPaintBrush");
            brushCircle = brushVisualizer.AddComponent<LineRenderer>();

            brushCircle.startWidth = 0.5f;
            brushCircle.endWidth = 0.5f;
            brushCircle.material = new Material(Shader.Find("Sprites/Default"));
            brushCircle.positionCount = 33;
            brushCircle.useWorldSpace = true;

            // Create preview quad
            if (showTexturePreview)
            {
                GameObject previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                previewQuad.name = "TexturePreview";
                previewQuad.transform.parent = brushVisualizer.transform;
                previewQuad.transform.localScale = Vector3.one * previewSize;

                previewMaterial = previewQuad.GetComponent<Renderer>().material;
                Destroy(previewQuad.GetComponent<Collider>());
            }
        }

        public override void UpdateTool()
        {
            if (targetTerrain == null) return;

            HandleInput();
            UpdateBrushVisualization();

            if (Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftAlt))
            {
                PaintTerrain();
            }
        }

        void HandleInput()
        {
            // Brush size
            if (Input.GetKey(KeyCode.LeftBracket))
                brushSize = Mathf.Max(1f, brushSize - Time.deltaTime * 20f);
            if (Input.GetKey(KeyCode.RightBracket))
                brushSize = Mathf.Min(100f, brushSize + Time.deltaTime * 20f);

            // Brush strength
            if (Input.GetKey(KeyCode.Minus))
                brushStrength = Mathf.Max(0.01f, brushStrength - Time.deltaTime * 0.5f);
            if (Input.GetKey(KeyCode.Equals))
                brushStrength = Mathf.Min(1f, brushStrength + Time.deltaTime * 0.5f);

            // Texture selection
            if (Input.GetKeyDown(KeyCode.Alpha1)) selectedTextureIndex = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) selectedTextureIndex = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) selectedTextureIndex = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) selectedTextureIndex = 3;

            selectedTextureIndex = Mathf.Clamp(selectedTextureIndex, 0, terrainLayers.Count - 1);

            // Mode switching
            if (Input.GetKey(KeyCode.LeftShift)) currentMode = PaintMode.Erase;
            else if (Input.GetKey(KeyCode.LeftControl)) currentMode = PaintMode.Smooth;
            else currentMode = PaintMode.Paint;

            // Rotation
            if (Input.GetKey(KeyCode.R))
            {
                brushRotation += Input.GetAxis("Mouse X") * 5f;
            }

            // Undo
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
            {
                Undo();
            }
        }

        void UpdateBrushVisualization()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 center = hit.point;

                // Update brush circle
                for (int i = 0; i <= 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f + brushRotation * Mathf.Deg2Rad;
                    Vector3 pos = center + new Vector3(
                        Mathf.Cos(angle) * brushSize,
                        0.5f,
                        Mathf.Sin(angle) * brushSize
                    );

                    if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit circleHit, 100f))
                    {
                        brushCircle.SetPosition(i, circleHit.point + Vector3.up * 0.1f);
                    }
                }

                // Update color based on mode
                Color brushColor = GetModeColor();
                brushCircle.startColor = brushColor;
                brushCircle.endColor = brushColor;

                // Update texture preview
                if (showTexturePreview && previewMaterial != null && selectedTextureIndex < terrainLayers.Count)
                {
                    var layer = terrainLayers[selectedTextureIndex];
                    if (layer != null && layer.diffuseTexture != null)
                    {
                        previewMaterial.mainTexture = layer.diffuseTexture;
                        previewMaterial.color = new Color(1, 1, 1, 0.5f);
                    }
                }
            }
        }

        Color GetModeColor()
        {
            switch (currentMode)
            {
                case PaintMode.Paint: return new Color(0, 1, 0, 0.5f);
                case PaintMode.Erase: return new Color(1, 0, 0, 0.5f);
                case PaintMode.Smooth: return new Color(0, 0, 1, 0.5f);
                case PaintMode.Replace: return new Color(1, 1, 0, 0.5f);
                default: return Color.white;
            }
        }

        void PaintTerrain()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            if (hit.collider.GetComponent<Terrain>() != targetTerrain) return;

            // Convert world position to alphamap position
            Vector3 terrainPos = hit.point - targetTerrain.transform.position;
            Vector2 normalizedPos = new Vector2(
                terrainPos.x / terrainData.size.x,
                terrainPos.z / terrainData.size.z
            );

            int mapX = Mathf.RoundToInt(normalizedPos.x * alphamapResolution);
            int mapZ = Mathf.RoundToInt(normalizedPos.y * alphamapResolution);
            int brushSizeInSamples = Mathf.RoundToInt(brushSize * alphamapResolution / terrainData.size.x);

            // Calculate affected area
            int minX = Mathf.Max(0, mapX - brushSizeInSamples);
            int maxX = Mathf.Min(alphamapResolution, mapX + brushSizeInSamples);
            int minZ = Mathf.Max(0, mapZ - brushSizeInSamples);
            int maxZ = Mathf.Min(alphamapResolution, mapZ + brushSizeInSamples);

            int width = maxX - minX;
            int height = maxZ - minZ;

            // Get current alphamaps
            float[,,] modifiedAlpha = terrainData.GetAlphamaps(minX, minZ, width, height);

            // Store undo state
            StoreUndoState(modifiedAlpha, minX, minZ, width, height);

            // Apply painting
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int worldX = minX + x;
                    int worldZ = minZ + z;

                    float distance = Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(mapX, mapZ)
                    );

                    if (distance <= brushSizeInSamples)
                    {
                        float normalizedDistance = distance / brushSizeInSamples;
                        float influence = brushFalloff.Evaluate(normalizedDistance) * brushStrength * Time.deltaTime * 5f;

                        switch (currentMode)
                        {
                            case PaintMode.Paint:
                                PaintTexture(modifiedAlpha, x, z, influence);
                                break;
                            case PaintMode.Erase:
                                EraseTexture(modifiedAlpha, x, z, influence);
                                break;
                            case PaintMode.Smooth:
                                SmoothTextures(modifiedAlpha, x, z, influence, width, height);
                                break;
                        }

                        // Normalize weights
                        NormalizeWeights(modifiedAlpha, x, z);
                    }
                }
            }

            // Apply modified alphamaps
            terrainData.SetAlphamaps(minX, minZ, modifiedAlpha);
        }

        void PaintTexture(float[,,] alphas, int x, int z, float strength)
        {
            // Reduce other textures
            for (int i = 0; i < terrainLayers.Count; i++)
            {
                if (i == selectedTextureIndex)
                {
                    alphas[z, x, i] = Mathf.Min(1f, alphas[z, x, i] + strength);
                }
                else
                {
                    alphas[z, x, i] = Mathf.Max(0f, alphas[z, x, i] - strength / (terrainLayers.Count - 1));
                }
            }
        }

        void EraseTexture(float[,,] alphas, int x, int z, float strength)
        {
            alphas[z, x, selectedTextureIndex] = Mathf.Max(0f, alphas[z, x, selectedTextureIndex] - strength);

            // Increase base texture
            if (selectedTextureIndex != 0)
            {
                alphas[z, x, 0] = Mathf.Min(1f, alphas[z, x, 0] + strength);
            }
        }

        void SmoothTextures(float[,,] alphas, int x, int z, float strength, int width, int height)
        {
            float[] avgWeights = new float[terrainLayers.Count];
            int samples = 0;

            // Sample surrounding area
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int nz = z + dz;

                    if (nx >= 0 && nx < width && nz >= 0 && nz < height)
                    {
                        for (int layer = 0; layer < terrainLayers.Count; layer++)
                        {
                            avgWeights[layer] += alphas[nz, nx, layer];
                        }
                        samples++;
                    }
                }
            }

            // Apply averaged weights
            if (samples > 0)
            {
                for (int layer = 0; layer < terrainLayers.Count; layer++)
                {
                    avgWeights[layer] /= samples;
                    alphas[z, x, layer] = Mathf.Lerp(alphas[z, x, layer], avgWeights[layer], strength);
                }
            }
        }

        void NormalizeWeights(float[,,] alphas, int x, int z)
        {
            float total = 0f;

            for (int i = 0; i < terrainLayers.Count; i++)
            {
                total += alphas[z, x, i];
            }

            if (total > 0.01f)
            {
                for (int i = 0; i < terrainLayers.Count; i++)
                {
                    alphas[z, x, i] /= total;
                }
            }
        }

        void StoreUndoState(float[,,] alphas, int x, int z, int w, int h)
        {
            if (undoStack.Count >= maxUndoSteps)
            {
                var temp = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 1; i < temp.Length; i++)
                {
                    undoStack.Push(temp[i]);
                }
            }

            undoStack.Push(new TextureUndoState(alphas, x, z, w, h));
        }

        void Undo()
        {
            if (undoStack.Count == 0) return;

            var state = undoStack.Pop();
            terrainData.SetAlphamaps(state.xBase, state.yBase, state.alphamaps);
            Debug.Log("Texture painting undone");
        }

        public void AddTerrainLayer(TerrainLayer layer)
        {
            if (layer == null || terrainLayers.Contains(layer)) return;

            terrainLayers.Add(layer);
            terrainData.terrainLayers = terrainLayers.ToArray();

            // Resize alphamaps if needed
            RefreshAlphamaps();
        }

        public void RemoveTerrainLayer(int index)
        {
            if (index < 0 || index >= terrainLayers.Count) return;

            terrainLayers.RemoveAt(index);
            terrainData.terrainLayers = terrainLayers.ToArray();

            // Adjust alphamaps
            RefreshAlphamaps();
        }
    }
}