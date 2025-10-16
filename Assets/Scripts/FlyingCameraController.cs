using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class EditorCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Base movement speed")]
    public float moveSpeed = 10f;

    [Tooltip("Speed multiplier when holding Shift")]
    public float sprintMultiplier = 2f;

    [Tooltip("Smoothing for movement")]
    [Range(0.01f, 1f)]
    public float movementSmoothing = 0.15f;

    [Header("Look Settings")]
    [Tooltip("Mouse button to enable camera rotation (0=Left, 1=Right, 2=Middle)")]
    public int rotateMouseButton = 1; // Right mouse button

    [Tooltip("Alternative mouse button for rotation")]
    public int altRotateMouseButton = 2; // Middle mouse button

    [Tooltip("Mouse sensitivity for looking around")]
    public float lookSensitivity = 2f;

    [Tooltip("Smoothing for camera rotation")]
    [Range(0.01f, 1f)]
    public float lookSmoothing = 0.1f;

    [Tooltip("Limit vertical look angle")]
    public float maxLookAngle = 90f;

    [Header("Pan Settings")]
    [Tooltip("Enable panning with Shift + Middle Mouse")]
    public bool enablePanning = true;

    [Tooltip("Pan speed")]
    public float panSpeed = 0.5f;

    [Header("Zoom Settings")]
    [Tooltip("Enable zoom with scroll wheel")]
    public bool enableZoom = true;

    [Tooltip("Zoom speed")]
    public float zoomSpeed = 5f;

    [Tooltip("Minimum zoom speed (for fine control when close)")]
    public float minZoomSpeed = 1f;

    [Header("Boundary Settings")]
    [Tooltip("Reference to terrain generator for boundaries")]
    public TerrainGenerator terrainGenerator;

    [Tooltip("Minimum height above terrain surface")]
    public float minHeightAboveTerrain = 2f;

    [Tooltip("Maximum distance from terrain center")]
    public float maxDistanceFromCenter = 100f;

    [Tooltip("Maximum height above terrain")]
    public float maxHeightAboveTerrain = 50f;

    [Tooltip("Enable collision with terrain")]
    public bool enableCollision = true;

    [Header("Collision Settings")]
    [Tooltip("Layer mask for terrain collision")]
    public LayerMask collisionLayers = -1;

    [Tooltip("Distance to maintain from terrain surfaces")]
    public float collisionDistance = 1.5f;

    [Tooltip("Number of collision rays to cast")]
    public int collisionRayCount = 8;

    [Tooltip("Push-back force when too close to terrain")]
    public float pushBackForce = 0.5f;

    // Input references
    private Keyboard keyboard;
    private Mouse mouse;

    // Movement state
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private Vector2 currentRotation;
    private Vector2 targetRotation;
    private float currentPitch;

    // Rotation state
    private bool isRotating = false;
    private bool isPanning = false;
    private Vector2 lastMousePosition;

    void Start()
    {
        // Get input devices
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        // Find terrain generator if not assigned
        if (terrainGenerator == null)
        {
            terrainGenerator = FindObjectOfType<TerrainGenerator>();
        }

        // Initialize rotation
        Vector3 currentEuler = transform.eulerAngles;
        currentPitch = currentEuler.x;
        if (currentPitch > 180f) currentPitch -= 360f; // Normalize to -180 to 180
        currentRotation = new Vector2(currentEuler.y, currentPitch);
        targetRotation = currentRotation;

        // Keep cursor visible and unlocked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (keyboard == null || mouse == null) return;

        HandleRotationInput();
        HandlePanInput();
        HandleZoomInput();
        HandleMovementInput();
    }

    void HandleRotationInput()
    {
        // Check if rotation button is pressed
        bool rotatePressed = false;

        if (rotateMouseButton == 0 && mouse.leftButton.isPressed)
            rotatePressed = true;
        else if (rotateMouseButton == 1 && mouse.rightButton.isPressed)
            rotatePressed = true;
        else if (rotateMouseButton == 2 && mouse.middleButton.isPressed)
            rotatePressed = true;

        // Check alternative button
        if (altRotateMouseButton == 0 && mouse.leftButton.isPressed)
            rotatePressed = true;
        else if (altRotateMouseButton == 1 && mouse.rightButton.isPressed)
            rotatePressed = true;
        else if (altRotateMouseButton == 2 && mouse.middleButton.isPressed)
            rotatePressed = true;

        // Only rotate if not panning
        if (rotatePressed && !isPanning)
        {
            if (!isRotating)
            {
                isRotating = true;
                lastMousePosition = mouse.position.ReadValue();
            }

            // Get mouse delta
            Vector2 currentMousePosition = mouse.position.ReadValue();
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            // Apply sensitivity
            targetRotation.x += mouseDelta.x * lookSensitivity * 0.1f;
            targetRotation.y -= mouseDelta.y * lookSensitivity * 0.1f;

            // Clamp vertical rotation
            targetRotation.y = Mathf.Clamp(targetRotation.y, -maxLookAngle, maxLookAngle);
        }
        else
        {
            isRotating = false;
        }

        // Smooth rotation
        currentRotation = Vector2.Lerp(currentRotation, targetRotation, lookSmoothing * 60f * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetRotation.y, lookSmoothing * 60f * Time.deltaTime);

        // Apply rotation
        transform.rotation = Quaternion.Euler(currentPitch, currentRotation.x, 0f);
    }

    void HandlePanInput()
    {
        if (!enablePanning) return;

        // Pan with Shift + Middle Mouse (or Shift + Alt button)
        bool shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        bool panButtonPressed = mouse.middleButton.isPressed ||
                               (altRotateMouseButton == 1 && mouse.rightButton.isPressed) ||
                               (altRotateMouseButton == 0 && mouse.leftButton.isPressed);

        if (shiftPressed && panButtonPressed)
        {
            if (!isPanning)
            {
                isPanning = true;
                lastMousePosition = mouse.position.ReadValue();
            }

            // Get mouse delta
            Vector2 currentMousePosition = mouse.position.ReadValue();
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            // Convert screen delta to world delta
            Vector3 panMove = transform.right * -mouseDelta.x * panSpeed * 0.01f +
                             transform.up * -mouseDelta.y * panSpeed * 0.01f;

            // Apply pan
            Vector3 newPosition = transform.position + panMove;
            newPosition = ApplyBoundaries(newPosition);

            if (enableCollision)
                newPosition = CheckAndResolveCollision(newPosition);

            transform.position = newPosition;
        }
        else
        {
            isPanning = false;
        }
    }

    void HandleZoomInput()
    {
        if (!enableZoom) return;

        // Get scroll wheel input
        Vector2 scroll = mouse.scroll.ReadValue();

        if (Mathf.Abs(scroll.y) > 0.01f)
        {
            // Calculate zoom speed based on distance to terrain (slower when closer)
            float terrainHeight = GetTerrainHeightAt(transform.position);
            float heightAboveTerrain = transform.position.y - terrainHeight;
            float dynamicZoomSpeed = Mathf.Max(minZoomSpeed, heightAboveTerrain * 0.1f);

            // Zoom in/out along forward direction
            float zoomAmount = scroll.y * dynamicZoomSpeed * zoomSpeed * 0.01f;
            Vector3 zoomMove = transform.forward * zoomAmount;

            Vector3 newPosition = transform.position + zoomMove;
            newPosition = ApplyBoundaries(newPosition);

            if (enableCollision)
                newPosition = CheckAndResolveCollision(newPosition);

            transform.position = newPosition;
        }
    }

    void HandleMovementInput()
    {
        // Get input direction
        Vector3 inputDirection = Vector3.zero;

        // WASD movement
        if (keyboard.wKey.isPressed) inputDirection += Vector3.forward;
        if (keyboard.sKey.isPressed) inputDirection += Vector3.back;
        if (keyboard.aKey.isPressed) inputDirection += Vector3.left;
        if (keyboard.dKey.isPressed) inputDirection += Vector3.right;

        // Vertical movement (Space for up, Ctrl for down, also E/Q)
        if (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed)
            inputDirection += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || keyboard.qKey.isPressed)
            inputDirection += Vector3.down;

        // Normalize to prevent faster diagonal movement
        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();

        // Calculate speed multiplier (but not if Ctrl is used for vertical movement)
        float speedMultiplier = 1f;
        bool shiftForSpeed = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) && !isPanning;
        if (shiftForSpeed)
            speedMultiplier = sprintMultiplier;

        // Transform input to world space relative to camera
        Vector3 moveDirection = transform.TransformDirection(inputDirection);

        // Calculate target velocity
        targetVelocity = moveDirection * moveSpeed * speedMultiplier;

        // Smooth velocity
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, movementSmoothing * 60f * Time.deltaTime);

        // Calculate new position
        Vector3 newPosition = transform.position + currentVelocity * Time.deltaTime;

        // Apply boundary constraints
        newPosition = ApplyBoundaries(newPosition);

        // Apply collision detection
        if (enableCollision)
        {
            newPosition = CheckAndResolveCollision(newPosition);
        }

        // Update position
        transform.position = newPosition;
    }

    Vector3 CheckAndResolveCollision(Vector3 targetPosition)
    {
        Vector3 safePosition = targetPosition;
        Vector3 pushBack = Vector3.zero;
        int hitCount = 0;

        // Check multiple directions around the camera
        Vector3[] directions = new Vector3[collisionRayCount + 2]; // +2 for up and down

        // Radial directions (forward, back, sides, diagonals)
        for (int i = 0; i < collisionRayCount; i++)
        {
            float angle = (360f / collisionRayCount) * i * Mathf.Deg2Rad;
            Vector3 localDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            directions[i] = transform.TransformDirection(localDir);
        }

        // Additional up and down checks
        directions[collisionRayCount] = Vector3.down;
        directions[collisionRayCount + 1] = Vector3.up;

        // Check each direction
        foreach (Vector3 dir in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(targetPosition, dir, out hit, collisionDistance, collisionLayers))
            {
                // Calculate push-back vector
                Vector3 pushDirection = targetPosition - hit.point;
                float penetrationDepth = collisionDistance - hit.distance;
                pushBack += pushDirection.normalized * penetrationDepth * pushBackForce;
                hitCount++;

                // Debug visualization
                Debug.DrawLine(targetPosition, hit.point, Color.red);
            }
            else
            {
                Debug.DrawRay(targetPosition, dir * collisionDistance, Color.green);
            }
        }

        // Apply accumulated push-back
        if (hitCount > 0)
        {
            safePosition += pushBack;

            // Ensure we're not pushed through other geometry
            RaycastHit validateHit;
            Vector3 moveDir = safePosition - transform.position;
            float moveDist = moveDir.magnitude;

            if (moveDist > 0.001f && Physics.SphereCast(transform.position, collisionDistance * 0.5f,
                moveDir.normalized, out validateHit, moveDist, collisionLayers))
            {
                // Stop at safe distance from the hit
                safePosition = transform.position + moveDir.normalized * Mathf.Max(0, validateHit.distance - collisionDistance * 0.5f);
            }
        }

        return safePosition;
    }

    Vector3 ApplyBoundaries(Vector3 position)
    {
        if (terrainGenerator == null) return position;

        // Get terrain center
        Vector3 terrainCenter = terrainGenerator.transform.position;

        // Check distance from center (XZ plane)
        Vector3 horizontalOffset = new Vector3(position.x - terrainCenter.x, 0, position.z - terrainCenter.z);
        if (horizontalOffset.magnitude > maxDistanceFromCenter)
        {
            horizontalOffset = horizontalOffset.normalized * maxDistanceFromCenter;
            position.x = terrainCenter.x + horizontalOffset.x;
            position.z = terrainCenter.z + horizontalOffset.z;
        }

        // Check height above terrain
        float terrainHeight = GetTerrainHeightAt(position);

        // Minimum height constraint
        if (position.y < terrainHeight + minHeightAboveTerrain)
        {
            position.y = terrainHeight + minHeightAboveTerrain;
        }

        // Maximum height constraint
        if (position.y > terrainHeight + maxHeightAboveTerrain)
        {
            position.y = terrainHeight + maxHeightAboveTerrain;
        }

        return position;
    }

    float GetTerrainHeightAt(Vector3 worldPosition)
    {
        // Raycast down to find terrain height
        RaycastHit hit;
        Vector3 rayStart = worldPosition + Vector3.up * 1000f;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, 2000f, collisionLayers))
        {
            return hit.point.y;
        }

        // Fallback
        return terrainGenerator != null ? terrainGenerator.transform.position.y : 0f;
    }

    void OnDrawGizmosSelected()
    {
        // Draw collision detection sphere
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, collisionDistance);

        // Draw collision rays
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < collisionRayCount; i++)
            {
                float angle = (360f / collisionRayCount) * i * Mathf.Deg2Rad;
                Vector3 localDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 worldDir = transform.TransformDirection(localDir);
                Gizmos.DrawRay(transform.position, worldDir * collisionDistance);
            }

            // Up/Down rays
            Gizmos.DrawRay(transform.position, Vector3.down * collisionDistance);
            Gizmos.DrawRay(transform.position, Vector3.up * collisionDistance);
        }

        // Draw boundary cylinder
        if (terrainGenerator != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = terrainGenerator.transform.position;

            // Draw horizontal boundary circle
            int segments = 32;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 point1 = center + new Vector3(
                    Mathf.Cos(angle1) * maxDistanceFromCenter,
                    transform.position.y,
                    Mathf.Sin(angle1) * maxDistanceFromCenter
                );

                Vector3 point2 = center + new Vector3(
                    Mathf.Cos(angle2) * maxDistanceFromCenter,
                    transform.position.y,
                    Mathf.Sin(angle2) * maxDistanceFromCenter
                );

                Gizmos.DrawLine(point1, point2);
            }
        }
    }
}