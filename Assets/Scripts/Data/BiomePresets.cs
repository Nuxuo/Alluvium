using UnityEngine;
using System.Collections.Generic;

namespace VoxelTerrain.Data
{
    /// <summary>
    /// Factory for creating pre-configured biome presets
    /// </summary>
    public static class BiomePresets
    {
        // Replace the CreateMountains() method in BiomePresets.cs with this:

        public static BiomePreset CreateMountains()
        {
            var preset = ScriptableObject.CreateInstance<BiomePreset>();
            preset.biomeName = "Mountains";
            preset.description = "High elevation terrain with rocky peaks, snow caps, and valuable ore deposits.";

            // Dramatic heightmap
            preset.numOctaves = 7;
            preset.persistence = 0.5f;
            preset.lacunarity = 2f;
            preset.initialScale = 1.5f;
            preset.elevationScale = 10f;

            // Heavy erosion for realistic mountains
            preset.erosionIterations = 1000000;
            preset.erosionBrushRadius = 5;
            preset.erosionStrength = 6f;

            // === ZONE-BASED TERRAIN RULES ===
            // Each zone has a fallback, then specific overrides

            preset.terrainRules = new List<TerrainRule>
    {
        // ========================================
        // UPPER ZONE FALLBACK (0.55-1.0) → ROCK
        // ========================================
        new TerrainRule
        {
            ruleName = "Upper Zone Base",
            terrainType = TerrainType.Rock,
            previewColor = new Color(0.42f, 0.4f, 0.4f),
            minHeight = 0.55f,
            maxHeight = 1f,
            heightBlend = 0.15f,  // Smooth blend down
            minSlope = 0f,
            maxSlope = 2f,
            priority = 1,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.2f
        },
        
        // ========================================
        // MID ZONE FALLBACK (0.15-0.65) → GRASS
        // ========================================
        new TerrainRule
        {
            ruleName = "Mid Zone Base",
            terrainType = TerrainType.Grass,
            previewColor = new Color(0.4f, 0.6f, 0.35f),
            minHeight = 0.15f,
            maxHeight = 0.65f,
            heightBlend = 0.15f,  // Smooth blend both ways
            minSlope = 0f,
            maxSlope = 2f,
            priority = 1,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.25f
        },
        
        // ========================================
        // LOWER ZONE FALLBACK (0.0-0.25) → DIRT/SAND
        // ========================================
        new TerrainRule
        {
            ruleName = "Lower Zone Base",
            terrainType = TerrainType.Dirt,
            previewColor = new Color(0.5f, 0.42f, 0.32f),
            minHeight = 0f,
            maxHeight = 0.25f,
            heightBlend = 0.15f,  // Smooth blend up
            minSlope = 0f,
            maxSlope = 2f,
            priority = 1,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.3f
        },
        
        // ========================================
        // UPPER ZONE OVERRIDES
        // ========================================
        
        // Snow on flat high areas
        new TerrainRule
        {
            ruleName = "Snow Caps",
            terrainType = TerrainType.Snow,
            previewColor = new Color(0.95f, 0.95f, 1f),
            minHeight = 0.7f,
            maxHeight = 1f,
            heightBlend = 0.1f,
            minSlope = 0f,
            maxSlope = 0.4f,  // Gentle only
            priority = 10,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.15f
        },
        
        // Gravel on medium slopes in upper zone
        new TerrainRule
        {
            ruleName = "High Scree",
            terrainType = TerrainType.Gravel,
            previewColor = new Color(0.52f, 0.5f, 0.53f),
            minHeight = 0.6f,
            maxHeight = 0.85f,
            heightBlend = 0.1f,
            minSlope = 0.35f,
            maxSlope = 0.75f,
            priority = 7,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.3f
        },
        
        // ========================================
        // MID ZONE OVERRIDES
        // ========================================
        
        // Gravel on medium slopes in mid zone
        new TerrainRule
        {
            ruleName = "Mid Scree",
            terrainType = TerrainType.Gravel,
            previewColor = new Color(0.5f, 0.48f, 0.5f),
            minHeight = 0.3f,
            maxHeight = 0.65f,
            heightBlend = 0.1f,
            minSlope = 0.4f,
            maxSlope = 0.8f,
            priority = 6,
            strength = 1f,
            useNoise = true,
            noiseInfluence = 0.3f
        },
        
        // Sparse vegetation in mid-high transition
        new TerrainRule
        {
            ruleName = "Sparse Tundra",
            terrainType = TerrainType.Tundra,
            previewColor = new Color(0.45f, 0.52f, 0.45f),
            minHeight = 0.5f,
            maxHeight = 0.7f,
            heightBlend = 0.12f,
            minSlope = 0f,
            maxSlope = 0.3f,
            priority = 5,
            strength = 0.8f,
            useNoise = true,
            noiseInfluence = 0.35f
        },
        
        // ========================================
        // LOWER ZONE OVERRIDES
        // ========================================
        
        // Sand near water
        new TerrainRule
        {
            ruleName = "Riverbank Sand",
            terrainType = TerrainType.Sand,
            previewColor = new Color(0.85f, 0.75f, 0.55f),
            minHeight = 0f,
            maxHeight = 0.15f,
            heightBlend = 0.08f,
            minSlope = 0f,
            maxSlope = 0.3f,
            priority = 4,
            strength = 0.7f,
            useNoise = true,
            noiseInfluence = 0.4f
        },
        
        // ========================================
        // UNIVERSAL OVERRIDES (work in any zone)
        // ========================================
        
        // Very steep = always rock
        new TerrainRule
        {
            ruleName = "Cliff Faces",
            terrainType = TerrainType.Rock,
            previewColor = new Color(0.38f, 0.36f, 0.36f),
            minHeight = 0f,
            maxHeight = 1f,
            heightBlend = 0.05f,
            minSlope = 0.8f,  // Very steep
            maxSlope = 2f,
            priority = 9,
            strength = 1f,
            useNoise = false,
            noiseInfluence = 0f
        }
    };

            // Resource rules unchanged
            preset.resourceRules = new List<ResourcePlacementRule>
    {
        new ResourcePlacementRule
        {
            ruleName = "Iron Deposits",
            resourceType = ResourceType.IronOre,
            validTerrainTypes = new List<TerrainType> { TerrainType.Rock, TerrainType.Gravel },
            minHeight = 0.4f,
            maxHeight = 0.9f,
            minSlope = 0.3f,
            maxSlope = 2f,
            spawnProbability = 0.15f,
            minDensity = 80,
            maxDensity = 200,
            useClustering = true,
            clusterScale = 4f,
            clusterThreshold = 0.65f
        },
        new ResourcePlacementRule
        {
            ruleName = "Gold Veins",
            resourceType = ResourceType.GoldOre,
            validTerrainTypes = new List<TerrainType> { TerrainType.Rock },
            minHeight = 0.7f,
            maxHeight = 1f,
            minSlope = 0f,
            maxSlope = 2f,
            spawnProbability = 0.05f,
            minDensity = 50,
            maxDensity = 150,
            useClustering = true,
            clusterScale = 5f,
            clusterThreshold = 0.7f
        },
        new ResourcePlacementRule
        {
            ruleName = "Stone",
            resourceType = ResourceType.Stone,
            validTerrainTypes = new List<TerrainType> { TerrainType.Rock, TerrainType.Gravel },
            minHeight = 0.2f,
            maxHeight = 1f,
            minSlope = 0f,
            maxSlope = 2f,
            spawnProbability = 0.3f,
            minDensity = 100,
            maxDensity = 255,
            useClustering = false
        }
    };

            return preset;
        }

