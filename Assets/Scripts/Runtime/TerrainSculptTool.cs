using UnityEngine;
using RubyEditor.Core;
using System.Collections.Generic;

namespace RubyEditor.Tools
{
    public class TerrainSculptTool : EditorTool
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float brushSoftness = 0.5f;
        [SerializeField] private AnimationCurve brushFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Sculpt Mode")]
        [SerializeField] private SculptMode currentMode = SculptMode.Raise;
        [SerializeField] private float targetHeight = 10f; // For flatten mode

        [Header("Smoothing")]
        [SerializeField] private int smoothIterations = 1;

        [Header("Visual Feedback")]
        [SerializeField] private Color brushColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private bool showBrushPreview = true;

        // Terrain references
        private Terrain targetTerrain;
        private TerrainData terrainData;
        private int terrainResolution;
        private Vector3 terrainSize;

        // Undo system
        private Stack<TerrainUndoState> undoStack = new Stack<TerrainUndoState>();
        private Stack<TerrainUndoState> redoStack = new Stack<TerrainUndoState>();
        private const int maxUndoSteps = 20;

        // Brush visualization
        private GameObject brushVisualizer;
        private LineRenderer brushCircle;

        public enum SculptMode
        {
            Raise,
            Lower,
            Smooth,
            Flatten,
            SetHeight,
            Noise
        }

        private class TerrainUndoState
        {
            public float[,] heights;
            public int xBase, yBase, width, height;
            public string description;

            public TerrainUndoState(float[,] h, int x, int y, int w, int ht, string desc)
            {
                heights = (float[,])h.Clone();
                xBase = x;
                yBase = y;
                width = w;
                height = ht;
                description = desc;
            }
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();
            FindOrCreateTerrain();
            CreateBrushVisualizer();
        }

        public override void OnToolDeactivated()
        {
            base.OnToolDeactivated();
            if (brushVisualizer != null)
            {
                DestroyImmediate(brushVisualizer);
            }
        }

        void FindOrCreateTerrain()
        {
            targetTerrain = Terrain.activeTerrain;

            if (targetTerrain == null)
            {
                // Try to find any terrain
                targetTerrain = FindFirstObjectByType<Terrain>();
            }

            if (targetTerrain == null)
            {
                Debug.LogWarning("No terrain found. Creating default terrain...");
                CreateDefaultTerrain();
            }

            if (targetTerrain != null)
            {
                terrainData = targetTerrain.terrainData;
                terrainResolution = terrainData.heightmapResolution;
                terrainSize = terrainData.size;
            }
        }

        void CreateDefaultTerrain()
        {
            GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
            targetTerrain = terrainObj.GetComponent<Terrain>();
            terrainData = targetTerrain.terrainData;

            // Configure terrain
            terrainData.heightmapResolution = 513;
            terrainData.size = new Vector3(200, 30, 200);

            // Center terrain
            terrainObj.transform.position = new Vector3(-100, 0, -100);

            terrainResolution = terrainData.heightmapResolution;
            terrainSize = terrainData.size;
        }

        void CreateBrushVisualizer()
        {
            brushVisualizer = new GameObject("BrushVisualizer");
            brushCircle = brushVisualizer.AddComponent<LineRenderer>();

            // Configure line renderer
            brushCircle.startWidth = 0.5f;
            brushCircle.endWidth = 0.5f;
            brushCircle.material = new Material(Shader.Find("Sprites/Default"));
            brushCircle.startColor = brushColor;
            brushCircle.endColor = brushColor;
            brushCircle.positionCount = 33; // For smooth circle
            brushCircle.useWorldSpace = true;
        }

