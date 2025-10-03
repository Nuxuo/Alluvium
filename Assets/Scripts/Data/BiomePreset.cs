using UnityEngine;
using System.Collections.Generic;

namespace VoxelTerrain.Data
{
    /// <summary>
    /// Configurable preset for different terrain types
    /// Create via: Assets > Create > Voxel Terrain > Biome Preset
    /// </summary>
    [CreateAssetMenu(fileName = "New Biome Preset", menuName = "Voxel Terrain/Biome Preset")]
    public class BiomePreset : ScriptableObject
    {
        [Header("Biome Identity")]
        public string biomeName = "New Biome";
        [TextArea(2, 4)]
        public string description = "Description of this biome";

        [Header("Heightmap Generation")]
        public int numOctaves = 7;
        [Range(0.1f, 1f)]
        public float persistence = 0.5f;
        [Range(1f, 4f)]
        public float lacunarity = 2f;
        [Range(0.5f, 5f)]
        public float initialScale = 1.5f;
        [Range(1f, 50f)]
        public float elevationScale = 10f;

        [Header("Erosion")]
        public int erosionIterations = 50000;
        [Range(1, 6)]
        public int erosionBrushRadius = 3;
        [Range(0f, 8f)]
        public float erosionStrength = 4f;

        [Header("Terrain Rules")]
        public List<TerrainRule> terrainRules = new List<TerrainRule>();

        [Header("Resource Placement")]
        public List<ResourcePlacementRule> resourceRules = new List<ResourcePlacementRule>();

        /// <summary>
        /// Setup default terrain and resource rules for this biome
        /// </summary>
        public void SetupDefaultRules()
        {
            terrainRules.Clear();
            resourceRules.Clear();

            // Will be overridden by specific biome types
        }
    }

    /// <summary>
    /// Rule for placing terrain types based on height and slope
    /// </summary>
    [System.Serializable]
    public class TerrainRule
    {
        public string ruleName = "New Rule";
        public TerrainType terrainType;
        public Color previewColor = Color.white;

        [Header("Height Conditions")]
        [Range(0f, 1f)]
        public float minHeight = 0f;
        [Range(0f, 1f)]
        public float maxHeight = 1f;
        [Range(0.01f, 0.3f)]
        public float heightBlend = 0.1f;

        [Header("Slope Conditions")]
        [Range(0f, 2f)]
        public float minSlope = 0f;
        [Range(0f, 2f)]
        public float maxSlope = 2f;
        [Range(0.01f, 0.3f)]
        public float slopeBlend = 0.1f;

        [Header("Priority")]
        [Range(0, 10)]
        public int priority = 5;
        [Range(0f, 1f)]
        public float strength = 1f;

        [Header("Noise Variation")]
        public bool useNoise = true;
        [Range(0f, 1f)]
        public float noiseInfluence = 0.3f;
    }

    /// <summary>
    /// Rule for placing resources based on terrain conditions
    /// </summary>
    [System.Serializable]
    public class ResourcePlacementRule
    {
        public string ruleName = "Resource";
        public ResourceType resourceType;

        [Header("Placement Conditions")]
        public List<TerrainType> validTerrainTypes = new List<TerrainType>();

        [Range(0f, 1f)]
        public float minHeight = 0f;
        [Range(0f, 1f)]
        public float maxHeight = 1f;

        [Range(0f, 2f)]
        public float minSlope = 0f;
        [Range(0f, 2f)]
        public float maxSlope = 2f;

        [Header("Density")]
        [Range(0f, 1f)]
        [Tooltip("Probability of resource appearing (0-1)")]
        public float spawnProbability = 0.1f;

        [Range(0, 255)]
        [Tooltip("Minimum resource richness")]
        public byte minDensity = 50;
        [Range(0, 255)]
        [Tooltip("Maximum resource richness")]
        public byte maxDensity = 200;

        [Header("Clustering")]
        [Tooltip("Resources tend to cluster together")]
        public bool useClustering = true;
        [Range(0.5f, 10f)]
        public float clusterScale = 3f;
        [Range(0f, 1f)]
        public float clusterThreshold = 0.6f;
    }
}