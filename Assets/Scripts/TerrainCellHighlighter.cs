using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Visual highlighter for building placement on terrain.
/// This component is driven entirely by BuildingPlacer - it displays a grid overlay
/// and validates placement based on the currently selected building's properties.
/// 
/// Features:
/// - Automatically syncs grid size with selected building width
/// - Uses building's maxSlopeDeviation for validation
/// - Shows green highlight for valid placement, red for invalid
/// - Optional click-to-place functionality
/// - Real-time height difference feedback in Scene view
/// 
/// Usage:
/// 1. Add this component to the same GameObject as BuildingPlacer
/// 2. Configure visual settings (colors, intensity)
/// 3. All validation logic comes from BuildingPlacer's selected building
/// </summary>
[RequireComponent(typeof(TerrainGenerator))]
public class TerrainCellHighlighter : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Color of highlighted cells when valid")]
    public Color validColor = new Color(0f, 1f, 1f, 0.8f);

    [Tooltip("Color of highlighted cells when invalid")]
    public Color invalidColor = new Color(1f, 0f, 0f, 0.8f);

    [Range(0f, 1f)]
    [Tooltip("Intensity of the highlight effect")]
    public float highlightIntensity = 0.8f;

    [Header("Placement Settings")]
    [Tooltip("Enable click to place building")]
    public bool enableClickPlacement = true;

    [Tooltip("Snap placement to grid centers (recommended for alignment)")]
    public bool snapToGridCenter = true;

    [Tooltip("Mouse button for placement (0=Left, 1=Right, 2=Middle)")]
    public int placementMouseButton = 0;

    [Tooltip("Prevent placement if mouse moved (dragging camera)")]
    public bool preventPlacementWhileDragging = true;

    [Tooltip("Maximum mouse movement in pixels to count as a click (not a drag)")]
    public float maxClickDragDistance = 5f;

    [Tooltip("Cooldown between placements in seconds")]
    public float placementCooldown = 0.2f;

    [Header("Raycast Settings")]
    [Tooltip("Layer mask for raycasting (should include terrain)")]
    public LayerMask terrainLayer = -1;

    private TerrainGenerator terrainGenerator;
    private BuildingPlacer buildingPlacer;
    private Material terrainMaterial;
    private Camera mainCamera;
    private Vector3 lastHitPoint = Vector3.zero;
    private bool isHighlighting = false;
    private bool isCurrentLocationValid = false;
    private Mouse mouse;

    // Current values from BuildingPlacer
    private int currentHighlightRadius = 0;
    private float currentMaxHeightDeviation = 0f;
    private bool hasValidBuilding = false;

    // Placement tracking
    private Vector2 mouseDownPosition;
    private float lastPlacementTime = -999f;
    private bool wasMouseDownThisFrame = false;

    // Debug info
    private float lastHeightDifference = 0f;

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
        buildingPlacer = GetComponent<BuildingPlacer>();
        mainCamera = Camera.main;
        mouse = Mouse.current;

        if (buildingPlacer == null)
        {
            Debug.LogError("TerrainCellHighlighter: BuildingPlacer component not found! This component requires BuildingPlacer.");
            enabled = false;
            return;
        }

        // Get the terrain material
        if (terrainGenerator != null && terrainGenerator.material != null)
        {
            terrainMaterial = terrainGenerator.material;
            UpdateShaderProperties(Vector3.zero, true);
        }
        else
        {
            Debug.LogError("TerrainCellHighlighter: Could not find terrain material!");
        }
    }

    void Update()
    {
        if (terrainMaterial == null || mainCamera == null || mouse == null) return;

        // Get values from BuildingPlacer's selected building
        if (!UpdateFromBuildingPlacer())
        {
            // No valid building selected - disable highlight
            if (isHighlighting)
            {
                isHighlighting = false;
                isCurrentLocationValid = false;
                terrainMaterial.SetFloat("_HighlightRadius", 0);
            }
            return;
        }

        // Get mouse position using new Input System
        Vector2 mousePosition = mouse.position.ReadValue();

        // Raycast from mouse position to terrain
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer))
        {
            if (hit.collider != null)
            {
                // Optionally snap hit point to grid center for consistent alignment
                Vector3 targetPoint = snapToGridCenter ? SnapToGridCenter(hit.point) : hit.point;
                lastHitPoint = targetPoint;
                isHighlighting = true;

                // Check if the highlighted area is valid (using target position)
                isCurrentLocationValid = CheckHeightDeviation(targetPoint, out lastHeightDifference);

                UpdateShaderProperties(targetPoint, isCurrentLocationValid);

                // Handle click placement
                if (enableClickPlacement)
                {
                    HandlePlacementInput();
                }
            }
        }
        else
        {
            // No hit, disable highlighting
            if (isHighlighting)
            {
                isHighlighting = false;
                isCurrentLocationValid = false;
                terrainMaterial.SetFloat("_HighlightRadius", 0);
            }
        }
    }

    void HandlePlacementInput()
    {
        if (buildingPlacer == null) return;

        // Track mouse down position
        bool isMouseDown = false;
        if (placementMouseButton == 0 && mouse.leftButton.isPressed)
            isMouseDown = true;
        else if (placementMouseButton == 1 && mouse.rightButton.isPressed)
            isMouseDown = true;
        else if (placementMouseButton == 2 && mouse.middleButton.isPressed)
            isMouseDown = true;

        if (isMouseDown && !wasMouseDownThisFrame)
        {
            mouseDownPosition = mouse.position.ReadValue();
            wasMouseDownThisFrame = true;
        }
        else if (!isMouseDown)
        {
            wasMouseDownThisFrame = false;
        }

        // Check for mouse button release (click completed)
        bool clickReleased = false;
        if (placementMouseButton == 0 && mouse.leftButton.wasReleasedThisFrame)
            clickReleased = true;
        else if (placementMouseButton == 1 && mouse.rightButton.wasReleasedThisFrame)
            clickReleased = true;
        else if (placementMouseButton == 2 && mouse.middleButton.wasReleasedThisFrame)
            clickReleased = true;

        if (clickReleased)
        {
            // Check if mouse moved too much (was dragging)
            if (preventPlacementWhileDragging)
            {
                Vector2 currentMousePos = mouse.position.ReadValue();
                float dragDistance = Vector2.Distance(mouseDownPosition, currentMousePos);

                if (dragDistance > maxClickDragDistance)
                {
                    // User was dragging, not clicking - don't place
                    return;
                }
            }

            // Check cooldown
            if (Time.time - lastPlacementTime < placementCooldown)
            {
                return;
            }

            // Use the current lastHitPoint which is already snapped if enabled
            Vector3 placementPosition = lastHitPoint;

            // Try to place the building at the position
            if (isCurrentLocationValid)
            {
                buildingPlacer.PlaceBuilding(placementPosition);
                lastPlacementTime = Time.time;
                Debug.Log($"<color=green>Building placed successfully at {placementPosition}</color>");
            }
            else
            {
                Debug.LogWarning($"<color=red>Cannot place building: Invalid location</color>\n" +
                                $"Height difference: {lastHeightDifference:F2}m (max: {currentMaxHeightDeviation:F2}m)");
            }
        }
    }

    /// <summary>
    /// Snaps a world position to the center of the nearest grid cell.
    /// Uses the EXACT same logic as the shader to prevent offset issues.
    /// 
    /// IMPORTANT: Must use Floor, not Round, to match shader's GetGridCell function!
    /// The shader calculates grid cells as: floor((pos + gridOffset) / spacing)
    /// Using Round would cause misalignment when viewing at angles.
    /// </summary>
    Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        if (terrainMaterial == null) return worldPosition;

        float gridSpacing = terrainMaterial.GetFloat("_GridSpacing");
        float gridOffset = terrainMaterial.GetFloat("_GridOffset");

        // Use the same calculation as the shader's GetGridCell function
        // Shader does: floor((pos + gridOffset) / spacing)
        Vector2 pos2D = new Vector2(worldPosition.x, worldPosition.z);
        pos2D += new Vector2(gridOffset, gridOffset);

        // Get grid cell indices (floor, not round!)
        Vector2 cellIndices = new Vector2(
            Mathf.Floor(pos2D.x / gridSpacing),
            Mathf.Floor(pos2D.y / gridSpacing)
        );

        // Convert back to world position at cell center
        Vector2 cellCenter = cellIndices * gridSpacing + new Vector2(gridSpacing * 0.5f, gridSpacing * 0.5f);
        cellCenter -= new Vector2(gridOffset, gridOffset);

        // Keep the Y coordinate from the original raycast
        return new Vector3(cellCenter.x, worldPosition.y, cellCenter.y);
    }

    bool UpdateFromBuildingPlacer()
    {
        if (buildingPlacer == null || buildingPlacer.buildings == null || buildingPlacer.buildings.Count == 0)
        {
            hasValidBuilding = false;
            return false;
        }

        int selectedIndex = buildingPlacer.selectedBuildingIndex;
        if (selectedIndex < 0 || selectedIndex >= buildingPlacer.buildings.Count)
        {
            hasValidBuilding = false;
            return false;
        }

        var selectedBuilding = buildingPlacer.buildings[selectedIndex];
        if (selectedBuilding.prefab == null)
        {
            hasValidBuilding = false;
            return false;
        }

        // Get values from selected building
        currentHighlightRadius = selectedBuilding.width / 2;
        currentMaxHeightDeviation = selectedBuilding.maxSlopeDeviation;
        hasValidBuilding = true;

        return true;
    }

    bool CheckHeightDeviation(Vector3 centerPoint, out float heightDifference)
    {
        heightDifference = 0f;

        if (terrainGenerator == null || !hasValidBuilding) return false;

        float gridSpacing = terrainMaterial.GetFloat("_GridSpacing");
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        int samplesChecked = 0;

        // Sample height at all points in the highlighted area using current building's radius
        int sampleRadius = currentHighlightRadius;

        for (int z = -sampleRadius; z <= sampleRadius; z++)
        {
            for (int x = -sampleRadius; x <= sampleRadius; x++)
            {
                Vector3 samplePoint = centerPoint + new Vector3(x * gridSpacing, 100f, z * gridSpacing);

                // Raycast down to get terrain height
                RaycastHit hit;
                if (Physics.Raycast(samplePoint, Vector3.down, out hit, 200f, terrainLayer))
                {
                    float height = hit.point.y;
                    if (height < minHeight) minHeight = height;
                    if (height > maxHeight) maxHeight = height;
                    samplesChecked++;
                }
            }
        }

        heightDifference = maxHeight - minHeight;

        // Validate we got enough samples
        int expectedSamples = (currentHighlightRadius * 2 + 1) * (currentHighlightRadius * 2 + 1);
        if (samplesChecked < expectedSamples * 0.5f)
        {
            // Less than 50% of expected samples - probably invalid location
            return false;
        }

        // Use current building's max height deviation
        return heightDifference <= currentMaxHeightDeviation;
    }

    void UpdateShaderProperties(Vector3 worldPosition, bool isValid)
    {
        if (terrainMaterial == null) return;

        terrainMaterial.SetVector("_HighlightCenter", worldPosition);
        terrainMaterial.SetFloat("_HighlightRadius", currentHighlightRadius);
        terrainMaterial.SetColor("_HighlightColor", validColor);
        terrainMaterial.SetColor("_InvalidColor", invalidColor);
        terrainMaterial.SetFloat("_HighlightIntensity", highlightIntensity);
        terrainMaterial.SetFloat("_IsValidPlacement", isValid ? 1f : 0f);
    }

    void OnDisable()
    {
        if (terrainMaterial != null)
        {
            terrainMaterial.SetFloat("_HighlightRadius", 0);
        }
    }

    void OnDrawGizmos()
    {
        if (!isHighlighting || !hasValidBuilding || terrainGenerator == null || terrainMaterial == null)
            return;

        Gizmos.color = isCurrentLocationValid ? validColor : invalidColor;

        float cellSize = terrainMaterial.GetFloat("_GridSpacing");
        float radius = currentHighlightRadius * cellSize;

        Vector3 center = lastHitPoint;
        center.y += 0.1f;

        // Draw square
        Vector3 topLeft = center + new Vector3(-radius, 0, radius);
        Vector3 topRight = center + new Vector3(radius, 0, radius);
        Vector3 bottomRight = center + new Vector3(radius, 0, -radius);
        Vector3 bottomLeft = center + new Vector3(-radius, 0, -radius);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);

        // Draw center point to show grid snapping (only when enabled)
        if (snapToGridCenter)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastHitPoint, 0.05f);
        }

        // Draw text label in scene view