        public override void UpdateTool()
        {
            if (targetTerrain == null) return;

            HandleInput();
            UpdateBrushVisualization();

            if (Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftAlt))
            {
                SculptTerrain();
            }
        }

        void HandleInput()
        {
            // Brush size adjustment
            if (Input.GetKey(KeyCode.LeftBracket))
                brushSize = Mathf.Max(1f, brushSize - Time.deltaTime * 20f);
            if (Input.GetKey(KeyCode.RightBracket))
                brushSize = Mathf.Min(100f, brushSize + Time.deltaTime * 20f);

            // Brush strength adjustment
            if (Input.GetKey(KeyCode.Minus))
                brushStrength = Mathf.Max(0.01f, brushStrength - Time.deltaTime * 0.5f);
            if (Input.GetKey(KeyCode.Equals))
                brushStrength = Mathf.Min(1f, brushStrength + Time.deltaTime * 0.5f);

            // Mode switching
            if (Input.GetKeyDown(KeyCode.Alpha1)) currentMode = SculptMode.Raise;
            if (Input.GetKeyDown(KeyCode.Alpha2)) currentMode = SculptMode.Lower;
            if (Input.GetKeyDown(KeyCode.Alpha3)) currentMode = SculptMode.Smooth;
            if (Input.GetKeyDown(KeyCode.Alpha4)) currentMode = SculptMode.Flatten;
            if (Input.GetKeyDown(KeyCode.Alpha5)) currentMode = SculptMode.Noise;

            // Undo/Redo
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.Z)) Undo();
                if (Input.GetKeyDown(KeyCode.Y)) Redo();
            }

            // Set target height for flatten mode
            if (currentMode == SculptMode.Flatten && Input.GetKey(KeyCode.LeftShift))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    targetHeight = hit.point.y;
                }
            }
        }

        void UpdateBrushVisualization()
        {
            if (!showBrushPreview || brushCircle == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 center = hit.point;

                // Draw circle at hit point
                for (int i = 0; i <= 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    Vector3 pos = center + new Vector3(
                        Mathf.Cos(angle) * brushSize,
                        0.5f,
                        Mathf.Sin(angle) * brushSize
                    );

                    // Project onto terrain
                    if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit circleHit, 100f))
                    {
                        brushCircle.SetPosition(i, circleHit.point + Vector3.up * 0.1f);
                    }
                    else
                    {
                        brushCircle.SetPosition(i, pos);
                    }
                }

                // Update color based on mode
                Color modeColor = GetModeColor();
                brushCircle.startColor = modeColor;
                brushCircle.endColor = modeColor;
            }
        }

        Color GetModeColor()
        {
            switch (currentMode)
            {
                case SculptMode.Raise: return new Color(0, 1, 0, 0.5f);
                case SculptMode.Lower: return new Color(1, 0, 0, 0.5f);
                case SculptMode.Smooth: return new Color(0, 0, 1, 0.5f);
                case SculptMode.Flatten: return new Color(1, 1, 0, 0.5f);
                case SculptMode.Noise: return new Color(1, 0, 1, 0.5f);
                default: return brushColor;
            }
        }

        void SculptTerrain()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            if (hit.collider.GetComponent<Terrain>() != targetTerrain) return;

            // Convert world position to terrain position
            Vector3 terrainPos = hit.point - targetTerrain.transform.position;
            Vector3 normalizedPos = new Vector3(
                terrainPos.x / terrainSize.x,
                0,
                terrainPos.z / terrainSize.z
            );

            // Calculate affected area
            int brushSizeInSamples = Mathf.RoundToInt(brushSize * terrainResolution / terrainSize.x);
            int x = Mathf.RoundToInt(normalizedPos.x * terrainResolution);
            int z = Mathf.RoundToInt(normalizedPos.z * terrainResolution);

            // Calculate bounds
            int minX = Mathf.Max(0, x - brushSizeInSamples);
            int maxX = Mathf.Min(terrainResolution, x + brushSizeInSamples);
            int minZ = Mathf.Max(0, z - brushSizeInSamples);
            int maxZ = Mathf.Min(terrainResolution, z + brushSizeInSamples);

            int width = maxX - minX;
            int height = maxZ - minZ;

            // Get current heights
            float[,] heights = terrainData.GetHeights(minX, minZ, width, height);

            // Store undo state
            StoreUndoState(heights, minX, minZ, width, height, $"Terrain {currentMode}");

            // Apply sculpting
            for (int zi = 0; zi < height; zi++)
            {
                for (int xi = 0; xi < width; xi++)
                {
                    int worldX = minX + xi;
                    int worldZ = minZ + zi;

                    float distance = Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(x, z)
                    );

                    if (distance <= brushSizeInSamples)
                    {
                        float normalizedDistance = distance / brushSizeInSamples;
                        float influence = brushFalloff.Evaluate(normalizedDistance) * brushStrength * Time.deltaTime;

                        switch (currentMode)
                        {
                            case SculptMode.Raise:
                                heights[zi, xi] += influence * 0.01f;
                                break;

                            case SculptMode.Lower:
                                heights[zi, xi] -= influence * 0.01f;
                                break;

                            case SculptMode.Smooth:
                                heights[zi, xi] = SmoothHeight(heights, xi, zi, width, height) * influence +
                                                 heights[zi, xi] * (1f - influence);
                                break;

                            case SculptMode.Flatten:
                                float flattenHeight = targetHeight / terrainSize.y;
                                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], flattenHeight, influence);
                                break;

                            case SculptMode.Noise:
                                float noise = Mathf.PerlinNoise(worldX * 0.05f, worldZ * 0.05f) - 0.5f;
                                heights[zi, xi] += noise * influence * 0.02f;
                                break;
                        }

                        // Clamp heights
                        heights[zi, xi] = Mathf.Clamp01(heights[zi, xi]);
                    }
                }
            }

            // Apply heights back to terrain
            terrainData.SetHeights(minX, minZ, heights);
        }

        float SmoothHeight(float[,] heights, int x, int z, int width, int height)
        {
            float sum = 0f;
            int count = 0;

            for (int zi = -1; zi <= 1; zi++)
            {
                for (int xi = -1; xi <= 1; xi++)
                {
                    int nx = x + xi;
                    int nz = z + zi;

                    if (nx >= 0 && nx < width && nz >= 0 && nz < height)
                    {
                        sum += heights[nz, nx];
                        count++;
                    }
                }
            }

            return count > 0 ? sum / count : heights[z, x];
        }

        void StoreUndoState(float[,] heights, int x, int z, int w, int h, string description)
        {
            if (undoStack.Count >= maxUndoSteps)
            {
                // Remove oldest
                var temp = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 1; i < temp.Length; i++)
                {
                    undoStack.Push(temp[i]);
                }
            }

            undoStack.Push(new TerrainUndoState(heights, x, z, w, h, description));
            redoStack.Clear(); // Clear redo when new action is performed
        }

        void Undo()
        {
            if (undoStack.Count == 0) return;

            var state = undoStack.Pop();

            // Get current state for redo
            float[,] currentHeights = terrainData.GetHeights(state.xBase, state.yBase, state.width, state.height);
            redoStack.Push(new TerrainUndoState(currentHeights, state.xBase, state.yBase, state.width, state.height, "Redo " + state.description));

            // Apply undo
            terrainData.SetHeights(state.xBase, state.yBase, state.heights);
            Debug.Log($"Undo: {state.description}");
        }

        void Redo()
        {
            if (redoStack.Count == 0) return;

            var state = redoStack.Pop();

            // Get current state for undo
            float[,] currentHeights = terrainData.GetHeights(state.xBase, state.yBase, state.width, state.height);
            undoStack.Push(new TerrainUndoState(currentHeights, state.xBase, state.yBase, state.width, state.height, "Undo " + state.description));

            // Apply redo
            terrainData.SetHeights(state.xBase, state.yBase, state.heights);
            Debug.Log($"Redo: {state.description}");
        }

        public void SetBrushSize(float size)
        {
            brushSize = Mathf.Clamp(size, 1f, 100f);
        }

        public void SetBrushStrength(float strength)
        {
            brushStrength = Mathf.Clamp01(strength);
        }

        public void SetSculptMode(SculptMode mode)
        {
            currentMode = mode;
        }

        void OnDrawGizmos()
        {
            if (!isActive || targetTerrain == null) return;

            // Additional debug visualization if needed
        }
    }
}