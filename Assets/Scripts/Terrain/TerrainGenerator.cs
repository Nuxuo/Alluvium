using UnityEditor.SceneManagement;
using UnityEngine;
using VoxelTerrain.Data;
using VoxelTerrain.Generators;
using VoxelTerrain.Interfaces;
using VoxelTerrain.Utilities;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Biome Selection")]
    [SerializeField] private BiomePreset biomePreset;
    [SerializeField] private bool useQuickPreset = true;
    [SerializeField] private QuickPresetType quickPreset = QuickPresetType.Mountains;

    public enum QuickPresetType
    {
        Mountains,
        Desert,
        Plains,
        Highlands
    }

    [Header("Map Settings")]
    public int mapSize = 255;
    public float scale = 20;
    public int seed;
    public bool randomizeSeed;

    [Header("Voxel Settings")]
    [Range(0.1f, 2.0f)]
    public float voxelSize = 1.0f;
    public bool generateVoxelSkirt = true;
    public Material voxelMaterial;

    [Header("Water Settings")]
    public bool enableWater = true;
    [Range(0, 1)]
    public float waterLevel = 0.3f;
    public Material waterMaterial;
    public float waterScale = 1.0f;
    public float waterSkirtHeight = 5.0f;

    [Header("Compute Shaders")]
    public ComputeShader heightMapComputeShader;
    public ComputeShader erosionShader;
    public ComputeShader noiseMapComputeShader;
    public ComputeShader terrainAssignmentShader;
    public ComputeShader resourcePlacementShader;

    // Internal data
    private float[] heightmap;
    private TerrainCell[] terrainData;
    private Mesh mesh;
    private Mesh waterMesh;
    private int mapSizeWithBorder;
    private int erosionBrushRadius;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshRenderer waterMeshRenderer;
    private MeshFilter waterMeshFilter;
    private IMeshGenerator voxelMeshGenerator;
    private TerrainDataManager dataManager;

    private void Awake()
    {
        // Get or add data manager
        dataManager = GetComponent<TerrainDataManager>();
        if (dataManager == null)
        {
            dataManager = gameObject.AddComponent<TerrainDataManager>();
        }
    }

    private void EnsureGeneratorInitialized()
    {
        if (voxelMeshGenerator == null)
            voxelMeshGenerator = new MeshGenerator();
    }

    private void EnsureDataManager()
    {
        if (dataManager == null)
        {
            dataManager = GetComponent<TerrainDataManager>();
            if (dataManager == null)
            {
                dataManager = gameObject.AddComponent<TerrainDataManager>();
            }
        }
    }

    /// <summary>
    /// Load biome preset - either from asset or quick preset
    /// </summary>
    private BiomePreset GetCurrentBiome()
    {
        if (useQuickPreset)
        {
            switch (quickPreset)
            {
                case QuickPresetType.Mountains: return BiomePresets.CreateMountains();
                case QuickPresetType.Desert: return BiomePresets.CreateDesert();
                case QuickPresetType.Plains: return BiomePresets.CreatePlains();
                case QuickPresetType.Highlands: return BiomePresets.CreateHighlands();
            }
        }

        return biomePreset != null ? biomePreset : BiomePresets.CreatePlains();
    }

    public void GenerateCompleteWorld()
    {
        var biome = GetCurrentBiome();

        Debug.Log($"Generating {biome.biomeName} biome...");

        // Step 1: Generate heightmap
        GenerateHeightMap(biome);

        // Step 2: Apply erosion
        Erode(biome);

        // Step 3: Assign terrain types
        AssignTerrainTypes(biome);

        // Step 4: Place resources
        PlaceResources(biome);

        // Step 5: Build mesh
        ConstructMesh();

        // Step 6: Initialize data manager
        InitializeDataManager();

        Debug.Log($"World generation complete! {terrainData.Length} cells generated.");
    }

    private void GenerateHeightMap(BiomePreset biome)
    {
        seed = randomizeSeed ? Random.Range(-10000, 10000) : seed;
        erosionBrushRadius = biome.erosionBrushRadius;
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;

        var prng = new System.Random(seed);
        Vector2[] offsets = new Vector2[biome.numOctaves];
        for (int i = 0; i < biome.numOctaves; i++)
        {
            offsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
        }

        ComputeBuffer offsetsBuffer = new ComputeBuffer(offsets.Length, sizeof(float) * 2);
        offsetsBuffer.SetData(offsets);
        heightMapComputeShader.SetBuffer(0, "offsets", offsetsBuffer);

        int floatToIntMultiplier = 1000;
        heightmap = new float[mapSizeWithBorder * mapSizeWithBorder];

        ComputeBuffer mapBuffer = new ComputeBuffer(heightmap.Length, sizeof(float));
        mapBuffer.SetData(heightmap);
        heightMapComputeShader.SetBuffer(0, "heightMap", mapBuffer);

        int[] minMaxHeight = { floatToIntMultiplier * biome.numOctaves, 0 };
        ComputeBuffer minMaxBuffer = new ComputeBuffer(minMaxHeight.Length, sizeof(int));
        minMaxBuffer.SetData(minMaxHeight);
        heightMapComputeShader.SetBuffer(0, "minMax", minMaxBuffer);

        heightMapComputeShader.SetInt("mapSize", mapSizeWithBorder);
        heightMapComputeShader.SetInt("octaves", biome.numOctaves);
        heightMapComputeShader.SetFloat("lacunarity", biome.lacunarity);
        heightMapComputeShader.SetFloat("persistence", biome.persistence);
        heightMapComputeShader.SetFloat("scaleFactor", biome.initialScale);
        heightMapComputeShader.SetInt("floatToIntMultiplier", floatToIntMultiplier);
        heightMapComputeShader.SetInt("heightMapSize", heightmap.Length);
        heightMapComputeShader.SetInt("seed", seed);

        ComputeHelper.Dispatch(heightMapComputeShader, heightmap.Length);

        mapBuffer.GetData(heightmap);
        minMaxBuffer.GetData(minMaxHeight);

        mapBuffer.Release();
        minMaxBuffer.Release();
        offsetsBuffer.Release();

        float minValue = (float)minMaxHeight[0] / floatToIntMultiplier;
        float maxValue = (float)minMaxHeight[1] / floatToIntMultiplier;

        for (int i = 0; i < heightmap.Length; i++)
        {
            heightmap[i] = Mathf.InverseLerp(minValue, maxValue, heightmap[i]);
        }
    }

    private void Erode(BiomePreset biome)
    {
        int numThreads = biome.erosionIterations / 1024;

        // Generate erosion resistance map
        float[] noiseMap = GenerateNoiseMap(mapSizeWithBorder);
        ComputeBuffer noiseMapBuffer = new ComputeBuffer(noiseMap.Length, sizeof(float));
        noiseMapBuffer.SetData(noiseMap);

        ComputeBuffer mapBuffer = new ComputeBuffer(heightmap.Length, sizeof(float));
        mapBuffer.SetData(heightmap);

        var brushIndexOffsets = new System.Collections.Generic.List<int>();
        var brushWeights = new System.Collections.Generic.List<float>();
        CreateErosionBrush(ref brushIndexOffsets, ref brushWeights);

        ComputeBuffer brushIndexBuffer = new ComputeBuffer(brushIndexOffsets.Count, sizeof(int));
        ComputeBuffer brushWeightBuffer = new ComputeBuffer(brushWeights.Count, sizeof(float));
        brushIndexBuffer.SetData(brushIndexOffsets);
        brushWeightBuffer.SetData(brushWeights);

        int[] randomIndices = new int[biome.erosionIterations];
        System.Random prng = new System.Random(seed);
        for (int i = 0; i < biome.erosionIterations; i++)
        {
            randomIndices[i] = prng.Next(erosionBrushRadius, mapSizeWithBorder - erosionBrushRadius) +
                               prng.Next(erosionBrushRadius, mapSizeWithBorder - erosionBrushRadius) * mapSizeWithBorder;
        }

        ComputeBuffer randomIndexBuffer = new ComputeBuffer(randomIndices.Length, sizeof(int));
        randomIndexBuffer.SetData(randomIndices);

        erosionShader.SetBuffer(0, "noiseMap", noiseMapBuffer);
        erosionShader.SetBuffer(0, "map", mapBuffer);
        erosionShader.SetBuffer(0, "randomIndices", randomIndexBuffer);
        erosionShader.SetBuffer(0, "brushIndices", brushIndexBuffer);
        erosionShader.SetBuffer(0, "brushWeights", brushWeightBuffer);

        erosionShader.SetInt("mapSize", mapSizeWithBorder);
        erosionShader.SetInt("brushLength", brushIndexOffsets.Count);
        erosionShader.SetInt("borderSize", erosionBrushRadius);
        erosionShader.SetInt("maxLifetime", 50);
        erosionShader.SetFloat("inertia", 0.025f);
        erosionShader.SetFloat("sedimentCapacityFactor", 8f);
        erosionShader.SetFloat("minSedimentCapacity", 0.03f);
        erosionShader.SetFloat("depositSpeed", 0.6f);
        //erosionShader.SetFloat("erodeSpeed", 0.3f * biome.erosionStrength / 4f); // Normalize to biome strength
        erosionShader.SetFloat("erodeSpeed", 0.3f); // Normalize to biome strength
        erosionShader.SetFloat("evaporateSpeed", 0.05f);
        erosionShader.SetFloat("gravity", 4f);
        erosionShader.SetFloat("startSpeed", 1f);
        erosionShader.SetFloat("startWater", 1f);

        erosionShader.Dispatch(0, numThreads, 1, 1);

        mapBuffer.GetData(heightmap);

        mapBuffer.Release();
        brushIndexBuffer.Release();
        brushWeightBuffer.Release();
        randomIndexBuffer.Release();
        noiseMapBuffer.Release();
    }

    private void AssignTerrainTypes(BiomePreset biome)
    {
        // Initialize terrain data array
        terrainData = new TerrainCell[mapSize * mapSize];

        // Convert biome rules to compute shader format
        // This would need a new compute shader similar to BlockTypeAssignment
        // For now, assign on CPU as placeholder

        float cellSize = (scale * 2f) / (mapSize - 1);

        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int borderedIndex = (z + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;
                float normalizedHeight = heightmap[borderedIndex];
                float slope = CalculateSlope(x, z);

                // Assign terrain type based on rules
                TerrainType terrain = DetermineTerrainType(normalizedHeight, slope, biome.terrainRules);

                int index = z * mapSize + x;
                terrainData[index] = new TerrainCell(terrain, ResourceType.None, 0);
            }
        }
    }

    private TerrainType DetermineTerrainType(float height, float slope, System.Collections.Generic.List<TerrainRule> rules)
    {
        TerrainType bestType = TerrainType.Grass;
        float bestScore = -1f;
        int bestPriority = -1;

        foreach (var rule in rules)
        {
            float score = 0f;

            // Height check
            if (height >= rule.minHeight && height <= rule.maxHeight)
            {
                float heightCenter = (rule.minHeight + rule.maxHeight) * 0.5f;
                float distFromCenter = Mathf.Abs(height - heightCenter) / ((rule.maxHeight - rule.minHeight) * 0.5f);
                float heightScore = 1f - distFromCenter;

                // Slope check
                if (slope >= rule.minSlope && slope <= rule.maxSlope)
                {
                    float slopeCenter = (rule.minSlope + rule.maxSlope) * 0.5f;
                    float slopeDistFromCenter = Mathf.Abs(slope - slopeCenter) / ((rule.maxSlope - rule.minSlope) * 0.5f);
                    float slopeScore = 1f - slopeDistFromCenter;

                    score = heightScore * slopeScore * rule.strength;

                    // Check if this is better
                    if (rule.priority > bestPriority || (rule.priority == bestPriority && score > bestScore))
                    {
                        if (score > 0.1f)
                        {
                            bestType = rule.terrainType;
                            bestScore = score;
                            bestPriority = rule.priority;
                        }
                    }
                }
            }
        }

        return bestType;
    }

    private void PlaceResources(BiomePreset biome)
    {
        System.Random prng = new System.Random(seed + 12345);

        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int index = z * mapSize + x;
                TerrainCell cell = terrainData[index];

                float normalizedHeight = heightmap[(z + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius];
                float slope = CalculateSlope(x, z);

                foreach (var rule in biome.resourceRules)
                {
                    // Check if terrain type is valid
                    if (!rule.validTerrainTypes.Contains(cell.terrainType))
                        continue;

                    // Check height range
                    if (normalizedHeight < rule.minHeight || normalizedHeight > rule.maxHeight)
                        continue;

                    // Check slope range
                    if (slope < rule.minSlope || slope > rule.maxSlope)
                        continue;

                    // Probability check
                    float roll = (float)prng.NextDouble();
                    if (roll > rule.spawnProbability)
                        continue;

                    // Clustering check if enabled
                    if (rule.useClustering)
                    {
                        float clusterNoise = Mathf.PerlinNoise(x * rule.clusterScale / mapSize + seed,
                                                                z * rule.clusterScale / mapSize + seed);
                        if (clusterNoise < rule.clusterThreshold)
                            continue;
                    }

                    // Place resource
                    byte density = (byte)prng.Next(rule.minDensity, rule.maxDensity + 1);
                    terrainData[index] = new TerrainCell(cell.terrainType, rule.resourceType, density);
                    break; // Only one resource per cell
                }
            }
        }
    }

    private float CalculateSlope(int x, int z)
    {
        float cellSize = (scale * 2f) / (mapSize - 1);

        int borderedX = x + erosionBrushRadius;
        int borderedZ = z + erosionBrushRadius;

        float heightC = heightmap[borderedZ * mapSizeWithBorder + borderedX];
        float heightL = heightmap[borderedZ * mapSizeWithBorder + Mathf.Max(0, borderedX - 1)];
        float heightR = heightmap[borderedZ * mapSizeWithBorder + Mathf.Min(mapSizeWithBorder - 1, borderedX + 1)];
        float heightD = heightmap[Mathf.Max(0, borderedZ - 1) * mapSizeWithBorder + borderedX];
        float heightU = heightmap[Mathf.Min(mapSizeWithBorder - 1, borderedZ + 1) * mapSizeWithBorder + borderedX];

        float dx = (heightR - heightL) / (2f * cellSize);
        float dz = (heightU - heightD) / (2f * cellSize);

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void ConstructMesh()
    {
        EnsureGeneratorInitialized();

        var biome = GetCurrentBiome();

        TerrainMeshData meshData = new TerrainMeshData
        {
            map = heightmap,
            mapSize = mapSize,
            mapSizeWithBorder = mapSizeWithBorder,
            erosionBrushRadius = erosionBrushRadius,
            scale = scale,
            elevationScale = biome.elevationScale,
            skirtHeight = 0,
            voxelSize = voxelSize,
            generateVoxelSkirt = generateVoxelSkirt,
            blockData = new VoxelTerrain.Storage.BlockData(mapSize, mapSize)
        };

        // Convert terrain data to block data for rendering
        for (int i = 0; i < terrainData.Length; i++)
        {
            meshData.blockData.Data[i] = (uint)terrainData[i].terrainType;
        }

        mesh = voxelMeshGenerator.GenerateMesh(meshData);

        AssignMeshComponents();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = voxelMaterial;

        if (voxelMaterial != null)
        {
            voxelMaterial.SetFloat("_MaxHeight", biome.elevationScale);
            voxelMaterial.SetFloat("_WaterLevel", waterLevel * biome.elevationScale);
        }

        if (enableWater)
        {
            ConstructWaterMesh(biome);
        }
        else if (waterMeshRenderer != null)
        {
            waterMeshRenderer.enabled = false;
        }
    }

    private void InitializeDataManager()
    {
        EnsureDataManager();

        var biome = GetCurrentBiome();

        // Extract height data without border
        float[] cleanHeightmap = new float[mapSize * mapSize];
        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int cleanIndex = z * mapSize + x;
                int borderedIndex = (z + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;
                cleanHeightmap[cleanIndex] = heightmap[borderedIndex];
            }
        }

        dataManager.Initialize(mapSize, scale, biome.elevationScale, cleanHeightmap, terrainData);
    }

    private float[] GenerateNoiseMap(int size)
    {
        float[] noiseMap = new float[size * size];
        ComputeBuffer noiseMapBuffer = new ComputeBuffer(noiseMap.Length, sizeof(float));
        noiseMapBuffer.SetData(noiseMap);
        noiseMapComputeShader.SetBuffer(0, "noiseMap", noiseMapBuffer);

        noiseMapComputeShader.SetInt("mapSize", size);
        noiseMapComputeShader.SetInt("noiseMapSize", noiseMap.Length);
        noiseMapComputeShader.SetInt("seed", seed);
        noiseMapComputeShader.SetFloat("scale", 2.5f);
        noiseMapComputeShader.SetInt("octaves", 4);
        noiseMapComputeShader.SetFloat("persistence", 0.5f);
        noiseMapComputeShader.SetFloat("lacunarity", 2.0f);

        ComputeHelper.Dispatch(noiseMapComputeShader, noiseMap.Length);

        noiseMapBuffer.GetData(noiseMap);
        noiseMapBuffer.Release();

        return noiseMap;
    }

    private void CreateErosionBrush(ref System.Collections.Generic.List<int> brushIndexOffsets,
                                    ref System.Collections.Generic.List<float> brushWeights)
    {
        brushIndexOffsets.Clear();
        brushWeights.Clear();

        float weightSum = 0;
        for (int brushY = -erosionBrushRadius; brushY <= erosionBrushRadius; brushY++)
        {
            for (int brushX = -erosionBrushRadius; brushX <= erosionBrushRadius; brushX++)
            {
                float sqrDst = brushX * brushX + brushY * brushY;
                if (sqrDst < erosionBrushRadius * erosionBrushRadius)
                {
                    brushIndexOffsets.Add(brushY * mapSizeWithBorder + brushX);
                    float brushWeight = 1 - Mathf.Sqrt(sqrDst) / erosionBrushRadius;
                    weightSum += brushWeight;
                    brushWeights.Add(brushWeight);
                }
            }
        }

        for (int i = 0; i < brushWeights.Count; i++)
        {
            brushWeights[i] /= weightSum;
        }
    }

    private void ConstructWaterMesh(BiomePreset biome)
    {
        float waterHeight = waterLevel * biome.elevationScale;

        Vector3[] waterVerts = new Vector3[8];
        int[] waterTriangles = new int[30];
        Vector2[] waterUVs = new Vector2[8];

        float waterSize = scale * waterScale;
        waterVerts[0] = new Vector3(-waterSize, waterHeight, -waterSize);
        waterVerts[1] = new Vector3(-waterSize, waterHeight, waterSize);
        waterVerts[2] = new Vector3(waterSize, waterHeight, waterSize);
        waterVerts[3] = new Vector3(waterSize, waterHeight, -waterSize);

        waterVerts[4] = new Vector3(-waterSize, waterHeight - waterSkirtHeight, -waterSize);
        waterVerts[5] = new Vector3(-waterSize, waterHeight - waterSkirtHeight, waterSize);
        waterVerts[6] = new Vector3(waterSize, waterHeight - waterSkirtHeight, waterSize);
        waterVerts[7] = new Vector3(waterSize, waterHeight - waterSkirtHeight, -waterSize);

        for (int i = 0; i < 4; i++)
        {
            waterUVs[i] = new Vector2((waterVerts[i].x + waterSize) / (2f * waterSize),
                                      (waterVerts[i].z + waterSize) / (2f * waterSize));
            waterUVs[i + 4] = waterUVs[i];
        }

        waterTriangles[0] = 0; waterTriangles[1] = 1; waterTriangles[2] = 2;
        waterTriangles[3] = 0; waterTriangles[4] = 2; waterTriangles[5] = 3;

        int triIndex = 6;
        waterTriangles[triIndex++] = 0; waterTriangles[triIndex++] = 4; waterTriangles[triIndex++] = 5;
        waterTriangles[triIndex++] = 0; waterTriangles[triIndex++] = 5; waterTriangles[triIndex++] = 1;
        waterTriangles[triIndex++] = 3; waterTriangles[triIndex++] = 2; waterTriangles[triIndex++] = 6;
        waterTriangles[triIndex++] = 3; waterTriangles[triIndex++] = 6; waterTriangles[triIndex++] = 7;
        waterTriangles[triIndex++] = 0; waterTriangles[triIndex++] = 3; waterTriangles[triIndex++] = 7;
        waterTriangles[triIndex++] = 0; waterTriangles[triIndex++] = 7; waterTriangles[triIndex++] = 4;
        waterTriangles[triIndex++] = 1; waterTriangles[triIndex++] = 5; waterTriangles[triIndex++] = 6;
        waterTriangles[triIndex++] = 1; waterTriangles[triIndex++] = 6; waterTriangles[triIndex++] = 2;

        if (waterMesh == null)
        {
            waterMesh = new Mesh();
            waterMesh.name = "Water Mesh";
        }
        else
        {
            waterMesh.Clear();
        }

        waterMesh.vertices = waterVerts;
        waterMesh.triangles = waterTriangles;
        waterMesh.uv = waterUVs;
        waterMesh.RecalculateNormals();
        waterMesh.RecalculateBounds();

        AssignWaterMeshComponents();
        waterMeshFilter.sharedMesh = waterMesh;
        waterMeshRenderer.sharedMaterial = waterMaterial;
        waterMeshRenderer.enabled = true;

        if (waterMaterial != null)
        {
            waterMaterial.SetFloat("_WaterLevel", waterHeight);
        }
    }

    private void AssignMeshComponents()
    {
        string meshHolderName = "Mesh Holder";
        Transform meshHolder = transform.Find(meshHolderName);
        if (meshHolder == null)
        {
            meshHolder = new GameObject(meshHolderName).transform;
            meshHolder.transform.parent = transform;
            meshHolder.transform.localPosition = Vector3.zero;
            meshHolder.transform.localRotation = Quaternion.identity;
        }

        if (!meshHolder.gameObject.GetComponent<MeshFilter>())
        {
            meshHolder.gameObject.AddComponent<MeshFilter>();
        }
        if (!meshHolder.GetComponent<MeshRenderer>())
        {
            meshHolder.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
    }

    private void AssignWaterMeshComponents()
    {
        string waterHolderName = "Water Holder";
        Transform waterHolder = transform.Find(waterHolderName);
        if (waterHolder == null)
        {
            waterHolder = new GameObject(waterHolderName).transform;
            waterHolder.transform.parent = transform;
            waterHolder.transform.localPosition = Vector3.zero;
            waterHolder.transform.localRotation = Quaternion.identity;
        }

        if (!waterHolder.gameObject.GetComponent<MeshFilter>())
        {
            waterHolder.gameObject.AddComponent<MeshFilter>();
        }
        if (!waterHolder.GetComponent<MeshRenderer>())
        {
            waterHolder.gameObject.AddComponent<MeshRenderer>();
        }

        waterMeshRenderer = waterHolder.GetComponent<MeshRenderer>();
        waterMeshFilter = waterHolder.GetComponent<MeshFilter>();
    }
}