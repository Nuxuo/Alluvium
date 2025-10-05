using UnityEngine;
using System.Collections.Generic;

public class SimpleHeightmapGenerator : MonoBehaviour
{
    [Header("Heightmap Settings")]
    public int mapSize = 100;
    public int seed = 1337;

    [Header("Noise Settings")]
    public int octaves = 4;
    [Range(0.1f, 1f)] public float persistence = 0.5f;
    [Range(1f, 4f)] public float lacunarity = 2f;
    [Range(0.5f, 5f)] public float scale = 1.5f;

    [Header("Visualization")]
    public float heightScale = 10f;
    public float terrainScale = 20f;
    public Material voxelMaterial;

    [Header("Compute")]
    public ComputeShader heightMapShader;

    [Header("Layer Settings")]
    [Tooltip("Number of 0.1 unit dirt layers to add on top of rock")]
    public int dirtLayers = 10;
    [Tooltip("Number of 0.1 unit sand layers to add on top of dirt")]
    public int sandLayers = 10;

    private float[] heightMap;
    private VoxelColumn[,] voxelColumns;
    private GameObject meshObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Constants
    private const float LAYER_HEIGHT = 0.1f; // Each layer is 0.1 units tall
    private const float VOXEL_SIZE = 1.0f;   // Each voxel is 1x1 in XZ

    void Start()
    {
        if (heightMapShader == null)
        {
            Debug.LogError("Assign the HeightMap compute shader!");
            return;
        }

        GenerateAndVisualize();
    }

    [ContextMenu("Generate Heightmap")]
    public void GenerateAndVisualize()
    {
        GenerateHeightmap();
        CreateLayersFromHeightmap();
        VisualizeHeightmap();
    }

    void GenerateHeightmap()
    {
        Debug.Log("Generating heightmap...");

        System.Random prng = new System.Random(seed);
        Vector2[] offsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            offsets[i] = new Vector2(
                prng.Next(-10000, 10000),
                prng.Next(-10000, 10000)
            );
        }

        heightMap = new float[mapSize * mapSize];

        ComputeBuffer offsetsBuffer = new ComputeBuffer(offsets.Length, sizeof(float) * 2);
        ComputeBuffer mapBuffer = new ComputeBuffer(heightMap.Length, sizeof(float));

        int floatToIntMultiplier = 1000;
        int[] minMax = { floatToIntMultiplier * octaves, 0 };
        ComputeBuffer minMaxBuffer = new ComputeBuffer(minMax.Length, sizeof(int));

        offsetsBuffer.SetData(offsets);
        mapBuffer.SetData(heightMap);
        minMaxBuffer.SetData(minMax);

        heightMapShader.SetBuffer(0, "heightMap", mapBuffer);
        heightMapShader.SetBuffer(0, "offsets", offsetsBuffer);
        heightMapShader.SetBuffer(0, "minMax", minMaxBuffer);

        heightMapShader.SetInt("mapSize", mapSize);
        heightMapShader.SetInt("octaves", octaves);
        heightMapShader.SetFloat("lacunarity", lacunarity);
        heightMapShader.SetFloat("persistence", persistence);
        heightMapShader.SetFloat("scaleFactor", scale);
        heightMapShader.SetInt("floatToIntMultiplier", floatToIntMultiplier);
        heightMapShader.SetInt("heightMapSize", heightMap.Length);
        heightMapShader.SetInt("seed", seed);

        int threadGroups = Mathf.CeilToInt(heightMap.Length / 64f);
        heightMapShader.Dispatch(0, threadGroups, 1, 1);

        mapBuffer.GetData(heightMap);
        minMaxBuffer.GetData(minMax);

        offsetsBuffer.Release();
        mapBuffer.Release();
        minMaxBuffer.Release();

        float minValue = minMax[0] / (float)floatToIntMultiplier;
        float maxValue = minMax[1] / (float)floatToIntMultiplier;

        for (int i = 0; i < heightMap.Length; i++)
        {
            heightMap[i] = Mathf.InverseLerp(minValue, maxValue, heightMap[i]);
        }

