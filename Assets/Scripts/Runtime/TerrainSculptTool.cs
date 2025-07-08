using UnityEngine;
using RubyEditor.Core;
using RubyEditor.Tools;

namespace RubyEditor.Tools
{
    public class TerrainSculptTool : EditorTool
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private AnimationCurve brushFalloff = AnimationCurve.Linear(0, 1, 1, 0);

        [Header("Sculpt Mode")]
        [SerializeField] private SculptMode currentMode = SculptMode.Raise;

        [Header("Terrain Reference")]
        private Terrain targetTerrain;
        private TerrainData terrainData;

        public enum SculptMode
        {
            Raise,
            Lower,
            Smooth,
            Flatten,
            SetHeight
        }

        public override void OnToolActivated()
        {
            base.OnToolActivated();
            FindTerrain();
        }

        public override void UpdateTool()
        {
            if (targetTerrain == null) return;

            if (Input.GetMouseButton(0))
            {
                SculptTerrain();
            }

            // Brush size adjustment
            if (Input.GetKey(KeyCode.LeftBracket))
                brushSize = Mathf.Max(1f, brushSize - Time.deltaTime * 10f);
            if (Input.GetKey(KeyCode.RightBracket))
                brushSize = Mathf.Min(50f, brushSize + Time.deltaTime * 10f);
        }

        void FindTerrain()
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain != null)
            {
                terrainData = targetTerrain.terrainData;
            }
        }

        void SculptTerrain()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.GetComponent<Terrain>() != null)
                {
                    ModifyTerrain(hit.point);
                }
            }
        }

        void ModifyTerrain(Vector3 worldPos)
        {
            // Implementation würde hier folgen
            Debug.Log($"Terrain sculpting at {worldPos} with mode {currentMode}");
        }

        void OnDrawGizmos()
        {
            if (!isActive || targetTerrain == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawWireSphere(hit.point, brushSize);
            }
        }
    }
}