using UnityEngine;
using VoxelTerrain.Data;
using System.Collections.Generic;

namespace VoxelTerrain.Debug
{
    /// <summary>
    /// Visualizes resources and terrain data in the scene view
    /// Attach this to the same GameObject as TerrainGenerator
    /// </summary>
    public class ResourceVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool showResources = true;
        [SerializeField] private bool showResourceDensity = true;
        [SerializeField] private bool showTerrainTypes = false;
        [SerializeField] private bool showHeightLabels = false;

        [Header("Filter by Resource Type")]
        [SerializeField] private bool filterByType = false;
        [SerializeField] private ResourceType resourceFilter = ResourceType.IronOre;

        [Header("Display Options")]
        [SerializeField] private float iconHeight = 2f;
        [SerializeField] private float iconScale = 1f;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelDistance = 30f;

        [Header("Performance")]
        [SerializeField] private int maxVisibleResources = 500;

        private TerrainDataManager dataManager;
        private Camera sceneCamera;

        private void OnEnable()
        {
            dataManager = GetComponent<TerrainDataManager>();
        }

        private void OnDrawGizmos()
        {
            if (!showResources && !showTerrainTypes) return;

            dataManager = GetComponent<TerrainDataManager>();
            if (dataManager == null || !Application.isPlaying) return;

            sceneCamera = Camera.current;
            if (sceneCamera == null) return;

            if (showResources)
            {
                DrawResources();
            }

            if (showTerrainTypes)
            {
                DrawTerrainTypes();
            }
        }

        private void DrawResources()
        {
            var stats = dataManager.GetResourceStatistics();
            int drawn = 0;

            foreach (var kvp in stats)
            {
                if (filterByType && kvp.Key != resourceFilter)
                    continue;

                var locations = dataManager.FindAllResources(kvp.Key);
                Color resourceColor = GetResourceColor(kvp.Key);

                foreach (var gridPos in locations)
                {
                    if (drawn >= maxVisibleResources)
                        break;

                    Vector3 worldPos = dataManager.GridToWorld(gridPos);
                    float distToCamera = Vector3.Distance(worldPos, sceneCamera.transform.position);

                    // LOD: Only draw if within reasonable distance
                    if (distToCamera > 100f)
                        continue;

                    byte density = dataManager.GetResourceDensity(gridPos.x, gridPos.y);

                    // Size based on density
                    float size = iconScale * 0.5f;
                    if (showResourceDensity)
                    {
                        size += iconScale * (density / 255f) * 0.8f;
                    }

                    // Draw sphere
                    Gizmos.color = resourceColor;
                    Gizmos.DrawSphere(worldPos + Vector3.up * iconHeight, size);

                    // Draw wireframe for better visibility
                    Gizmos.color = resourceColor * 0.7f;
                    Gizmos.DrawWireSphere(worldPos + Vector3.up * iconHeight, size + 0.1f);

                    // Draw label
                    if (showLabels && distToCamera < labelDistance)
                    {
                        DrawLabel(worldPos + Vector3.up * (iconHeight + size + 0.5f),
                                 $"{kvp.Key}\n{density}/255",
                                 resourceColor);
                    }

                    drawn++;
                }

                if (drawn >= maxVisibleResources)
                    break;
            }
        }

        private void DrawTerrainTypes()
        {
            // Sample grid at lower resolution for performance
            int step = Mathf.Max(1, dataManager.MapSize / 50);

            for (int z = 0; z < dataManager.MapSize; z += step)
            {
                for (int x = 0; x < dataManager.MapSize; x += step)
                {
                    Vector3 worldPos = dataManager.GridToWorld(new Vector2Int(x, z));
                    float distToCamera = Vector3.Distance(worldPos, sceneCamera.transform.position);

                    if (distToCamera > 50f)
                        continue;

                    TerrainType terrain = dataManager.GetTerrainType(x, z);
                    Color terrainColor = GetTerrainColor(terrain);

                    Gizmos.color = terrainColor;
                    Gizmos.DrawCube(worldPos + Vector3.up * 0.5f, Vector3.one * 0.3f);

                    if (showHeightLabels && distToCamera < 20f)
                    {
                        float height = dataManager.GetHeight(x, z);
                        DrawLabel(worldPos + Vector3.up * 1f,
                                 $"{terrain}\n{height:F1}m",
                                 terrainColor);
                    }
                }
            }
        }

