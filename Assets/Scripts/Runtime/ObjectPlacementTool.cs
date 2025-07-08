using UnityEngine;
using System.Collections.Generic;
using RubyEditor.UI; // Add this line if EditorUIManager is in RubyEditor.UI namespace
using RubyEditor;
using RubyEditor.Core; // Add this line if EditorManager is in RubyEditor namespace
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RubyEditor.Tools
{
    public class ObjectPlacementTool : EditorTool
    {
        [Header("Placement Settings")]
        [SerializeField] private GameObject[] placeablePrefabs;
        [SerializeField] private int selectedPrefabIndex = 0;
        [SerializeField] private LayerMask placementLayers = -1;

        [Header("Preview Settings")]
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Color validPlacementColor = new Color(0, 1, 0, 0.5f);
        [SerializeField] private Color invalidPlacementColor = new Color(1, 0, 0, 0.5f);

        [Header("Placement Modes")]
        [SerializeField] private PlacementMode currentMode = PlacementMode.Single;
        [SerializeField] private float brushRadius = 5f;
        [SerializeField] private float brushDensity = 1f;
        [SerializeField] private bool randomRotation = false;
        [SerializeField] private bool randomScale = false;
        [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        // Private variables
        private GameObject previewObject;
        private bool isValidPlacement;
        private List<GameObject> placedObjects = new List<GameObject>();
        private Transform objectContainer;

        public enum PlacementMode
        {
            Single,
            Line,
            Grid,
            Brush,
            Fill
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();
            CreateObjectContainer();
            CreatePreviewObject();
        }

        public override void OnToolDeactivated()
        {
            base.OnToolDeactivated();
            DestroyPreviewObject();
        }

        void CreateObjectContainer()
        {
            GameObject container = GameObject.Find("PlacedObjects");
            if (container == null)
            {
                container = new GameObject("PlacedObjects");
            }
            objectContainer = container.transform;
        }

        void CreatePreviewObject()
        {
            if (placeablePrefabs == null || placeablePrefabs.Length == 0) return;
            if (selectedPrefabIndex >= placeablePrefabs.Length) return;

            GameObject prefab = placeablePrefabs[selectedPrefabIndex];
            if (prefab == null) return;

            previewObject = Instantiate(prefab);
            previewObject.name = "PreviewObject";

            // Make preview semi-transparent
            SetPreviewMaterial(previewObject);

            // Disable colliders on preview
            Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }
        }

        void DestroyPreviewObject()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
                previewObject = null;
            }
        }

        void SetPreviewMaterial(GameObject obj)
        {
            if (previewMaterial == null) return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = previewMaterial;
                }
                renderer.sharedMaterials = materials;
            }
        }

        public override void UpdateTool()
        {
            if (previewObject == null) return;

            UpdatePreviewPosition();
            UpdatePreviewColor();
            HandlePlacement();
            HandleModeSwitch();
        }

        void UpdatePreviewPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f, placementLayers))
            {
                Vector3 position = hit.point;

                // Snap to grid if enabled
                if (EditorManager.Instance.gridSystem != null)
                {
                    position = EditorManager.Instance.gridSystem.SnapToGrid(position);
                }

                previewObject.transform.position = position;

                // Align to surface normal if needed
                if (IsKeyPressed(KeyCode.LeftAlt))
                {
                    previewObject.transform.up = hit.normal;
                }
                else
                {
                    previewObject.transform.rotation = Quaternion.identity;
                }

                // Check if placement is valid
                isValidPlacement = CheckValidPlacement(position);

                // Update coordinates in UI
                if (EditorUIManager.Instance != null)
                {
                    EditorUIManager.Instance.UpdateCoordinates(position);
                }
            }
            else
            {
                isValidPlacement = false;
            }
        }

        void UpdatePreviewColor()
        {
            if (previewMaterial == null) return;

            Color color = isValidPlacement ? validPlacementColor : invalidPlacementColor;
            previewMaterial.color = color;
        }

        bool CheckValidPlacement(Vector3 position)
        {
            // Check for overlaps
            Collider[] overlaps = Physics.OverlapBox(
                position + Vector3.up * 0.5f,
                Vector3.one * 0.4f,
                Quaternion.identity,
                ~placementLayers
            );

            return overlaps.Length == 0;
        }

        void HandlePlacement()
        {
            if (!isValidPlacement) return;

            if (IsMouseButtonDown(0))
            {
                switch (currentMode)
                {
                    case PlacementMode.Single:
                        PlaceSingleObject();
                        break;
                    case PlacementMode.Brush:
                        PlaceBrushObjects();
                        break;
                    case PlacementMode.Line:
                        StartLinePlacement();
                        break;
                    case PlacementMode.Grid:
                        StartGridPlacement();
                        break;
                }
            }
        }

        void PlaceSingleObject()
        {
            GameObject prefab = placeablePrefabs[selectedPrefabIndex];
            GameObject newObject = Instantiate(prefab, previewObject.transform.position, previewObject.transform.rotation);
            newObject.transform.parent = objectContainer;
            newObject.name = prefab.name + "_" + placedObjects.Count;

            placedObjects.Add(newObject);

            // Apply random rotation if enabled
            if (randomRotation)
            {
                newObject.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            }

            // Apply random scale if enabled
            if (randomScale)
            {
                float scale = Random.Range(scaleRange.x, scaleRange.y);
                newObject.transform.localScale = Vector3.one * scale;
            }
        }

        void PlaceBrushObjects()
        {
            int objectCount = Mathf.RoundToInt(brushRadius * brushDensity);

            for (int i = 0; i < objectCount; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * brushRadius;
                Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
                Vector3 spawnPosition = previewObject.transform.position + randomOffset;

                // Raycast down to find ground
                RaycastHit hit;
                if (Physics.Raycast(spawnPosition + Vector3.up * 10f, Vector3.down, out hit, 20f, placementLayers))
                {
                    GameObject prefab = placeablePrefabs[selectedPrefabIndex];
                    GameObject newObject = Instantiate(prefab, hit.point, Quaternion.identity);
                    newObject.transform.parent = objectContainer;

                    if (randomRotation)
                    {
                        newObject.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    }

                    if (randomScale)
                    {
                        float scale = Random.Range(scaleRange.x, scaleRange.y);
                        newObject.transform.localScale = Vector3.one * scale;
                    }

                    placedObjects.Add(newObject);
                }
            }
        }

        void StartLinePlacement()
        {
            // Implementation for line placement
            Debug.Log("Line placement not yet implemented");
        }

        void StartGridPlacement()
        {
            // Implementation for grid placement
            Debug.Log("Grid placement not yet implemented");
        }

        void HandleModeSwitch()
        {
            if (IsKeyDown(KeyCode.Alpha1)) currentMode = PlacementMode.Single;
            if (IsKeyDown(KeyCode.Alpha2)) currentMode = PlacementMode.Brush;
            if (IsKeyDown(KeyCode.Alpha3)) currentMode = PlacementMode.Line;
            if (IsKeyDown(KeyCode.Alpha4)) currentMode = PlacementMode.Grid;
        }

        public void SetSelectedPrefab(int index)
        {
            if (index >= 0 && index < placeablePrefabs.Length)
            {
                selectedPrefabIndex = index;
                DestroyPreviewObject();
                CreatePreviewObject();
            }
        }

        public void ClearAllPlacedObjects()
        {
            foreach (GameObject obj in placedObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
            placedObjects.Clear();
        }

        void OnDrawGizmos()
        {
            if (!isActive || previewObject == null) return;

            // Draw brush radius for brush mode
            if (currentMode == PlacementMode.Brush)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawWireSphere(previewObject.transform.position, brushRadius);
            }
        }

        // Input System compatibility methods
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

        bool IsMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && GetMouseButtonFromInt(button).wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
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

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Controls.KeyControl GetKeyFromKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftAlt: return Keyboard.current.leftAltKey;
                case KeyCode.Alpha1: return Keyboard.current.digit1Key;
                case KeyCode.Alpha2: return Keyboard.current.digit2Key;
                case KeyCode.Alpha3: return Keyboard.current.digit3Key;
                case KeyCode.Alpha4: return Keyboard.current.digit4Key;
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

    // Base class for all editor tools
    public abstract class EditorTool : MonoBehaviour
    {
        protected bool isActive = false;

        public virtual void OnToolActivated()
        {
            isActive = true;
        }

        public virtual void OnToolDeactivated()
        {
            isActive = false;
        }

        public abstract void UpdateTool();

        void Update()
        {
            if (isActive)
            {
                UpdateTool();
            }
        }
    }
}