        public static BiomePreset CreateDesert()
        {
            var preset = ScriptableObject.CreateInstance<BiomePreset>();
            preset.biomeName = "Desert";
            preset.description = "Flat sandy terrain with occasional rock formations and sand deposits.";

            // Flatter heightmap
            preset.numOctaves = 5;
            preset.persistence = 0.4f;
            preset.lacunarity = 1.8f;
            preset.initialScale = 2f;
            preset.elevationScale = 8f;

            // Minimal erosion
            preset.erosionIterations = 20000;
            preset.erosionBrushRadius = 3;
            preset.erosionStrength = 2f;

            preset.terrainRules = new List<TerrainRule>
            {
                // Rocky outcrops
                new TerrainRule
                {
                    ruleName = "Rock Formations",
                    terrainType = TerrainType.Rock,
                    previewColor = new Color(0.5f, 0.45f, 0.4f),
                    minHeight = 0.6f,
                    maxHeight = 1f,
                    heightBlend = 0.15f,
                    minSlope = 0.5f,
                    maxSlope = 2f,
                    priority = 7,
                    strength = 1f
                },
                // Sand dunes
                new TerrainRule
                {
                    ruleName = "Sand Dunes",
                    terrainType = TerrainType.Sand,
                    previewColor = new Color(0.9f, 0.8f, 0.6f),
                    minHeight = 0f,
                    maxHeight = 1f,
                    heightBlend = 0.1f,
                    minSlope = 0f,
                    maxSlope = 0.8f,
                    priority = 4,
                    strength = 1f
                }
            };

            preset.resourceRules = new List<ResourcePlacementRule>
            {
                // Sand resource
                new ResourcePlacementRule
                {
                    ruleName = "Sand Deposits",
                    resourceType = ResourceType.Sand,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Sand },
                    minHeight = 0f,
                    maxHeight = 0.7f,
                    spawnProbability = 0.4f,
                    minDensity = 150,
                    maxDensity = 255,
                    useClustering = false
                },
                // Some copper in rocks
                new ResourcePlacementRule
                {
                    ruleName = "Copper Deposits",
                    resourceType = ResourceType.CopperOre,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Rock },
                    minHeight = 0.5f,
                    maxHeight = 1f,
                    spawnProbability = 0.1f,
                    minDensity = 60,
                    maxDensity = 180,
                    useClustering = true,
                    clusterScale = 3.5f,
                    clusterThreshold = 0.6f
                }
            };