#if UNITY_EDITOR
        string buildingName = "None";
        if (buildingPlacer != null && buildingPlacer.buildings != null)
        {
            int idx = buildingPlacer.selectedBuildingIndex;
            if (idx >= 0 && idx < buildingPlacer.buildings.Count)
            {
                var building = buildingPlacer.buildings[idx];
                buildingName = !string.IsNullOrEmpty(building.name) ? building.name :
                              building.prefab != null ? building.prefab.name : $"Building {idx}";
            }
        }

        string placementHint = enableClickPlacement ?
            $"\n[{GetMouseButtonName(placementMouseButton)} Click to Place]" : "";

        UnityEditor.Handles.Label(center + Vector3.up * 2f,
            $"Building: {buildingName}\n" +
            $"Grid Size: {currentHighlightRadius * 2 + 1}x{currentHighlightRadius * 2 + 1}\n" +
            $"Height Diff: {lastHeightDifference:F2}m / {currentMaxHeightDeviation:F2}m\n" +
            $"Status: {(isCurrentLocationValid ? "VALID ?" : "INVALID ?")}" +
            placementHint);
#endif
    }

    string GetMouseButtonName(int button)
    {
        switch (button)
        {
            case 0: return "Left";
            case 1: return "Right";
            case 2: return "Middle";
            default: return "Unknown";
        }
    }
}