        private void DrawLabel(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = 10;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            UnityEditor.Handles.Label(position, text, style);
#endif
        }

        private Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.IronOre: return new Color(0.7f, 0.7f, 0.8f);
                case ResourceType.CopperOre: return new Color(0.9f, 0.6f, 0.3f);
                case ResourceType.GoldOre: return new Color(1f, 0.9f, 0.2f);
                case ResourceType.CoalDeposit: return new Color(0.2f, 0.2f, 0.2f);
                case ResourceType.Stone: return Color.gray;
                case ResourceType.Granite: return new Color(0.6f, 0.5f, 0.5f);
                case ResourceType.Limestone: return new Color(0.9f, 0.9f, 0.8f);
                case ResourceType.Clay: return new Color(0.7f, 0.6f, 0.5f);
                case ResourceType.Sand: return new Color(1f, 0.9f, 0.7f);
                case ResourceType.Gravel: return new Color(0.6f, 0.6f, 0.65f);
                case ResourceType.CrystalDeposit: return new Color(0.5f, 0.8f, 1f);
                default: return Color.white;
            }
        }

        private Color GetTerrainColor(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Grass: return new Color(0.4f, 0.7f, 0.3f);
                case TerrainType.Sand: return new Color(1f, 0.9f, 0.6f);
                case TerrainType.Rock: return new Color(0.5f, 0.5f, 0.5f);
                case TerrainType.Snow: return new Color(1f, 1f, 1f);
                case TerrainType.Dirt: return new Color(0.6f, 0.5f, 0.4f);
                case TerrainType.Clay: return new Color(0.7f, 0.6f, 0.5f);
                case TerrainType.Gravel: return new Color(0.6f, 0.6f, 0.65f);
                case TerrainType.Tundra: return new Color(0.5f, 0.6f, 0.5f);
                default: return Color.white;
            }
        }

        // UI for runtime toggling
        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Resource Visualizer", GUI.skin.box);

            showResources = GUILayout.Toggle(showResources, "Show Resources");
            if (showResources)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                showResourceDensity = GUILayout.Toggle(showResourceDensity, "Show Density (Size)");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                filterByType = GUILayout.Toggle(filterByType, "Filter by Type");
                GUILayout.EndHorizontal();

                if (filterByType)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    GUILayout.Label("Type:");
                    // Simple enum cycling
                    if (GUILayout.Button("<"))
                    {
                        int val = (int)resourceFilter - 1;
                        if (val < 0) val = System.Enum.GetValues(typeof(ResourceType)).Length - 1;
                        resourceFilter = (ResourceType)val;
                    }
                    GUILayout.Label(resourceFilter.ToString());
                    if (GUILayout.Button(">"))
                    {
                        int val = (int)resourceFilter + 1;
                        if (val >= System.Enum.GetValues(typeof(ResourceType)).Length) val = 0;
                        resourceFilter = (ResourceType)val;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            showTerrainTypes = GUILayout.Toggle(showTerrainTypes, "Show Terrain Types");
            if (showTerrainTypes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                showHeightLabels = GUILayout.Toggle(showHeightLabels, "Show Height Labels");
                GUILayout.EndHorizontal();
            }

            showLabels = GUILayout.Toggle(showLabels, "Show Labels");

            GUILayout.Space(10);

            // Statistics
            if (dataManager != null)
            {
                GUILayout.Label("Resource Statistics:", GUI.skin.box);
                var stats = dataManager.GetResourceStatistics();
                foreach (var kvp in stats)
                {
                    Color resourceColor = GetResourceColor(kvp.Key);
                    GUI.contentColor = resourceColor;
                    GUILayout.Label($"  {kvp.Key}: {kvp.Value} deposits");
                }
                GUI.contentColor = Color.white;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}