using UnityEngine;

namespace RubyEditor.Core
{
    public class GridSystem : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private int gridWidth = 100;
        [SerializeField] private int gridHeight = 100;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        [Header("Snapping")]
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float snapValue = 0.5f;

        private GameObject gridVisual;

        void Start()
        {
            CreateGridVisual();
        }

        void CreateGridVisual()
        {
            gridVisual = new GameObject("GridVisual");
            gridVisual.transform.parent = transform;

            // Hier würden wir das Grid-Mesh erstellen
            // Für jetzt nur ein Platzhalter
        }

        public Vector3 SnapToGrid(Vector3 worldPosition)
        {
            if (!snapToGrid) return worldPosition;

            float x = Mathf.Round(worldPosition.x / snapValue) * snapValue;
            float y = worldPosition.y; // Y-Position beibehalten
            float z = Mathf.Round(worldPosition.z / snapValue) * snapValue;

            return new Vector3(x, y, z);
        }

        void OnDrawGizmos()
        {
            if (!showGrid) return;

            Gizmos.color = gridColor;

            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = new Vector3(x * gridSize, 0, 0);
                Vector3 end = new Vector3(x * gridSize, 0, gridHeight * gridSize);
                Gizmos.DrawLine(start, end);
            }

            for (int z = 0; z <= gridHeight; z++)
            {
                Vector3 start = new Vector3(0, 0, z * gridSize);
                Vector3 end = new Vector3(gridWidth * gridSize, 0, z * gridSize);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}