using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Building Settings")]
    public int buildingWidth = 5;

    private TerrainGenerator terrainGenerator;
    private float[] heightMap;
    private int mapSizeWithBorder;
    private int mapSize;
    private int erosionBrushRadius;

    void Awake()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
    }

    public void PlaceBuilding(Vector3 worldPosition)
    {
        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            return;
        }

        // Get terrain data
        mapSize = terrainGenerator.mapSize;
        erosionBrushRadius = terrainGenerator.erosionBrushRadius;
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;

        // Access the heightmap through reflection since it's private
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

        if (!IsValidPlacement(mapCoord))
        {
            Debug.LogWarning("Invalid building placement location!");
            return;
        }

        // Flatten the terrain area
        FlattenTerrainArea(mapCoord);

        // Rebuild chunks and navmesh
        RebuildTerrain();

        Debug.Log($"Building placed at map coordinates: {mapCoord}");
    }

    Vector2Int WorldToMapCoordinates(Vector3 worldPosition)
    {
        // Convert world position to normalized coordinates (-1 to 1)
        float normalizedX = worldPosition.x / terrainGenerator.scale;
        float normalizedZ = worldPosition.z / terrainGenerator.scale;

        // Convert to map coordinates (0 to mapSize-1)
        int mapX = Mathf.RoundToInt((normalizedX + 1f) * 0.5f * (mapSize - 1));
        int mapZ = Mathf.RoundToInt((normalizedZ + 1f) * 0.5f * (mapSize - 1));

        return new Vector2Int(mapX, mapZ);
    }

    bool IsValidPlacement(Vector2Int mapCoord)
    {
        int halfWidth = buildingWidth / 2;

        return mapCoord.x - halfWidth >= 0 &&
               mapCoord.x + halfWidth < mapSize &&
               mapCoord.y - halfWidth >= 0 &&
               mapCoord.y + halfWidth < mapSize;
    }

    void FlattenTerrainArea(Vector2Int centerCoord)
    {
        int halfWidth = buildingWidth / 2;
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
    }

    void RebuildTerrain()
    {
        // Use reflection to call private ConstructChunkedMesh method
        var constructMethod = typeof(TerrainGenerator).GetMethod("ConstructChunkedMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        constructMethod.Invoke(terrainGenerator, null);

        // Use reflection to call private BakeNavMesh method
        var bakeMethod = typeof(TerrainGenerator).GetMethod("BakeNavMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bakeMethod.Invoke(terrainGenerator, null);

        Debug.Log("Terrain chunks and NavMesh updated!");
    }

    public void PlaceRandomBuilding()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();

        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            return;
        }

        // Initialize terrain data
        int currentMapSize = terrainGenerator.mapSize;

        // Pick a random position within valid bounds
        int halfWidth = buildingWidth / 2;
        int randomX = Random.Range(halfWidth, currentMapSize - halfWidth);
        int randomZ = Random.Range(halfWidth, currentMapSize - halfWidth);

        // Convert to world position
        float normalizedX = randomX / (currentMapSize - 1f);
        float normalizedZ = randomZ / (currentMapSize - 1f);
        float worldX = (normalizedX * 2f - 1f) * terrainGenerator.scale;
        float worldZ = (normalizedZ * 2f - 1f) * terrainGenerator.scale;

        Vector3 worldPos = new Vector3(worldX, 0, worldZ);
        PlaceBuilding(worldPos);
    }
}