using UnityEngine;

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
            float moveMultiplier = Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f;
            Vector3 moveDirection = Vector3.zero;

            // WASD Movement
            if (Input.GetKey(KeyCode.W)) moveDirection += cameraTransform.forward;
            if (Input.GetKey(KeyCode.S)) moveDirection -= cameraTransform.forward;
            if (Input.GetKey(KeyCode.A)) moveDirection -= cameraTransform.right;
            if (Input.GetKey(KeyCode.D)) moveDirection += cameraTransform.right;

            // Remove Y component for horizontal movement
            moveDirection.y = 0;
            moveDirection.Normalize();

            // QE for vertical movement
            if (Input.GetKey(KeyCode.Q)) moveDirection.y = -1;
            if (Input.GetKey(KeyCode.E)) moveDirection.y = 1;

            // Apply movement
            if (moveDirection != Vector3.zero)
            {
                transform.position += moveDirection * moveSpeed * moveMultiplier * Time.deltaTime;
            }
        }

        void HandleMouseInput()
        {
            // Middle mouse button for panning
            if (Input.GetMouseButtonDown(2))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                Vector3 move = new Vector3(-delta.x, 0, -delta.y) * 0.05f;

                // Transform to world space
                move = transform.TransformDirection(move);
                move.y = 0;

                transform.position += move;
                lastMousePosition = Input.mousePosition;
            }

            // Right mouse button for rotation
            if (Input.GetMouseButtonDown(1))
            {
                isRotating = true;
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isRotating = false;
            }

            if (isRotating)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;

                // Horizontal rotation (Y-axis)
                transform.Rotate(Vector3.up, delta.x * rotationSpeed * Time.deltaTime, Space.World);

                // Vertical rotation (X-axis)
                float currentTilt = transform.eulerAngles.x;
                currentTilt = currentTilt > 180 ? currentTilt - 360 : currentTilt;

                float tiltDelta = -delta.y * tiltSpeed * Time.deltaTime;
                float newTilt = Mathf.Clamp(currentTilt + tiltDelta, minTilt, maxTilt);

                transform.eulerAngles = new Vector3(newTilt, transform.eulerAngles.y, 0);

                lastMousePosition = Input.mousePosition;
            }
        }

        void HandleEdgeScrolling()
        {
            if (isDragging || isRotating) return;

            Vector3 moveDirection = Vector3.zero;
            Vector3 mousePos = Input.mousePosition;

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
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
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
            if (Input.GetKeyDown(KeyCode.Home))
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
    }
}