using UnityEngine;
using System.Collections.Generic;

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
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f, placementLayers))
            {
                Vector3 position = hit.point;

                // Snap to grid if enabled
                if (EditorManager.Instance.gridSystem.snapToGrid)
                {
                    position = EditorManager.Instance.gridSystem.SnapToGrid(position);
                }

                previewObject.transform.position = position;

                // Align to surface normal if needed
                if (Input.GetKey(KeyCode.LeftAlt))
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

            if (Input.GetMouseButtonDown(0))
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

            // Register undo
            // Undo.RegisterCreatedObjectUndo(newObject, "Place Object");
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
            if (Input.GetKeyDown(KeyCode.Alpha1)) currentMode = PlacementMode.Single;
            if (Input.GetKeyDown(KeyCode.Alpha2)) currentMode = PlacementMode.Brush;
            if (Input.GetKeyDown(KeyCode.Alpha3)) currentMode = PlacementMode.Line;
            if (Input.GetKeyDown(KeyCode.Alpha4)) currentMode = PlacementMode.Grid;
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