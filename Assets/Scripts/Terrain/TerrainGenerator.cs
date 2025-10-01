using UnityEngine;
using System.Collections.Generic;
using VoxelTerrain.Data;
using VoxelTerrain.Interfaces;
using VoxelTerrain.Generators;
using VoxelTerrain.Utilities;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Mesh Settings")]
    public int mapSize = 255;
    public float scale = 20;
    public float elevationScale = 10;
    public int seed;
    public bool randomizeSeed;

    [Header("Voxel Settings")]
    [Range(0.1f, 2.0f)]
    public float voxelSize = 1.0f;
    public bool generateVoxelSkirt = true;
    public Material voxelMaterial;

    [Header("Voxel Block Types")]
    [Range(0f, 1f)]
    public float sandHeightThreshold = 0.3f;
    [Range(0f, 1f)]
    public float snowHeightThreshold = 0.75f;

    [Header("Water Settings")]
    public bool enableWater = true;
    [Range(0, 1)]
    public float waterLevel = 0.3f;
    public Material waterMaterial;
    public float waterScale = 1.0f;
    public float waterSkirtHeight = 5.0f;

    [Header("Heightmap Generation")]
    public ComputeShader heightMapComputeShader;
    public int numOctaves = 7;
    public float persistence = .5f;
    public float lacunarity = 2;
    public float initialScale = 1.5f;

    [Header("Erosion Settings")]
    public ComputeShader erosion;
    public int numErosionIterations = 50000;
    public int erosionBrushRadius = 3;

    public int maxLifetime = 30;
    public float sedimentCapacityFactor = 3;
    public float minSedimentCapacity = .01f;
    public float depositSpeed = 0.3f;
    public float erodeSpeed = 0.3f;
    public float evaporateSpeed = .01f;
    public float gravity = 4;
    public float startSpeed = 1;
    public float startWater = 1;
    [Range(0, 1)]
    public float inertia = 0.3f;

    // Internal
    float[] map;
    Mesh mesh;
    Mesh waterMesh;
    int mapSizeWithBorder;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshRenderer waterMeshRenderer;
    MeshFilter waterMeshFilter;

    // Voxel mesh generator
    private IMeshGenerator voxelMeshGenerator;

    private void EnsureGeneratorInitialized()
    {
        if (voxelMeshGenerator == null)
            voxelMeshGenerator = new MeshGenerator();
    }

    public void GenerateHeightMap()
    {
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;
        map = GenerateHeightMapGPU(mapSizeWithBorder);
    }

    float[] GenerateHeightMapGPU(int mapSize)
    {
        seed = (randomizeSeed) ? Random.Range(-10000, 10000) : seed;
        var prng = new System.Random(seed);

        Vector2[] offsets = new Vector2[numOctaves];
        for (int i = 0; i < numOctaves; i++)
        {
            offsets[i] = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
        }
        ComputeBuffer offsetsBuffer = new ComputeBuffer(offsets.Length, sizeof(float) * 2);
        offsetsBuffer.SetData(offsets);
        heightMapComputeShader.SetBuffer(0, "offsets", offsetsBuffer);

        int floatToIntMultiplier = 1000;
        float[] map = new float[mapSize * mapSize];

        ComputeBuffer mapBuffer = new ComputeBuffer(map.Length, sizeof(int));
        mapBuffer.SetData(map);
        heightMapComputeShader.SetBuffer(0, "heightMap", mapBuffer);

        int[] minMaxHeight = { floatToIntMultiplier * numOctaves, 0 };
        ComputeBuffer minMaxBuffer = new ComputeBuffer(minMaxHeight.Length, sizeof(int));
        minMaxBuffer.SetData(minMaxHeight);
        heightMapComputeShader.SetBuffer(0, "minMax", minMaxBuffer);

        heightMapComputeShader.SetInt("mapSize", mapSize);
        heightMapComputeShader.SetInt("octaves", numOctaves);
        heightMapComputeShader.SetFloat("lacunarity", lacunarity);
        heightMapComputeShader.SetFloat("persistence", persistence);
        heightMapComputeShader.SetFloat("scaleFactor", initialScale);
        heightMapComputeShader.SetInt("floatToIntMultiplier", floatToIntMultiplier);
        heightMapComputeShader.SetInt("heightMapSize", map.Length);

        ComputeHelper.Dispatch(heightMapComputeShader, map.Length);

        mapBuffer.GetData(map);
        minMaxBuffer.GetData(minMaxHeight);
        mapBuffer.Release();
        minMaxBuffer.Release();
        offsetsBuffer.Release();

        float minValue = (float)minMaxHeight[0] / (float)floatToIntMultiplier;
        float maxValue = (float)minMaxHeight[1] / (float)floatToIntMultiplier;

        for (int i = 0; i < map.Length; i++)
        {
            map[i] = Mathf.InverseLerp(minValue, maxValue, map[i]);
        }

        return map;
    }

    public void Erode()
    {
        ErodeGPU();
    }

    private void ErodeGPU()
    {
        int numThreads = numErosionIterations / 1024;

        ComputeBuffer mapBuffer = new ComputeBuffer(map.Length, sizeof(float));
        mapBuffer.SetData(map);

        System.Collections.Generic.List<int> brushIndexOffsets = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<float> brushWeights = new System.Collections.Generic.List<float>();
        CreateErosionBrush(ref brushIndexOffsets, ref brushWeights);

        ComputeBuffer brushIndexBuffer = new ComputeBuffer(brushIndexOffsets.Count, sizeof(int));
        ComputeBuffer brushWeightBuffer = new ComputeBuffer(brushWeights.Count, sizeof(float));
        brushIndexBuffer.SetData(brushIndexOffsets);
        brushWeightBuffer.SetData(brushWeights);

        int[] randomIndices = new int[numErosionIterations];
        System.Random prng = new System.Random(seed);
        for (int i = 0; i < numErosionIterations; i++)
        {
            randomIndices[i] = prng.Next(erosionBrushRadius, mapSizeWithBorder - erosionBrushRadius) +
                               prng.Next(erosionBrushRadius, mapSizeWithBorder - erosionBrushRadius) * mapSizeWithBorder;
        }

        ComputeBuffer randomIndexBuffer = new ComputeBuffer(randomIndices.Length, sizeof(int));
        randomIndexBuffer.SetData(randomIndices);

        erosion.SetBuffer(0, "map", mapBuffer);
        erosion.SetBuffer(0, "randomIndices", randomIndexBuffer);
        erosion.SetBuffer(0, "brushIndices", brushIndexBuffer);
        erosion.SetBuffer(0, "brushWeights", brushWeightBuffer);

        erosion.SetInt("mapSize", mapSizeWithBorder);
        erosion.SetInt("brushLength", brushIndexOffsets.Count);
        erosion.SetInt("borderSize", erosionBrushRadius);
        erosion.SetInt("maxLifetime", maxLifetime);
        erosion.SetFloat("inertia", inertia);
        erosion.SetFloat("sedimentCapacityFactor", sedimentCapacityFactor);
        erosion.SetFloat("minSedimentCapacity", minSedimentCapacity);
        erosion.SetFloat("depositSpeed", depositSpeed);
        erosion.SetFloat("erodeSpeed", erodeSpeed);
        erosion.SetFloat("evaporateSpeed", evaporateSpeed);
        erosion.SetFloat("gravity", gravity);
        erosion.SetFloat("startSpeed", startSpeed);
        erosion.SetFloat("startWater", startWater);

        erosion.Dispatch(0, numThreads, 1, 1);

        mapBuffer.GetData(map);

        mapBuffer.Release();
        brushIndexBuffer.Release();
        brushWeightBuffer.Release();
        randomIndexBuffer.Release();
    }

    private void CreateErosionBrush(ref System.Collections.Generic.List<int> brushIndexOffsets, ref System.Collections.Generic.List<float> brushWeights)
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

    /// <summary>
    /// Constructs the terrain mesh using the voxel generator
    /// </summary>
    public void ConstructMesh()
    {
        EnsureGeneratorInitialized();

        TerrainMeshData meshData = new TerrainMeshData
        {
            map = map,
            mapSize = mapSize,
            mapSizeWithBorder = mapSizeWithBorder,
            erosionBrushRadius = erosionBrushRadius,
            scale = scale,
            elevationScale = elevationScale,
            skirtHeight = 0,
            voxelSize = voxelSize,
            generateVoxelSkirt = generateVoxelSkirt,
            sandHeightThreshold = sandHeightThreshold,
            snowHeightThreshold = snowHeightThreshold
        };

        mesh = voxelMeshGenerator.GenerateMesh(meshData);

        AssignMeshComponents();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = voxelMaterial;

        if (voxelMaterial != null)
        {
            voxelMaterial.SetFloat("_MaxHeight", elevationScale);
            voxelMaterial.SetFloat("_WaterLevel", waterLevel * elevationScale);
        }

        if (enableWater)
        {
            ConstructWaterMesh();
        }
        else if (waterMeshRenderer != null)
        {
            waterMeshRenderer.enabled = false;
        }
    }

    void ConstructWaterMesh()
    {
        float waterHeight = waterLevel * elevationScale;

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

    void AssignMeshComponents()
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

    void AssignWaterMeshComponents()
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