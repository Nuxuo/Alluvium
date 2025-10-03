using UnityEngine;
using System.Collections.Generic;

namespace VoxelTerrain.Data
{
    /// <summary>
    /// Central manager for terrain data - provides easy querying for gameplay
    /// Attach this to the same GameObject as TerrainGenerator
    /// </summary>
    public class TerrainDataManager : MonoBehaviour
    {
        [Header("Terrain Data")]
        [SerializeField] private int mapSize;
        [SerializeField] private float worldScale;
        [SerializeField] private float elevationScale;

        // Core data structures
        private TerrainCell[,] terrainGrid;
        private float[,] heightMap;

        // Resource lookup for fast queries
        private Dictionary<ResourceType, List<Vector2Int>> resourceLocations;

        public int MapSize => mapSize;
        public float WorldScale => worldScale;

        /// <summary>
        /// Initialize terrain data from generated data
        /// Called by TerrainGenerator after generation
        /// </summary>
        public void Initialize(int size, float scale, float elevation, float[] heightData, TerrainCell[] cellData)
        {
            mapSize = size;
            worldScale = scale;
            elevationScale = elevation;

            // Convert flat arrays to 2D grids
            terrainGrid = new TerrainCell[mapSize, mapSize];
            heightMap = new float[mapSize, mapSize];
            resourceLocations = new Dictionary<ResourceType, List<Vector2Int>>();

            for (int z = 0; z < mapSize; z++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    int index = z * mapSize + x;

                    terrainGrid[x, z] = cellData[index];
                    heightMap[x, z] = heightData[index];

                    // Index resources
                    if (cellData[index].HasResource)
                    {
                        if (!resourceLocations.ContainsKey(cellData[index].resourceType))
                        {
                            resourceLocations[cellData[index].resourceType] = new List<Vector2Int>();
                        }
                        resourceLocations[cellData[index].resourceType].Add(new Vector2Int(x, z));
                    }
                }
            }
        }

        #region World/Grid Conversion

        /// <summary>
        /// Convert world position to grid coordinates
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            // Terrain is centered at origin, spanning from -scale to +scale
            float normalizedX = (worldPos.x / worldScale + 1f) * 0.5f; // 0 to 1
            float normalizedZ = (worldPos.z / worldScale + 1f) * 0.5f;