        Debug.Log($"Heightmap generated! Min: {minValue}, Max: {maxValue}");
    }

    void CreateLayersFromHeightmap()
    {
        Debug.Log("Creating layer system from heightmap...");

        voxelColumns = new VoxelColumn[mapSize, mapSize];

        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int index = z * mapSize + x;
                float normalizedHeight = heightMap[index];

                // Convert normalized height to number of 0.1 unit rock layers
                float worldHeight = normalizedHeight * heightScale;
                int rockLayerCount = Mathf.RoundToInt(worldHeight / LAYER_HEIGHT);

                // Create voxel column
                VoxelColumn column = new VoxelColumn();

                // Add rock layers (base terrain from heightmap)
                column.AddLayers(SoilType.Rock, rockLayerCount);

                // Add dirt layers on top
                column.AddLayers(SoilType.Dirt, dirtLayers);

                // Add sand layers on top of dirt
                column.AddLayers(SoilType.Sand, sandLayers);

                voxelColumns[x, z] = column;
            }
        }

        Debug.Log($"Created {mapSize * mapSize} voxel columns with layers (each layer = {LAYER_HEIGHT} units)");
    }

    void VisualizeHeightmap()
    {
        Debug.Log("Creating voxel mesh with layers...");

        if (meshObject == null)
        {
            meshObject = new GameObject("Voxel Terrain Mesh");
            meshObject.transform.parent = transform;
            meshObject.transform.localPosition = Vector3.zero;

            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
        }

        Mesh mesh = CreateVoxelMesh();
        meshFilter.mesh = mesh;

        if (voxelMaterial != null)
        {
            meshRenderer.material = voxelMaterial;
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        Debug.Log($"Created voxel mesh with layers!");
    }

    Mesh CreateVoxelMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Voxel Terrain with Layers";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        float cellSize = (terrainScale * 2f) / mapSize;

        // Step 1: Create all top faces
        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                VoxelColumn column = voxelColumns[x, z];
                float totalHeight = column.GetTotalHeight();

                float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
                float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;

                // Get color of top layer
                int topLayerIndex = column.GetLayerCount() - 1;
                SoilType topSoil = column.GetSoilTypeAtLayer(topLayerIndex);
                Color color = SoilTypeColors.GetColor(topSoil);

                // Top face corners
                Vector3 p0 = new Vector3(worldX, totalHeight, worldZ);
                Vector3 p1 = new Vector3(worldX + cellSize, totalHeight, worldZ);
                Vector3 p2 = new Vector3(worldX + cellSize, totalHeight, worldZ + cellSize);
                Vector3 p3 = new Vector3(worldX, totalHeight, worldZ + cellSize);

                AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);
            }
        }

        // Step 2: Create vertical faces along X axis
        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize - 1; x++)
            {
                VoxelColumn leftColumn = voxelColumns[x, z];
                VoxelColumn rightColumn = voxelColumns[x + 1, z];

                float heightLeft = leftColumn.GetTotalHeight();
                float heightRight = rightColumn.GetTotalHeight();

                if (Mathf.Abs(heightLeft - heightRight) > 0.001f)
                {
                    float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale + cellSize;
                    float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;

                    float minHeight = Mathf.Min(heightLeft, heightRight);
                    float maxHeight = Mathf.Max(heightLeft, heightRight);

                    // Create layered wall showing different soil types
                    VoxelColumn higherColumn = heightLeft > heightRight ? leftColumn : rightColumn;
                    bool faceRight = heightLeft > heightRight;

                    CreateVerticalLayeredFace(vertices, triangles, colors, higherColumn,
                        worldX, worldZ, cellSize, minHeight, maxHeight, 0, faceRight);
                }
            }
        }

        // Step 3: Create vertical faces along Z axis
        for (int z = 0; z < mapSize - 1; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                VoxelColumn backColumn = voxelColumns[x, z];
                VoxelColumn forwardColumn = voxelColumns[x, z + 1];

                float heightBack = backColumn.GetTotalHeight();
                float heightForward = forwardColumn.GetTotalHeight();

                if (Mathf.Abs(heightBack - heightForward) > 0.001f)
                {
                    float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
                    float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale + cellSize;

                    float minHeight = Mathf.Min(heightBack, heightForward);
                    float maxHeight = Mathf.Max(heightBack, heightForward);

                    VoxelColumn higherColumn = heightBack > heightForward ? backColumn : forwardColumn;
                    bool faceForward = heightBack > heightForward;

                    CreateVerticalLayeredFace(vertices, triangles, colors, higherColumn,
                        worldX, worldZ, cellSize, minHeight, maxHeight, 1, faceForward);
                }
            }
        }

        // Step 4: Create edge walls (perimeter) - showing full layer strata
        for (int x = 0; x < mapSize; x++)
        {
            // Bottom edge (z = 0)
            VoxelColumn column = voxelColumns[x, 0];
            float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
            float worldZ = -terrainScale;
            CreateEdgeWall(vertices, triangles, colors, column, worldX, worldZ, cellSize, 0, true);

            // Top edge (z = mapSize - 1)
            column = voxelColumns[x, mapSize - 1];
            worldZ = terrainScale;
            CreateEdgeWall(vertices, triangles, colors, column, worldX, worldZ, cellSize, 0, false);
        }

        for (int z = 0; z < mapSize; z++)
        {
            // Left edge (x = 0)
            VoxelColumn column = voxelColumns[0, z];
            float worldX = -terrainScale;
            float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;
            CreateEdgeWall(vertices, triangles, colors, column, worldX, worldZ, cellSize, 1, false);

            // Right edge (x = mapSize - 1)
            column = voxelColumns[mapSize - 1, z];
            worldX = terrainScale;
            CreateEdgeWall(vertices, triangles, colors, column, worldX, worldZ, cellSize, 1, true);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void CreateVerticalLayeredFace(List<Vector3> verts, List<int> tris, List<Color> colors,
                                   VoxelColumn column, float worldX, float worldZ, float cellSize,
                                   float minHeight, float maxHeight, int axis, bool reverse)
    {
        // Only show layers that are visible in the exposed section
        int startLayer = Mathf.FloorToInt(minHeight / LAYER_HEIGHT);
        int endLayer = Mathf.FloorToInt(maxHeight / LAYER_HEIGHT);

        for (int layer = startLayer; layer <= endLayer && layer < column.GetLayerCount(); layer++)
        {
            float layerBottom = layer * LAYER_HEIGHT;
            float layerTop = (layer + 1) * LAYER_HEIGHT;

            // Clamp to visible section
            layerBottom = Mathf.Max(layerBottom, minHeight);
            layerTop = Mathf.Min(layerTop, maxHeight);

            if (layerTop - layerBottom < 0.001f) continue;

            SoilType soilType = column.GetSoilTypeAtLayer(layer);
            Color color = SoilTypeColors.GetColor(soilType);

            Vector3 p0, p1, p2, p3;

            if (axis == 0) // X-axis
            {
                p0 = new Vector3(worldX, layerBottom, worldZ);
                p1 = new Vector3(worldX, layerBottom, worldZ + cellSize);
                p2 = new Vector3(worldX, layerTop, worldZ + cellSize);
                p3 = new Vector3(worldX, layerTop, worldZ);
            }
            else // Z-axis
            {
                p0 = new Vector3(worldX, layerBottom, worldZ);
                p1 = new Vector3(worldX + cellSize, layerBottom, worldZ);
                p2 = new Vector3(worldX + cellSize, layerTop, worldZ);
                p3 = new Vector3(worldX, layerTop, worldZ);
            }

            if (reverse)
            {
                AddQuad(verts, tris, colors, p0, p1, p2, p3, color);
            }
            else
            {
                AddQuad(verts, tris, colors, p1, p0, p3, p2, color);
            }
        }
    }

    void CreateEdgeWall(List<Vector3> verts, List<int> tris, List<Color> colors,
                       VoxelColumn column, float worldX, float worldZ, float cellSize,
                       int axis, bool reverse)
    {
        float currentHeight = 0;

        for (int layer = 0; layer < column.GetLayerCount(); layer++)
        {
            SoilType soilType = column.GetSoilTypeAtLayer(layer);
            Color color = SoilTypeColors.GetColor(soilType);

            Vector3 p0, p1, p2, p3;

            if (axis == 0) // X-axis wall
            {
                p0 = new Vector3(worldX, currentHeight, worldZ);
                p1 = new Vector3(worldX + cellSize, currentHeight, worldZ);
                p2 = new Vector3(worldX + cellSize, currentHeight + LAYER_HEIGHT, worldZ);
                p3 = new Vector3(worldX, currentHeight + LAYER_HEIGHT, worldZ);
            }
            else // Z-axis wall
            {
                p0 = new Vector3(worldX, currentHeight, worldZ);
                p1 = new Vector3(worldX, currentHeight, worldZ + cellSize);
                p2 = new Vector3(worldX, currentHeight + LAYER_HEIGHT, worldZ + cellSize);
                p3 = new Vector3(worldX, currentHeight + LAYER_HEIGHT, worldZ);
            }

            if (reverse)
            {
                AddQuad(verts, tris, colors, p0, p1, p2, p3, color);
            }
            else
            {
                AddQuad(verts, tris, colors, p1, p0, p3, p2, color);
            }

            currentHeight += LAYER_HEIGHT;
        }
    }

    void AddQuad(List<Vector3> verts, List<int> tris, List<Color> colors,
                 Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
    {
        int startIndex = verts.Count;

        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        tris.Add(startIndex + 0);
        tris.Add(startIndex + 2);
        tris.Add(startIndex + 1);

        tris.Add(startIndex + 0);
        tris.Add(startIndex + 3);
        tris.Add(startIndex + 2);
    }

    void OnDrawGizmosSelected()
    {
        if (voxelColumns != null && voxelColumns.Length > 0)
        {
            float maxHeight = 0;
            foreach (var column in voxelColumns)
            {
                if (column != null)
                {
                    maxHeight = Mathf.Max(maxHeight, column.GetTotalHeight());
                }
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * (maxHeight * 0.5f),
                new Vector3(terrainScale * 2f, maxHeight, terrainScale * 2f)
            );
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * (heightScale * 0.5f),
                new Vector3(terrainScale * 2f, heightScale, terrainScale * 2f)
            );
        }
    }
}