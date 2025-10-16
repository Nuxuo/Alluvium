using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TerrainGenerator))]
public class TerrainCellHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    [Tooltip("Number of cells to highlight in radius around cursor")]
    public int highlightRadius = 3;

    [Tooltip("Color of highlighted cells when valid")]
    public Color validColor = new Color(0f, 1f, 1f, 0.8f);

    [Tooltip("Color of highlighted cells when invalid")]
    public Color invalidColor = new Color(1f, 0f, 0f, 0.8f);

    [Range(0f, 1f)]
    [Tooltip("Intensity of the highlight effect")]
    public float highlightIntensity = 0.8f;

    [Header("Validation Settings")]
    [Tooltip("Maximum height difference allowed in highlighted area")]
    public float maxHeightDeviation = 2f;

    [Tooltip("Layer mask for raycasting (should include terrain)")]
    public LayerMask terrainLayer = -1;

    private TerrainGenerator terrainGenerator;
    private Material terrainMaterial;
    private Camera mainCamera;
    private Vector3 lastHitPoint = Vector3.zero;
    private bool isHighlighting = false;
    private Mouse mouse;

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
        mainCamera = Camera.main;
        mouse = Mouse.current;

        // Get the terrain material
        if (terrainGenerator != null && terrainGenerator.material != null)
        {
            terrainMaterial = terrainGenerator.material;

            // Initialize shader properties
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

        // Get mouse position using new Input System
        Vector2 mousePosition = mouse.position.ReadValue();

        // Raycast from mouse position to terrain
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer))
        {
            // Check if we hit the terrain
            if (hit.collider != null)
            {
                lastHitPoint = hit.point;
                isHighlighting = true;

                // Check if the highlighted area is valid
                bool isValid = CheckHeightDeviation(hit.point);

                UpdateShaderProperties(hit.point, isValid);
            }
        }
        else
        {
            // No hit, disable highlighting by setting radius to 0
            if (isHighlighting)
            {
                isHighlighting = false;
                terrainMaterial.SetFloat("_HighlightRadius", 0);
            }
        }
    }

    bool CheckHeightDeviation(Vector3 centerPoint)
    {
        if (terrainGenerator == null) return false;

        float gridSpacing = terrainMaterial.GetFloat("_GridSpacing");
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Sample height at multiple points in the highlighted area
        int sampleRadius = highlightRadius;

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
                }
            }
        }

        float heightDifference = maxHeight - minHeight;
        return heightDifference <= maxHeightDeviation;
    }

    void UpdateShaderProperties(Vector3 worldPosition, bool isValid)
    {
        if (terrainMaterial == null) return;

        // Update shader properties
        terrainMaterial.SetVector("_HighlightCenter", worldPosition);
        terrainMaterial.SetFloat("_HighlightRadius", highlightRadius);
        terrainMaterial.SetColor("_HighlightColor", validColor);
        terrainMaterial.SetColor("_InvalidColor", invalidColor);
        terrainMaterial.SetFloat("_HighlightIntensity", highlightIntensity);
        terrainMaterial.SetFloat("_IsValidPlacement", isValid ? 1f : 0f);
    }

    void OnDisable()
    {
        // Clear highlighting when disabled
        if (terrainMaterial != null)
        {
            terrainMaterial.SetFloat("_HighlightRadius", 0);
        }
    }

    // Optional: Draw gizmo to visualize highlight area in editor
    void OnDrawGizmos()
    {
        if (isHighlighting && terrainGenerator != null && terrainMaterial != null)
        {
            // Check validity and use appropriate color
            bool isValid = CheckHeightDeviation(lastHitPoint);
            Gizmos.color = isValid ? validColor : invalidColor;

            float cellSize = terrainMaterial.GetFloat("_GridSpacing");
            float radius = highlightRadius * cellSize;

            // Draw a square at the highlight position
            Vector3 center = lastHitPoint;
            center.y += 0.1f; // Slightly above terrain

            // Draw square
            Vector3 topLeft = center + new Vector3(-radius, 0, radius);
            Vector3 topRight = center + new Vector3(radius, 0, radius);
            Vector3 bottomRight = center + new Vector3(radius, 0, -radius);
            Vector3 bottomLeft = center + new Vector3(-radius, 0, -radius);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}