            int gridX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * mapSize), 0, mapSize - 1);
            int gridZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * mapSize), 0, mapSize - 1);

            return new Vector2Int(gridX, gridZ);
        }

        /// <summary>
        /// Convert grid coordinates to world position (center of cell)
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            float normalizedX = (gridPos.x + 0.5f) / mapSize; // Center of cell
            float normalizedZ = (gridPos.y + 0.5f) / mapSize;

            float worldX = (normalizedX * 2f - 1f) * worldScale;
            float worldZ = (normalizedZ * 2f - 1f) * worldScale;
            float worldY = GetHeight(gridPos.x, gridPos.y);

            return new Vector3(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Check if grid coordinates are valid
        /// </summary>
        public bool IsValidGridPos(int x, int z)
        {
            return x >= 0 && x < mapSize && z >= 0 && z < mapSize;
        }

        public bool IsValidGridPos(Vector2Int pos)
        {
            return IsValidGridPos(pos.x, pos.y);
        }

        #endregion

        #region Terrain Queries

        /// <summary>
        /// Get terrain type at grid position
        /// </summary>
        public TerrainType GetTerrainType(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return TerrainType.Grass;
            return terrainGrid[x, z].terrainType;
        }

        public TerrainType GetTerrainType(Vector2Int pos) => GetTerrainType(pos.x, pos.y);

        /// <summary>
        /// Get height at grid position
        /// </summary>
        public float GetHeight(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return 0f;
            return heightMap[x, z] * elevationScale;
        }

        public float GetHeight(Vector2Int pos) => GetHeight(pos.x, pos.y);

        /// <summary>
        /// Get normalized height (0-1) at grid position
        /// </summary>
        public float GetNormalizedHeight(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return 0f;
            return heightMap[x, z];
        }

        /// <summary>
        /// Get complete terrain cell data
        /// </summary>
        public TerrainCell GetCell(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return new TerrainCell(TerrainType.Grass);
            return terrainGrid[x, z];
        }

        public TerrainCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

        /// <summary>
        /// Calculate slope at position (0 = flat, higher = steeper)
        /// </summary>
        public float GetSlope(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return 0f;

            float cellSize = (worldScale * 2f) / (mapSize - 1);

            float heightL = GetHeight(Mathf.Max(0, x - 1), z);
            float heightR = GetHeight(Mathf.Min(mapSize - 1, x + 1), z);
            float heightD = GetHeight(x, Mathf.Max(0, z - 1));
            float heightU = GetHeight(x, Mathf.Min(mapSize - 1, z + 1));

            float dx = (heightR - heightL) / (2f * cellSize);
            float dz = (heightU - heightD) / (2f * cellSize);

            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        #endregion

        #region Resource Queries

        /// <summary>
        /// Get resource type at position
        /// </summary>
        public ResourceType GetResourceType(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return ResourceType.None;
            return terrainGrid[x, z].resourceType;
        }

        public ResourceType GetResourceType(Vector2Int pos) => GetResourceType(pos.x, pos.y);

        /// <summary>
        /// Get resource density (richness) at position
        /// </summary>
        public byte GetResourceDensity(int x, int z)
        {
            if (!IsValidGridPos(x, z)) return 0;
            return terrainGrid[x, z].resourceDensity;
        }

        public byte GetResourceDensity(Vector2Int pos) => GetResourceDensity(pos.x, pos.y);

        /// <summary>
        /// Check if position has any resource
        /// </summary>
        public bool HasResource(int x, int z)
        {
            return GetResourceType(x, z) != ResourceType.None;
        }

        public bool HasResource(Vector2Int pos) => HasResource(pos.x, pos.y);

        /// <summary>
        /// Get all locations of a specific resource type
        /// </summary>
        public List<Vector2Int> FindAllResources(ResourceType resourceType)
        {
            if (resourceLocations.ContainsKey(resourceType))
            {
                return new List<Vector2Int>(resourceLocations[resourceType]);
            }
            return new List<Vector2Int>();
        }

        /// <summary>
        /// Find nearest resource of given type from a position
        /// </summary>
        public Vector2Int FindNearestResource(Vector2Int fromPos, ResourceType resourceType)
        {
            if (!resourceLocations.ContainsKey(resourceType))
                return new Vector2Int(-1, -1);

            Vector2Int nearest = new Vector2Int(-1, -1);
            float minDistSq = float.MaxValue;

            foreach (var pos in resourceLocations[resourceType])
            {
                float distSq = (pos - fromPos).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = pos;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find all resources within radius of position
        /// </summary>
        public List<Vector2Int> FindResourcesInRadius(Vector2Int center, float radius, ResourceType resourceType = ResourceType.None)
        {
            List<Vector2Int> results = new List<Vector2Int>();
            float radiusSq = radius * radius;

            if (resourceType == ResourceType.None)
            {
                // Search all resource types
                foreach (var kvp in resourceLocations)
                {
                    foreach (var pos in kvp.Value)
                    {
                        if ((pos - center).sqrMagnitude <= radiusSq)
                        {
                            results.Add(pos);
                        }
                    }
                }
            }
            else
            {
                // Search specific type
                if (resourceLocations.ContainsKey(resourceType))
                {
                    foreach (var pos in resourceLocations[resourceType])
                    {
                        if ((pos - center).sqrMagnitude <= radiusSq)
                        {
                            results.Add(pos);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Count resources in area
        /// </summary>
        public int CountResourcesInArea(int minX, int minZ, int maxX, int maxZ, ResourceType resourceType = ResourceType.None)
        {
            int count = 0;

            for (int z = Mathf.Max(0, minZ); z <= Mathf.Min(mapSize - 1, maxZ); z++)
            {
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(mapSize - 1, maxX); x++)
                {
                    var cell = terrainGrid[x, z];
                    if (cell.HasResource && (resourceType == ResourceType.None || cell.resourceType == resourceType))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        #endregion

        #region Building Placement Helpers

        /// <summary>
        /// Check if an area is suitable for building
        /// </summary>
        public bool IsAreaBuildable(Vector2Int center, int radius, float maxSlopeTolerance = 0.3f)
        {
            for (int z = center.y - radius; z <= center.y + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (!IsValidGridPos(x, z)) return false;

                    // Check slope
                    if (GetSlope(x, z) > maxSlopeTolerance)
                        return false;

                    // Could add more checks here (water, etc.)
                }
            }

            return true;
        }

        /// <summary>
        /// Find best building spot near a resource
        /// </summary>
        public Vector2Int FindBuildingSpotNearResource(Vector2Int resourcePos, int searchRadius, int buildingSize)
        {
            Vector2Int bestSpot = new Vector2Int(-1, -1);
            float bestScore = float.MinValue;

            for (int z = resourcePos.y - searchRadius; z <= resourcePos.y + searchRadius; z++)
            {
                for (int x = resourcePos.x - searchRadius; x <= resourcePos.x + searchRadius; x++)
                {
                    Vector2Int testPos = new Vector2Int(x, z);

                    if (!IsAreaBuildable(testPos, buildingSize))
                        continue;

                    // Score based on distance to resource (closer is better)
                    float dist = (testPos - resourcePos).magnitude;
                    float score = 1f / (dist + 1f);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSpot = testPos;
                    }
                }
            }

            return bestSpot;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Get resource statistics for debugging
        /// </summary>
        public Dictionary<ResourceType, int> GetResourceStatistics()
        {
            Dictionary<ResourceType, int> stats = new Dictionary<ResourceType, int>();

            foreach (var kvp in resourceLocations)
            {
                stats[kvp.Key] = kvp.Value.Count;
            }

            return stats;
        }

        private void OnDrawGizmosSelected()
        {
            if (terrainGrid == null || !Application.isPlaying) return;

            // Visualize resources
            foreach (var kvp in resourceLocations)
            {
                Gizmos.color = GetResourceColor(kvp.Key);

                foreach (var gridPos in kvp.Value)
                {
                    Vector3 worldPos = GridToWorld(gridPos);
                    Gizmos.DrawSphere(worldPos + Vector3.up * 0.5f, 0.3f);
                }
            }
        }

        private Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.IronOre: return new Color(0.6f, 0.6f, 0.7f);
                case ResourceType.CopperOre: return new Color(0.8f, 0.5f, 0.3f);
                case ResourceType.GoldOre: return Color.yellow;
                case ResourceType.CoalDeposit: return Color.black;
                case ResourceType.Stone: return Color.gray;
                default: return Color.white;
            }
        }

        #endregion
    }
}