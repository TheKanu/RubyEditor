using UnityEngine;
using System.Collections.Generic;

namespace RubyEditor.Core
{
    public class SelectionSystem : MonoBehaviour
    {
        [Header("Selection")]
        private GameObject selectedObject;
        private List<GameObject> selectedObjects = new List<GameObject>();

        [Header("Visual Feedback")]
        [SerializeField] private Material selectionMaterial;
        [SerializeField] private Color selectionColor = Color.yellow;

        void Update()
        {
            HandleSelection();
        }

        void HandleSelection()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    SelectObject(hit.collider.gameObject);
                }
                else
                {
                    DeselectAll();
                }
            }
        }

        public void SelectObject(GameObject obj)
        {
            DeselectAll();
            selectedObject = obj;
            HighlightObject(obj, true);
        }

        public void DeselectAll()
        {
            if (selectedObject != null)
            {
                HighlightObject(selectedObject, false);
                selectedObject = null;
            }
        }

        void HighlightObject(GameObject obj, bool highlight)
        {
            // Outline oder Material-Swap für Highlight
            // Implementierung folgt
        }
    }
}