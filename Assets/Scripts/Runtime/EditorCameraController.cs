using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RubyEditor.Core
{
    public class EditorCameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float fastMoveMultiplier = 3f;
        [SerializeField] private float edgeScrollSpeed = 15f;
        [SerializeField] private float edgeScrollBorder = 10f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private float tiltSpeed = 80f;
        [SerializeField] private float minTilt = 15f;
        [SerializeField] private float maxTilt = 85f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float smoothZoomTime = 0.2f;

        [Header("Focus Settings")]
        [SerializeField] private float focusSpeed = 5f;
        [SerializeField] private Vector3 focusOffset = new Vector3(0, 5f, -10f);

        // Private variables
        private float currentZoom;
        private float targetZoom;
        private float zoomVelocity;
        private Vector3 lastMousePosition;
        private bool isDragging;
        private bool isRotating;

        // Components
        private Transform cameraTransform;
        private Camera editorCamera;

        void Start()
        {
            cameraTransform = transform;
            editorCamera = GetComponent<Camera>();

            // Set initial position
            currentZoom = Vector3.Distance(transform.position, GetGroundPoint());
            targetZoom = currentZoom;

            // Configure camera
            editorCamera.clearFlags = CameraClearFlags.Skybox;
            editorCamera.fieldOfView = 60f;
        }

        void Update()
        {
            HandleKeyboardMovement();
            HandleMouseInput();
            HandleEdgeScrolling();
            HandleZoom();
            HandleRotation();
        }

        void HandleKeyboardMovement()
        {
            float moveMultiplier = IsKeyPressed(KeyCode.LeftShift) ? fastMoveMultiplier : 1f;
            Vector3 moveDirection = Vector3.zero;

            // WASD Movement
            if (IsKeyPressed(KeyCode.W)) moveDirection += cameraTransform.forward;
            if (IsKeyPressed(KeyCode.S)) moveDirection -= cameraTransform.forward;
            if (IsKeyPressed(KeyCode.A)) moveDirection -= cameraTransform.right;
            if (IsKeyPressed(KeyCode.D)) moveDirection += cameraTransform.right;

            // Remove Y component for horizontal movement
            moveDirection.y = 0;
            moveDirection.Normalize();

            // QE for vertical movement
            if (IsKeyPressed(KeyCode.Q)) moveDirection.y = -1;
            if (IsKeyPressed(KeyCode.E)) moveDirection.y = 1;

            // Apply movement
            if (moveDirection != Vector3.zero)
            {
                transform.position += moveDirection * moveSpeed * moveMultiplier * Time.deltaTime;
            }
        }

        void HandleMouseInput()
        {
            // Middle mouse button for panning
            if (IsMouseButtonDown(2))
            {
                isDragging = true;
                lastMousePosition = GetMousePosition();
            }
            else if (IsMouseButtonUp(2))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 delta = GetMousePosition() - lastMousePosition;
                Vector3 move = new Vector3(-delta.x, 0, -delta.y) * 0.05f;

                // Transform to world space
                move = transform.TransformDirection(move);
                move.y = 0;

                transform.position += move;
                lastMousePosition = GetMousePosition();
            }

            // Right mouse button for rotation
            if (IsMouseButtonDown(1))
            {
                isRotating = true;
                lastMousePosition = GetMousePosition();
            }
            else if (IsMouseButtonUp(1))
            {
                isRotating = false;
            }

            if (isRotating)
            {
                Vector3 delta = GetMousePosition() - lastMousePosition;

                // Horizontal rotation (Y-axis)
                transform.Rotate(Vector3.up, delta.x * rotationSpeed * Time.deltaTime, Space.World);

                // Vertical rotation (X-axis)
                float currentTilt = transform.eulerAngles.x;
                currentTilt = currentTilt > 180 ? currentTilt - 360 : currentTilt;

                float tiltDelta = -delta.y * tiltSpeed * Time.deltaTime;
                float newTilt = Mathf.Clamp(currentTilt + tiltDelta, minTilt, maxTilt);

                transform.eulerAngles = new Vector3(newTilt, transform.eulerAngles.y, 0);

                lastMousePosition = GetMousePosition();
            }
        }

        void HandleEdgeScrolling()
        {
            if (isDragging || isRotating) return;

            Vector3 moveDirection = Vector3.zero;
            Vector3 mousePos = GetMousePosition();

            // Check screen edges
            if (mousePos.x <= edgeScrollBorder)
                moveDirection -= transform.right;
            else if (mousePos.x >= Screen.width - edgeScrollBorder)
                moveDirection += transform.right;

            if (mousePos.y <= edgeScrollBorder)
                moveDirection -= transform.forward;
            else if (mousePos.y >= Screen.height - edgeScrollBorder)
                moveDirection += transform.forward;

            // Apply edge scrolling
            moveDirection.y = 0;
            if (moveDirection != Vector3.zero)
            {
                moveDirection.Normalize();
                transform.position += moveDirection * edgeScrollSpeed * Time.deltaTime;
            }
        }

        void HandleZoom()
        {
            float scrollInput = GetMouseScrollDelta();
            if (scrollInput != 0)
            {
                targetZoom -= scrollInput * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            // Smooth zoom
            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, smoothZoomTime);

            // Apply zoom by moving camera forward/backward
            Vector3 zoomDirection = transform.forward;
            RaycastHit hit;

            if (Physics.Raycast(transform.position, transform.forward, out hit))
            {
                float distanceToGround = hit.distance;
                if (currentZoom < distanceToGround - 1f)
                {
                    Vector3 targetPos = hit.point - zoomDirection * currentZoom;
                    transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
                }
            }
        }

        void HandleRotation()
        {
            // Home key to reset rotation
            if (IsKeyDown(KeyCode.Home))
            {
                transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        public void FocusOnObject(GameObject target)
        {
            if (target == null) return;

            Bounds bounds = GetObjectBounds(target);
            Vector3 targetPosition = bounds.center + focusOffset;

            StartCoroutine(SmoothFocus(targetPosition));
        }

        System.Collections.IEnumerator SmoothFocus(Vector3 targetPos)
        {
            float elapsedTime = 0;
            Vector3 startPos = transform.position;

            while (elapsedTime < 1f)
            {
                transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime);
                elapsedTime += Time.deltaTime * focusSpeed;
                yield return null;
            }

            transform.position = targetPos;
        }

        Bounds GetObjectBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(obj.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        Vector3 GetGroundPoint()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 1000f))
            {
                return hit.point;
            }
            return transform.position - Vector3.up * 10f;
        }

        void OnDrawGizmosSelected()
        {
            // Draw camera frustum
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, editorCamera.fieldOfView,
                                editorCamera.farClipPlane, editorCamera.nearClipPlane,
                                editorCamera.aspect);
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

        float GetMouseScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return Input.GetAxis("Mouse ScrollWheel");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Controls.KeyControl GetKeyFromKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.W: return Keyboard.current.wKey;
                case KeyCode.A: return Keyboard.current.aKey;
                case KeyCode.S: return Keyboard.current.sKey;
                case KeyCode.D: return Keyboard.current.dKey;
                case KeyCode.Q: return Keyboard.current.qKey;
                case KeyCode.E: return Keyboard.current.eKey;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
                case KeyCode.Home: return Keyboard.current.homeKey;
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