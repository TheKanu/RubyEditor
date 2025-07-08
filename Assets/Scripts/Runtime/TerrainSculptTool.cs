using UnityEngine;
using RubyEditor.Core;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RubyEditor.Tools
{
    public class TerrainSculptTool : EditorTool
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float brushSoftness = 0.5f;
        [SerializeField] private AnimationCurve brushFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private BrushShape brushShape = BrushShape.Circle;
        [SerializeField] private float brushRotation = 0f;
        [SerializeField] private float brushSpacing = 0.1f;

        [Header("Drag Sculpting")]
        [SerializeField] private bool enableDragSculpting = true;
        [SerializeField] private float dragSensitivity = 2f;
        [SerializeField] private float maxDragHeight = 50f;
        [SerializeField] private bool invertDrag = false;

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

        // Drag sculpting variables
        private bool isDragging = false;
        private Vector3 dragStartPosition;
        private Vector3 dragCurrentPosition;
        private float dragStartHeight;
        private float dragCurrentHeight;
        private Vector3 lastMousePosition;

        // Terrain stamps and presets
        private Dictionary<string, TerrainStamp> terrainStamps = new Dictionary<string, TerrainStamp>();

        // Erosion simulation
        private float[,] erosionMap;
        private float[,] sedimentMap;
        private float[,] velocityMapX;
        private float[,] velocityMapY;

        // Large-scale terraforming
        private bool isLargeScale = false;
        private float largeScaleMultiplier = 5f;

        public enum SculptMode
        {
            Raise,
            Lower,
            Smooth,
            Flatten,
            SetHeight,
            Noise,
            // WoW-style advanced modes
            Plateau,
            Canyon,
            Ridge,
            Cliff,
            Erosion,
            MountainStamp,
            ValleyStamp,
            DragSculpt
        }

        public enum BrushShape
        {
            Circle,
            Square,
            Diamond,
            Star,
            Mountain,
            Valley,
            Ridge
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

        [System.Serializable]
        private class TerrainStamp
        {
            public string name;
            public float[,] heightPattern;
            public int width, height;
            public float intensity;
            public BrushShape shape;

            public TerrainStamp(string stampName, int w, int h, BrushShape stampShape = BrushShape.Circle)
            {
                name = stampName;
                width = w;
                height = h;
                shape = stampShape;
                heightPattern = new float[h, w];
                intensity = 1f;
            }
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();
            FindOrCreateTerrain();
            CreateBrushVisualizer();
            InitializeTerrainStamps();
            InitializeErosionMaps();
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

            // Handle different sculpting modes
            if (currentMode == SculptMode.DragSculpt)
            {
                HandleDragSculpting();
            }
            else if (IsMouseButtonPressed(0) && !IsKeyPressed(KeyCode.LeftAlt))
            {
                SculptTerrain();
            }
        }

        void HandleInput()
        {
            // Brush size adjustment
            if (IsKeyPressed(KeyCode.LeftBracket))
                brushSize = Mathf.Max(1f, brushSize - Time.deltaTime * 20f);
            if (IsKeyPressed(KeyCode.RightBracket))
                brushSize = Mathf.Min(100f, brushSize + Time.deltaTime * 20f);

            // Brush strength adjustment
            if (IsKeyPressed(KeyCode.Minus))
                brushStrength = Mathf.Max(0.01f, brushStrength - Time.deltaTime * 0.5f);
            if (IsKeyPressed(KeyCode.Equals))
                brushStrength = Mathf.Min(1f, brushStrength + Time.deltaTime * 0.5f);

            // Mode switching - Basic modes
            if (IsKeyDown(KeyCode.Alpha1)) currentMode = SculptMode.Raise;
            if (IsKeyDown(KeyCode.Alpha2)) currentMode = SculptMode.Lower;
            if (IsKeyDown(KeyCode.Alpha3)) currentMode = SculptMode.Smooth;
            if (IsKeyDown(KeyCode.Alpha4)) currentMode = SculptMode.Flatten;
            if (IsKeyDown(KeyCode.Alpha5)) currentMode = SculptMode.Noise;

            // Advanced WoW-style modes
            if (IsKeyDown(KeyCode.Alpha6)) currentMode = SculptMode.Plateau;
            if (IsKeyDown(KeyCode.Alpha7)) currentMode = SculptMode.Canyon;
            if (IsKeyDown(KeyCode.Alpha8)) currentMode = SculptMode.Ridge;
            if (IsKeyDown(KeyCode.Alpha9)) currentMode = SculptMode.Cliff;
            if (IsKeyDown(KeyCode.Alpha0)) currentMode = SculptMode.DragSculpt;

            // Terrain stamps
            if (IsKeyPressed(KeyCode.LeftShift))
            {
                if (IsKeyDown(KeyCode.M)) currentMode = SculptMode.MountainStamp;
                if (IsKeyDown(KeyCode.V)) currentMode = SculptMode.ValleyStamp;
                if (IsKeyDown(KeyCode.E)) currentMode = SculptMode.Erosion;
            }

            // Brush shape switching
            if (IsKeyPressed(KeyCode.LeftAlt))
            {
                if (IsKeyDown(KeyCode.Alpha1)) brushShape = BrushShape.Circle;
                if (IsKeyDown(KeyCode.Alpha2)) brushShape = BrushShape.Square;
                if (IsKeyDown(KeyCode.Alpha3)) brushShape = BrushShape.Diamond;
                if (IsKeyDown(KeyCode.Alpha4)) brushShape = BrushShape.Star;
                if (IsKeyDown(KeyCode.Alpha5)) brushShape = BrushShape.Mountain;
            }

            // Large-scale toggle
            if (IsKeyDown(KeyCode.L))
            {
                isLargeScale = !isLargeScale;
                Debug.Log($"Large-scale terraforming: {(isLargeScale ? "ON" : "OFF")}");
            }

            // Brush rotation
            if (IsKeyPressed(KeyCode.R))
            {
                brushRotation += GetMouseAxisX() * 50f * Time.deltaTime;
            }

            // Undo/Redo
            if (IsKeyPressed(KeyCode.LeftControl))
            {
                if (IsKeyDown(KeyCode.Z)) Undo();
                if (IsKeyDown(KeyCode.Y)) Redo();
            }

            // Set target height for flatten mode
            if (currentMode == SculptMode.Flatten && IsKeyPressed(KeyCode.LeftShift))
            {
                Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    targetHeight = hit.point.y;
                }
            }

            // Invert drag for lowering terrain
            if (IsKeyDown(KeyCode.I))
            {
                invertDrag = !invertDrag;
                Debug.Log($"Drag invert: {(invertDrag ? "ON (Lower)" : "OFF (Raise)")}");
            }
        }

        void UpdateBrushVisualization()
        {
            if (!showBrushPreview || brushCircle == null) return;

            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
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
            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
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

                            // WoW-style advanced modes
                            case SculptMode.Plateau:
                                ApplyPlateauSculpting(heights, xi, zi, influence, worldX, worldZ, brushSizeInSamples);
                                break;

                            case SculptMode.Canyon:
                                ApplyCanyonSculpting(heights, xi, zi, influence, worldX, worldZ, brushSizeInSamples);
                                break;

                            case SculptMode.Ridge:
                                ApplyRidgeSculpting(heights, xi, zi, influence, worldX, worldZ, brushSizeInSamples, x, z);
                                break;

                            case SculptMode.Cliff:
                                ApplyCliffSculpting(heights, xi, zi, influence, worldX, worldZ, brushSizeInSamples, x, z);
                                break;

                            case SculptMode.MountainStamp:
                                ApplyTerrainStamp(heights, xi, zi, influence, worldX, worldZ, "mountain");
                                break;

                            case SculptMode.ValleyStamp:
                                ApplyTerrainStamp(heights, xi, zi, influence, worldX, worldZ, "valley");
                                break;

                            case SculptMode.Erosion:
                                ApplyErosionEffect(heights, xi, zi, influence, worldX, worldZ, width, height);
                                break;
                        }

                        // Clamp heights
                        heights[zi, xi] = Mathf.Clamp01(heights[zi, xi]);
                    }
                }
            }

            // Apply heights back to terrain
            terrainData.SetHeights(minX, minZ, heights);

            // Apply large-scale modifier if enabled
            if (isLargeScale)
            {
                ApplyLargeScaleEffect(minX, minZ, width, height);
            }
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

        // ===========================================
        // WoW-Style Terraforming Methods
        // ===========================================

        void InitializeTerrainStamps()
        {
            // Create mountain stamp
            var mountainStamp = new TerrainStamp("mountain", 64, 64, BrushShape.Mountain);
            CreateMountainStamp(mountainStamp);
            terrainStamps["mountain"] = mountainStamp;

            // Create valley stamp
            var valleyStamp = new TerrainStamp("valley", 64, 64, BrushShape.Valley);
            CreateValleyStamp(valleyStamp);
            terrainStamps["valley"] = valleyStamp;

            // Create ridge stamp
            var ridgeStamp = new TerrainStamp("ridge", 32, 64, BrushShape.Ridge);
            CreateRidgeStamp(ridgeStamp);
            terrainStamps["ridge"] = ridgeStamp;
        }

        void CreateMountainStamp(TerrainStamp stamp)
        {
            int centerX = stamp.width / 2;
            int centerY = stamp.height / 2;
            float maxRadius = Mathf.Min(centerX, centerY) * 0.8f;

            for (int y = 0; y < stamp.height; y++)
            {
                for (int x = 0; x < stamp.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    float normalizedDistance = distance / maxRadius;

                    if (normalizedDistance <= 1f)
                    {
                        // Create mountain shape with realistic falloff
                        float height = Mathf.Pow(1f - normalizedDistance, 2f) * 0.8f;
                        // Add some noise for natural variation
                        height += Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.1f;
                        stamp.heightPattern[y, x] = height;
                    }
                    else
                    {
                        stamp.heightPattern[y, x] = 0f;
                    }
                }
            }
        }

        void CreateValleyStamp(TerrainStamp stamp)
        {
            int centerX = stamp.width / 2;
            int centerY = stamp.height / 2;
            float maxRadius = Mathf.Min(centerX, centerY) * 0.9f;

            for (int y = 0; y < stamp.height; y++)
            {
                for (int x = 0; x < stamp.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    float normalizedDistance = distance / maxRadius;

                    if (normalizedDistance <= 1f)
                    {
                        // Create valley shape (inverted mountain)
                        float depth = -Mathf.Pow(1f - normalizedDistance, 1.5f) * 0.5f;
                        // Add erosion-like patterns
                        depth += Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.05f;
                        stamp.heightPattern[y, x] = depth;
                    }
                    else
                    {
                        stamp.heightPattern[y, x] = 0f;
                    }
                }
            }
        }

        void CreateRidgeStamp(TerrainStamp stamp)
        {
            int centerY = stamp.height / 2;

            for (int y = 0; y < stamp.height; y++)
            {
                for (int x = 0; x < stamp.width; x++)
                {
                    float distanceFromCenter = Mathf.Abs(y - centerY) / (float)centerY;
                    float height = (1f - distanceFromCenter) * 0.6f;

                    // Add ridge variation
                    height += Mathf.PerlinNoise(x * 0.2f, y * 0.1f) * 0.1f;
                    stamp.heightPattern[y, x] = Mathf.Max(0f, height);
                }
            }
        }

        void InitializeErosionMaps()
        {
            if (terrainData == null) return;

            int res = terrainResolution;
            erosionMap = new float[res, res];
            sedimentMap = new float[res, res];
            velocityMapX = new float[res, res];
            velocityMapY = new float[res, res];
        }

        void HandleDragSculpting()
        {
            if (IsMouseButtonDown(0) && !IsKeyPressed(KeyCode.LeftAlt))
            {
                // Start dragging
                isDragging = true;
                Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    dragStartPosition = hit.point;
                    dragStartHeight = hit.point.y;
                }
                lastMousePosition = GetMousePosition();
            }
            else if (IsMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 currentMousePos = GetMousePosition();
                Vector3 mouseDelta = currentMousePos - lastMousePosition;

                // Calculate height change based on mouse Y movement
                float heightDelta = -mouseDelta.y * dragSensitivity * Time.deltaTime;
                if (invertDrag) heightDelta = -heightDelta;

                dragCurrentHeight = Mathf.Clamp(dragStartHeight + heightDelta, 0f, maxDragHeight);

                // Apply drag sculpting
                Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    ApplyDragSculpting(hit.point, dragCurrentHeight);
                }

                lastMousePosition = currentMousePos;
            }
        }

        void ApplyDragSculpting(Vector3 worldPos, float targetHeight)
        {
            Vector3 terrainPos = worldPos - targetTerrain.transform.position;
            Vector2 normalizedPos = new Vector2(
                terrainPos.x / terrainSize.x,
                terrainPos.z / terrainSize.z
            );

            int mapX = Mathf.RoundToInt(normalizedPos.x * terrainResolution);
            int mapZ = Mathf.RoundToInt(normalizedPos.y * terrainResolution);
            int brushSizeInSamples = Mathf.RoundToInt(brushSize * terrainResolution / terrainSize.x);

            if (isLargeScale) brushSizeInSamples = Mathf.RoundToInt(brushSizeInSamples * largeScaleMultiplier);

            int minX = Mathf.Max(0, mapX - brushSizeInSamples);
            int maxX = Mathf.Min(terrainResolution, mapX + brushSizeInSamples);
            int minZ = Mathf.Max(0, mapZ - brushSizeInSamples);
            int maxZ = Mathf.Min(terrainResolution, mapZ + brushSizeInSamples);

            int width = maxX - minX;
            int height = maxZ - minZ;

            float[,] heights = terrainData.GetHeights(minX, minZ, width, height);
            float normalizedTargetHeight = targetHeight / terrainSize.y;

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
                        float influence = brushFalloff.Evaluate(normalizedDistance) * brushStrength;

                        heights[z, x] = Mathf.Lerp(heights[z, x], normalizedTargetHeight, influence * Time.deltaTime * 2f);
                    }
                }
            }

            terrainData.SetHeights(minX, minZ, heights);
        }

        void ApplyPlateauSculpting(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, int brushSizeInSamples)
        {
            // Create flat-topped mountains
            float plateauHeight = 0.7f;
            float currentHeight = heights[zi, xi];

            if (currentHeight < plateauHeight)
            {
                heights[zi, xi] = Mathf.Lerp(currentHeight, plateauHeight, influence);
            }
            else
            {
                // Flatten the top
                heights[zi, xi] = Mathf.Lerp(currentHeight, plateauHeight, influence * 0.5f);
            }
        }

        void ApplyCanyonSculpting(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, int brushSizeInSamples)
        {
            // Create deep valleys with steep sides
            float canyonDepth = 0.3f;
            float currentHeight = heights[zi, xi];

            // Create U-shaped canyon
            float edgeDistance = Mathf.Abs(xi - brushSizeInSamples) / (float)brushSizeInSamples;
            float canyonProfile = Mathf.Pow(edgeDistance, 2f);

            float targetHeight = Mathf.Lerp(canyonDepth, currentHeight, canyonProfile);
            heights[zi, xi] = Mathf.Lerp(currentHeight, targetHeight, influence);
        }

        void ApplyRidgeSculpting(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, int brushSizeInSamples, int centerX, int centerZ)
        {
            // Create mountain ridges
            Vector2 ridgeDirection = new Vector2(Mathf.Cos(brushRotation * Mathf.Deg2Rad), Mathf.Sin(brushRotation * Mathf.Deg2Rad));
            Vector2 pointPos = new Vector2(worldX - centerX, worldZ - centerZ);

            float distanceFromRidge = Mathf.Abs(Vector2.Dot(pointPos, ridgeDirection.normalized));
            float ridgeProfile = Mathf.Exp(-distanceFromRidge * 0.1f);

            float ridgeHeight = 0.6f * ridgeProfile;
            heights[zi, xi] = Mathf.Lerp(heights[zi, xi], heights[zi, xi] + ridgeHeight, influence);
        }

        void ApplyCliffSculpting(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, int brushSizeInSamples, int centerX, int centerZ)
        {
            // Create steep cliffs
            Vector2 cliffDirection = new Vector2(Mathf.Cos(brushRotation * Mathf.Deg2Rad), Mathf.Sin(brushRotation * Mathf.Deg2Rad));
            Vector2 pointPos = new Vector2(worldX - centerX, worldZ - centerZ);

            float distanceFromCliff = Vector2.Dot(pointPos, cliffDirection.normalized);

            if (distanceFromCliff > 0)
            {
                float cliffHeight = 0.8f;
                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], cliffHeight, influence);
            }
            else
            {
                float baseHeight = 0.2f;
                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], baseHeight, influence);
            }
        }

        void ApplyTerrainStamp(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, string stampName)
        {
            if (!terrainStamps.ContainsKey(stampName)) return;

            TerrainStamp stamp = terrainStamps[stampName];

            // Calculate stamp position
            int stampX = xi % stamp.width;
            int stampZ = zi % stamp.height;

            float stampHeight = stamp.heightPattern[stampZ, stampX] * stamp.intensity;
            heights[zi, xi] = Mathf.Lerp(heights[zi, xi], heights[zi, xi] + stampHeight, influence);
        }

        void ApplyErosionEffect(float[,] heights, int xi, int zi, float influence, int worldX, int worldZ, int width, int height)
        {
            // Simple erosion simulation
            float erosionRate = 0.01f;
            float sedimentCapacity = 0.1f;

            // Calculate water flow direction (downhill)
            Vector2 gradient = CalculateGradient(heights, xi, zi, width, height);

            // Apply erosion
            if (gradient.magnitude > 0.01f)
            {
                float erosionAmount = erosionRate * influence * gradient.magnitude;
                heights[zi, xi] = Mathf.Max(0f, heights[zi, xi] - erosionAmount);

                // Deposit sediment downstream
                int downstreamX = Mathf.Clamp(xi + Mathf.RoundToInt(gradient.x * 2), 0, width - 1);
                int downstreamZ = Mathf.Clamp(zi + Mathf.RoundToInt(gradient.y * 2), 0, height - 1);

                heights[downstreamZ, downstreamX] += erosionAmount * 0.5f;
            }
        }

        Vector2 CalculateGradient(float[,] heights, int x, int z, int width, int height)
        {
            float gradientX = 0f;
            float gradientZ = 0f;

            if (x > 0 && x < width - 1)
            {
                gradientX = heights[z, x + 1] - heights[z, x - 1];
            }

            if (z > 0 && z < height - 1)
            {
                gradientZ = heights[z + 1, x] - heights[z - 1, x];
            }

            return new Vector2(gradientX, gradientZ);
        }

        void ApplyLargeScaleEffect(int minX, int minZ, int width, int height)
        {
            // Apply additional large-scale shaping
            float[,] currentHeights = terrainData.GetHeights(minX, minZ, width, height);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Add large-scale noise for natural variation
                    float largeNoise = Mathf.PerlinNoise((minX + x) * 0.01f, (minZ + z) * 0.01f) * 0.1f;
                    currentHeights[z, x] += largeNoise * largeScaleMultiplier * 0.1f;
                }
            }

            terrainData.SetHeights(minX, minZ, currentHeights);
        }

        float GetBrushIntensity(float normalizedDistance)
        {
            switch (brushShape)
            {
                case BrushShape.Circle:
                    return brushFalloff.Evaluate(normalizedDistance);

                case BrushShape.Square:
                    return normalizedDistance <= 1f ? brushFalloff.Evaluate(0f) : 0f;

                case BrushShape.Diamond:
                    float diamondDist = Mathf.Abs(normalizedDistance - 0.5f) * 2f;
                    return brushFalloff.Evaluate(diamondDist);

                case BrushShape.Star:
                    float starPattern = Mathf.Abs(Mathf.Sin(normalizedDistance * Mathf.PI * 8f)) * 0.5f + 0.5f;
                    return brushFalloff.Evaluate(normalizedDistance) * starPattern;

                case BrushShape.Mountain:
                    return Mathf.Pow(1f - normalizedDistance, 2f);

                case BrushShape.Valley:
                    return 1f - Mathf.Pow(1f - normalizedDistance, 2f);

                default:
                    return brushFalloff.Evaluate(normalizedDistance);
            }
        }

        // ===========================================
        // Input System compatibility methods
        // ===========================================
        bool IsKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && GetKeyFromKeyCode(key).isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        bool IsKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && GetKeyFromKeyCode(key).wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        bool IsMouseButtonPressed(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && GetMouseButtonFromInt(button).isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        bool IsMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && GetMouseButtonFromInt(button).wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        bool IsMouseButtonUp(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && GetMouseButtonFromInt(button).wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        float GetMouseAxisX()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().x : 0f;
#else
            return Input.GetAxis("Mouse X");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Controls.KeyControl GetKeyFromKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftBracket: return Keyboard.current.leftBracketKey;
                case KeyCode.RightBracket: return Keyboard.current.rightBracketKey;
                case KeyCode.Minus: return Keyboard.current.minusKey;
                case KeyCode.Equals: return Keyboard.current.equalsKey;
                case KeyCode.Alpha1: return Keyboard.current.digit1Key;
                case KeyCode.Alpha2: return Keyboard.current.digit2Key;
                case KeyCode.Alpha3: return Keyboard.current.digit3Key;
                case KeyCode.Alpha4: return Keyboard.current.digit4Key;
                case KeyCode.Alpha5: return Keyboard.current.digit5Key;
                case KeyCode.Alpha6: return Keyboard.current.digit6Key;
                case KeyCode.Alpha7: return Keyboard.current.digit7Key;
                case KeyCode.Alpha8: return Keyboard.current.digit8Key;
                case KeyCode.Alpha9: return Keyboard.current.digit9Key;
                case KeyCode.Alpha0: return Keyboard.current.digit0Key;
                case KeyCode.LeftControl: return Keyboard.current.leftCtrlKey;
                case KeyCode.Z: return Keyboard.current.zKey;
                case KeyCode.Y: return Keyboard.current.yKey;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
                case KeyCode.LeftAlt: return Keyboard.current.leftAltKey;
                case KeyCode.M: return Keyboard.current.mKey;
                case KeyCode.V: return Keyboard.current.vKey;
                case KeyCode.E: return Keyboard.current.eKey;
                case KeyCode.L: return Keyboard.current.lKey;
                case KeyCode.R: return Keyboard.current.rKey;
                case KeyCode.I: return Keyboard.current.iKey;
                default: return Keyboard.current.spaceKey;
            }
        }

        UnityEngine.InputSystem.Controls.ButtonControl GetMouseButtonFromInt(int button)
        {
            switch (button)
            {
                case 0: return Mouse.current.leftButton;
                case 1: return Mouse.current.rightButton;
                case 2: return Mouse.current.middleButton;
                default: return Mouse.current.leftButton;
            }
        }
#endif
    }
}