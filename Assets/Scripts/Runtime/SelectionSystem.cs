using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
            if (IsMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
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

        // Input System compatibility methods
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