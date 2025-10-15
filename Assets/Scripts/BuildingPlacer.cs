using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BuildingData
{
    public GameObject prefab;
    public int width = 5;
    public string name;
    public float maxSlopeDeviation = 2f;
}

public class BuildingPlacer : MonoBehaviour
{
    [Header("Building Settings")]
    public List<BuildingData> buildings = new List<BuildingData>();
    public int selectedBuildingIndex = 0;

    private const float VERTEX_SPACING = 0.001f;
    private TerrainGenerator terrainGenerator;
    private float[] heightMap;
    private int mapSizeWithBorder;
    private int mapSize;
    private int erosionBrushRadius;
    private List<GameObject> placedBuildings = new List<GameObject>();
    private Transform buildingsHolder;

    void EnsureBuildingsHolder()
    {
        if (buildingsHolder == null)
        {
            buildingsHolder = transform.Find("Buildings");
        }

        if (buildingsHolder == null)
        {
            GameObject holder = new GameObject("Buildings");
            holder.transform.parent = transform;
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            buildingsHolder = holder.transform;
        }
    }

    public void PlaceBuilding(Vector3 worldPosition)
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = GetComponent<TerrainGenerator>();
        }

        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            return;
        }

        EnsureBuildingsHolder();

        if (buildings.Count == 0)
        {
            Debug.LogError("No buildings configured!");
            return;
        }

        if (selectedBuildingIndex < 0 || selectedBuildingIndex >= buildings.Count)
        {
            Debug.LogError("Invalid building index!");
            return;
        }

        BuildingData selectedBuilding = buildings[selectedBuildingIndex];
        if (selectedBuilding.prefab == null)
        {
            Debug.LogError("Selected building has no prefab assigned!");
            return;
        }

        // Get terrain data
        mapSize = terrainGenerator.mapSize;
        erosionBrushRadius = terrainGenerator.erosionBrushRadius;
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;

        // Access the heightmap through reflection
        var mapField = typeof(TerrainGenerator).GetField("map",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        heightMap = (float[])mapField.GetValue(terrainGenerator);

        if (heightMap == null || heightMap.Length == 0)
        {
            Debug.LogError("Heightmap not generated. Generate terrain first!");
            return;
        }

        // Convert world position to map coordinates
        Vector2Int mapCoord = WorldToMapCoordinates(worldPosition);

        if (!IsValidPlacement(mapCoord, selectedBuilding.width, selectedBuilding.maxSlopeDeviation))
        {
            Debug.LogWarning($"Invalid building placement location! Terrain too steep or out of bounds.");
            return;
        }

        // Flatten the terrain area and get the height
        float flattenedHeight = FlattenTerrainArea(mapCoord, selectedBuilding.width);

        // Rebuild terrain
        RebuildTerrain();

        // Convert map coordinates back to world position for accurate placement
        Vector3 buildingPosition = MapToWorldCoordinates(mapCoord, flattenedHeight);
        GameObject buildingInstance = Instantiate(selectedBuilding.prefab, buildingPosition, Quaternion.identity);
        buildingInstance.name = $"{selectedBuilding.name ?? selectedBuilding.prefab.name} ({placedBuildings.Count})";
        buildingInstance.transform.parent = buildingsHolder;
        placedBuildings.Add(buildingInstance);

        Debug.Log($"Building placed at {buildingPosition}");
    }

    Vector2Int WorldToMapCoordinates(Vector3 worldPosition)
    {
        // Convert world position to local space of terrain
        MeshFilter meshFilter = terrainGenerator.GetMeshFilter();
        if (meshFilter != null && meshFilter.transform != null)
        {
            worldPosition = meshFilter.transform.InverseTransformPoint(worldPosition);
        }

        // With 0.001 spacing, center is at (mapSize-1)/2
        float centerOffset = (mapSize - 1) * 0.5f;

        int mapX = Mathf.RoundToInt(worldPosition.x / VERTEX_SPACING + centerOffset);
        int mapZ = Mathf.RoundToInt(worldPosition.z / VERTEX_SPACING + centerOffset);

        return new Vector2Int(mapX, mapZ);
    }

    Vector3 MapToWorldCoordinates(Vector2Int mapCoord, float worldHeight)
    {
        float centerOffset = (mapSize - 1) * 0.5f;

        float localX = (mapCoord.x - centerOffset) * VERTEX_SPACING;
        float localZ = (mapCoord.y - centerOffset) * VERTEX_SPACING;

        Vector3 localPos = new Vector3(localX, worldHeight, localZ);

        // Convert local position to world space
        MeshFilter meshFilter = terrainGenerator.GetMeshFilter();
        if (meshFilter != null && meshFilter.transform != null)
        {
            return meshFilter.transform.TransformPoint(localPos);
        }

        return localPos;
    }

    bool IsValidPlacement(Vector2Int mapCoord, int width, float maxSlopeDeviation)
    {
        int halfWidth = width / 2;

        // Check bounds
        if (mapCoord.x - halfWidth < 0 ||
            mapCoord.x + halfWidth >= mapSize ||
            mapCoord.y - halfWidth < 0 ||
            mapCoord.y + halfWidth >= mapSize)
            return false;

        // Check slope deviation (height difference across building area)
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int z = -halfWidth; z <= halfWidth; z++)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                int mapX = mapCoord.x + x;
                int mapZ = mapCoord.y + z;
                int borderedIndex = (mapZ + erosionBrushRadius) * mapSizeWithBorder +
                                   (mapX + erosionBrushRadius);
                float height = heightMap[borderedIndex] * terrainGenerator.elevationScale;

                if (height < minHeight) minHeight = height;
                if (height > maxHeight) maxHeight = height;
            }
        }

        float heightDifference = maxHeight - minHeight;
        return heightDifference <= maxSlopeDeviation;
    }

    float FlattenTerrainArea(Vector2Int centerCoord, int width)
    {
        int halfWidth = width / 2;
        List<float> heights = new List<float>();

        // Collect all heights in the building area
        for (int z = -halfWidth; z <= halfWidth; z++)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                int mapX = centerCoord.x + x;
                int mapZ = centerCoord.y + z;
                int borderedIndex = (mapZ + erosionBrushRadius) * mapSizeWithBorder +
                                   (mapX + erosionBrushRadius);
                heights.Add(heightMap[borderedIndex]);
            }
        }

        // Calculate median height
        heights.Sort();
        float medianHeight = heights[heights.Count / 2];

        // Flatten all vertices to median height
        for (int z = -halfWidth; z <= halfWidth; z++)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                int mapX = centerCoord.x + x;
                int mapZ = centerCoord.y + z;
                int borderedIndex = (mapZ + erosionBrushRadius) * mapSizeWithBorder +
                                   (mapX + erosionBrushRadius);
                heightMap[borderedIndex] = medianHeight;
            }
        }

        // Write back the modified heightmap
        var mapField = typeof(TerrainGenerator).GetField("map",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        mapField.SetValue(terrainGenerator, heightMap);

        // Convert to world height
        return medianHeight * terrainGenerator.elevationScale;
    }

    void RebuildTerrain()
    {
        var constructMethod = typeof(TerrainGenerator).GetMethod("ConstructChunkedMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        constructMethod.Invoke(terrainGenerator, null);

        var bakeMethod = typeof(TerrainGenerator).GetMethod("BakeNavMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bakeMethod.Invoke(terrainGenerator, null);
    }

    public void PlaceRandomBuilding()
    {
        EnsureBuildingsHolder();

        if (buildings.Count == 0)
        {
            Debug.LogError("No buildings configured!");
            return;
        }

        if (selectedBuildingIndex < 0 || selectedBuildingIndex >= buildings.Count)
        {
            Debug.LogError("Invalid building index!");
            return;
        }

        terrainGenerator = GetComponent<TerrainGenerator>();
        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            return;
        }

        BuildingData selectedBuilding = buildings[selectedBuildingIndex];
        int currentMapSize = terrainGenerator.mapSize;
        int halfWidth = selectedBuilding.width / 2;

        // Try multiple times to find valid placement
        int maxAttempts = 100;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int randomX = Random.Range(halfWidth, currentMapSize - halfWidth);
            int randomZ = Random.Range(halfWidth, currentMapSize - halfWidth);

            Vector3 worldPos = MapToWorldCoordinates(new Vector2Int(randomX, randomZ), 0);

            // Check if this location is valid
            mapSize = terrainGenerator.mapSize;
            erosionBrushRadius = terrainGenerator.erosionBrushRadius;
            mapSizeWithBorder = mapSize + erosionBrushRadius * 2;

            var mapField = typeof(TerrainGenerator).GetField("map",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            heightMap = (float[])mapField.GetValue(terrainGenerator);

            Vector2Int mapCoord = new Vector2Int(randomX, randomZ);

            if (IsValidPlacement(mapCoord, selectedBuilding.width, selectedBuilding.maxSlopeDeviation))
            {
                PlaceBuilding(worldPos);
                return;
            }
        }

        Debug.LogWarning($"Could not find valid placement location after {maxAttempts} attempts. Try adjusting maxSlopeDeviation.");
    }

    public void ClearAllBuildings()
    {
        // Clear tracked buildings
        foreach (var building in placedBuildings)
        {
            if (building != null)
                DestroyImmediate(building);
        }
        placedBuildings.Clear();

        // Find and clear the holder's children
        Transform holder = transform.Find("Buildings");
        if (holder != null)
        {
            while (holder.childCount > 0)
            {
                DestroyImmediate(holder.GetChild(0).gameObject);
            }
        }

        buildingsHolder = null; // Reset reference so it can be recreated
    }
}