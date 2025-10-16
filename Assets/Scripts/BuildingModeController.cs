// Runtime controller for building mode
using UnityEngine;

[RequireComponent(typeof(TerrainGenerator))]
[ExecuteInEditMode]
public class BuildingModeController : MonoBehaviour
{
    [Header("Building Mode Settings")]
    [Tooltip("Enable building mode to show grid and placement tools")]
    public bool buildingModeEnabled = false;

    [Header("Grid Visibility Settings")]
    [Tooltip("Maximum distance from camera to show grid")]
    public float gridVisibilityDistance = 30f;

    [Tooltip("Distance where grid starts to fade")]
    public float gridFadeStartDistance = 10f;

    private TerrainGenerator terrainGenerator;
    private Material terrainMaterial;
    private bool lastBuildingModeState;
    private Camera mainCamera;

    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    void Initialize()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
        mainCamera = Camera.main;

        if (terrainGenerator != null && terrainGenerator.material != null)
        {
            terrainMaterial = terrainGenerator.material;
            UpdateGridVisibility();
        }

        lastBuildingModeState = buildingModeEnabled;
    }

    void Update()
    {
        // Ensure we have references
        if (terrainMaterial == null && terrainGenerator != null)
        {
            terrainMaterial = terrainGenerator.material;
        }

        // Check if building mode state changed
        if (buildingModeEnabled != lastBuildingModeState)
        {
            UpdateGridVisibility();
            lastBuildingModeState = buildingModeEnabled;
        }

        // Update grid fade distances if in building mode
        if (buildingModeEnabled && terrainMaterial != null)
        {
            UpdateGridFadeDistances();
        }
    }

    void UpdateGridVisibility()
    {
        if (terrainMaterial == null) return;

        // Enable or disable grid based on building mode
        terrainMaterial.SetFloat("_ShowGrid", buildingModeEnabled ? 1f : 0f);

        // Also enable/disable the highlighter component if it exists
        TerrainCellHighlighter highlighter = GetComponent<TerrainCellHighlighter>();
        if (highlighter != null)
        {
            highlighter.enabled = buildingModeEnabled;
        }

        Debug.Log($"Building Mode: {(buildingModeEnabled ? "ENABLED" : "DISABLED")}");
    }

    void UpdateGridFadeDistances()
    {
        // Update fade distances in the material
        terrainMaterial.SetFloat("_GridFadeStart", gridFadeStartDistance);
        terrainMaterial.SetFloat("_GridFadeEnd", gridVisibilityDistance);
    }

    // Public method to toggle building mode from other scripts
    public void ToggleBuildingMode()
    {
        buildingModeEnabled = !buildingModeEnabled;
        UpdateGridVisibility();
    }

    // Public method to set building mode state
    public void SetBuildingMode(bool enabled)
    {
        buildingModeEnabled = enabled;
        UpdateGridVisibility();
    }

    void OnValidate()
    {
        // Ensure fade start is less than visibility distance
        if (gridFadeStartDistance > gridVisibilityDistance)
        {
            gridFadeStartDistance = gridVisibilityDistance * 0.5f;
        }

        // Update settings in real-time when changed in inspector
        if (Application.isPlaying && terrainMaterial != null)
        {
            UpdateGridVisibility();
        }
    }
}