            return preset;
        }

        public static BiomePreset CreatePlains()
        {
            var preset = ScriptableObject.CreateInstance<BiomePreset>();
            preset.biomeName = "Plains";
            preset.description = "Gentle rolling hills perfect for building and agriculture.";

            // Gentle heightmap
            preset.numOctaves = 6;
            preset.persistence = 0.45f;
            preset.lacunarity = 2f;
            preset.initialScale = 2.5f;
            preset.elevationScale = 6f;

            preset.erosionIterations = 30000;
            preset.erosionBrushRadius = 3;
            preset.erosionStrength = 3f;

            preset.terrainRules = new List<TerrainRule>
            {
                // Hills
                new TerrainRule
                {
                    ruleName = "Hilltops",
                    terrainType = TerrainType.Grass,
                    previewColor = new Color(0.4f, 0.6f, 0.3f),
                    minHeight = 0.4f,
                    maxHeight = 1f,
                    heightBlend = 0.12f,
                    minSlope = 0f,
                    maxSlope = 0.8f,
                    priority = 6,
                    strength = 1f
                },
                // Dirt patches
                new TerrainRule
                {
                    ruleName = "Dirt Patches",
                    terrainType = TerrainType.Dirt,
                    previewColor = new Color(0.5f, 0.4f, 0.3f),
                    minHeight = 0f,
                    maxHeight = 0.5f,
                    heightBlend = 0.1f,
                    minSlope = 0f,
                    maxSlope = 0.4f,
                    priority = 5,
                    strength = 0.6f,
                    noiseInfluence = 0.5f
                }
            };

            preset.resourceRules = new List<ResourcePlacementRule>
            {
                // Coal deposits
                new ResourcePlacementRule
                {
                    ruleName = "Coal Deposits",
                    resourceType = ResourceType.CoalDeposit,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Grass, TerrainType.Dirt },
                    minHeight = 0.2f,
                    maxHeight = 0.7f,
                    spawnProbability = 0.12f,
                    minDensity = 70,
                    maxDensity = 190,
                    useClustering = true,
                    clusterScale = 4f,
                    clusterThreshold = 0.65f
                },
                // Clay deposits
                new ResourcePlacementRule
                {
                    ruleName = "Clay",
                    resourceType = ResourceType.Clay,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Dirt },
                    minHeight = 0f,
                    maxHeight = 0.4f,
                    spawnProbability = 0.2f,
                    minDensity = 100,
                    maxDensity = 220,
                    useClustering = true,
                    clusterScale = 3f,
                    clusterThreshold = 0.5f
                }
            };

            return preset;
        }

        public static BiomePreset CreateHighlands()
        {
            var preset = ScriptableObject.CreateInstance<BiomePreset>();
            preset.biomeName = "Highlands";
            preset.description = "Elevated plateaus with steep cliffs and rich mineral deposits.";

            preset.numOctaves = 7;
            preset.persistence = 0.55f;
            preset.lacunarity = 2.1f;
            preset.initialScale = 1.5f;
            preset.elevationScale = 18f;

            preset.erosionIterations = 60000;
            preset.erosionBrushRadius = 4;
            preset.erosionStrength = 5f;

            preset.terrainRules = new List<TerrainRule>
            {
                // Cliff faces
                new TerrainRule
                {
                    ruleName = "Cliffs",
                    terrainType = TerrainType.Rock,
                    previewColor = new Color(0.45f, 0.4f, 0.4f),
                    minHeight = 0.3f,
                    maxHeight = 1f,
                    heightBlend = 0.1f,
                    minSlope = 0.8f,
                    maxSlope = 2f,
                    priority = 8,
                    strength = 1f
                },
                // Plateau grass
                new TerrainRule
                {
                    ruleName = "Highland Grass",
                    terrainType = TerrainType.Tundra,
                    previewColor = new Color(0.4f, 0.5f, 0.4f),
                    minHeight = 0.5f,
                    maxHeight = 1f,
                    heightBlend = 0.15f,
                    minSlope = 0f,
                    maxSlope = 0.5f,
                    priority = 6,
                    strength = 1f
                },
                // Lowland grass
                new TerrainRule
                {
                    ruleName = "Valley Grass",
                    terrainType = TerrainType.Grass,
                    previewColor = new Color(0.3f, 0.6f, 0.3f),
                    minHeight = 0f,
                    maxHeight = 0.6f,
                    heightBlend = 0.12f,
                    minSlope = 0f,
                    maxSlope = 0.7f,
                    priority = 5,
                    strength = 1f
                }
            };

            preset.resourceRules = new List<ResourcePlacementRule>
            {
                // Iron in cliffs
                new ResourcePlacementRule
                {
                    ruleName = "Iron Veins",
                    resourceType = ResourceType.IronOre,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Rock },
                    minHeight = 0.4f,
                    maxHeight = 1f,
                    minSlope = 0.5f,
                    maxSlope = 2f,
                    spawnProbability = 0.18f,
                    minDensity = 90,
                    maxDensity = 210,
                    useClustering = true,
                    clusterScale = 4.5f,
                    clusterThreshold = 0.7f
                },
                // Copper more common
                new ResourcePlacementRule
                {
                    ruleName = "Copper Deposits",
                    resourceType = ResourceType.CopperOre,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Rock, TerrainType.Tundra },
                    minHeight = 0.3f,
                    maxHeight = 0.9f,
                    spawnProbability = 0.12f,
                    minDensity = 70,
                    maxDensity = 180,
                    useClustering = true,
                    clusterScale = 3.5f,
                    clusterThreshold = 0.65f
                },
                // Granite
                new ResourcePlacementRule
                {
                    ruleName = "Granite",
                    resourceType = ResourceType.Granite,
                    validTerrainTypes = new List<TerrainType> { TerrainType.Rock },
                    minHeight = 0.5f,
                    maxHeight = 1f,
                    spawnProbability = 0.25f,
                    minDensity = 120,
                    maxDensity = 255,
                    useClustering = false
                }
            };

            return preset;
        }
    }
}