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
    public float voxelSize = 1f;
    public float heightScale = 10f;
    public float terrainScale = 20f;
    public Material voxelMaterial;

    [Header("Compute")]
    public ComputeShader heightMapShader;

    private float[] heightMap;
    private GameObject meshObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

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
        VisualizeHeightmap();
    }

    void GenerateHeightmap()
    {
        Debug.Log("Generating heightmap...");

        // Create random offsets for each octave
        System.Random prng = new System.Random(seed);
        Vector2[] offsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            offsets[i] = new Vector2(
                prng.Next(-10000, 10000),
                prng.Next(-10000, 10000)
            );
        }

        // Setup buffers
        heightMap = new float[mapSize * mapSize];

        ComputeBuffer offsetsBuffer = new ComputeBuffer(offsets.Length, sizeof(float) * 2);
        ComputeBuffer mapBuffer = new ComputeBuffer(heightMap.Length, sizeof(float));

        int floatToIntMultiplier = 1000;
        int[] minMax = { floatToIntMultiplier * octaves, 0 };
        ComputeBuffer minMaxBuffer = new ComputeBuffer(minMax.Length, sizeof(int));

        // Set data
        offsetsBuffer.SetData(offsets);
        mapBuffer.SetData(heightMap);
        minMaxBuffer.SetData(minMax);

        // Set shader parameters
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

        // Dispatch
        int threadGroups = Mathf.CeilToInt(heightMap.Length / 64f);
        heightMapShader.Dispatch(0, threadGroups, 1, 1);

        // Get results
        mapBuffer.GetData(heightMap);
        minMaxBuffer.GetData(minMax);

        // Cleanup
        offsetsBuffer.Release();
        mapBuffer.Release();
        minMaxBuffer.Release();

        // Normalize heightmap
        float minValue = minMax[0] / (float)floatToIntMultiplier;
        float maxValue = minMax[1] / (float)floatToIntMultiplier;

        for (int i = 0; i < heightMap.Length; i++)
        {
            heightMap[i] = Mathf.InverseLerp(minValue, maxValue, heightMap[i]);
        }

        Debug.Log($"Heightmap generated! Min: {minValue}, Max: {maxValue}");
    }

    void VisualizeHeightmap()
    {
        Debug.Log("Creating voxel mesh...");

        // Create or get mesh object
        if (meshObject == null)
        {
            meshObject = new GameObject("Voxel Terrain Mesh");
            meshObject.transform.parent = transform;
            meshObject.transform.localPosition = Vector3.zero;

            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
        }

        // Create voxel mesh
        Mesh mesh = CreateVoxelMesh();
        meshFilter.mesh = mesh;

        // Set material
        if (voxelMaterial != null)
        {
            meshRenderer.material = voxelMaterial;
        }
        else
        {
            // Create default material if none assigned
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        Debug.Log($"Created voxel mesh with {heightMap.Length} voxels!");
    }

    Mesh CreateVoxelMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Voxel Terrain";
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
                int index = z * mapSize + x;
                float height = heightMap[index] * heightScale;

                // Calculate position
                float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
                float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;

                // Color by height
                Color color = Color.Lerp(Color.blue, Color.red, heightMap[index]);

                // Top face corners
                Vector3 p0 = new Vector3(worldX, height, worldZ);
                Vector3 p1 = new Vector3(worldX + cellSize, height, worldZ);
                Vector3 p2 = new Vector3(worldX + cellSize, height, worldZ + cellSize);
                Vector3 p3 = new Vector3(worldX, height, worldZ + cellSize);

                // Add top quad (counter-clockwise from above: p0 -> p1 -> p2 -> p3)
                AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);
            }
        }

        // Step 2: Create vertical faces along X axis (connect columns in X direction)
        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize - 1; x++)
            {
                int index = z * mapSize + x;
                int rightIndex = index + 1;

                float heightLeft = heightMap[index] * heightScale;
                float heightRight = heightMap[rightIndex] * heightScale;

                // Only create face if there's a height difference
                if (Mathf.Abs(heightLeft - heightRight) > 0.001f)
                {
                    float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale + cellSize;
                    float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;

                    float minHeight = Mathf.Min(heightLeft, heightRight);
                    float maxHeight = Mathf.Max(heightLeft, heightRight);

                    // Determine color (use the higher voxel's color)
                    Color color = heightLeft > heightRight ?
                        Color.Lerp(Color.blue, Color.red, heightMap[index]) :
                        Color.Lerp(Color.blue, Color.red, heightMap[rightIndex]);

                    // Vertical face between the two heights
                    Vector3 p0 = new Vector3(worldX, minHeight, worldZ);
                    Vector3 p1 = new Vector3(worldX, minHeight, worldZ + cellSize);
                    Vector3 p2 = new Vector3(worldX, maxHeight, worldZ + cellSize);
                    Vector3 p3 = new Vector3(worldX, maxHeight, worldZ);

                    // Face orientation depends on which side is higher
                    if (heightLeft > heightRight)
                    {
                        // Left is higher, face points right (+X direction)
                        AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);
                    }
                    else
                    {
                        // Right is higher, face points left (-X direction)
                        AddQuad(vertices, triangles, colors, p1, p0, p3, p2, color);
                    }
                }
            }
        }

        // Step 3: Create vertical faces along Z axis (connect columns in Z direction)
        for (int z = 0; z < mapSize - 1; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int index = z * mapSize + x;
                int forwardIndex = index + mapSize;

                float heightBack = heightMap[index] * heightScale;
                float heightForward = heightMap[forwardIndex] * heightScale;

                // Only create face if there's a height difference
                if (Mathf.Abs(heightBack - heightForward) > 0.001f)
                {
                    float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
                    float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale + cellSize;

                    float minHeight = Mathf.Min(heightBack, heightForward);
                    float maxHeight = Mathf.Max(heightBack, heightForward);

                    // Determine color (use the higher voxel's color)
                    Color color = heightBack > heightForward ?
                        Color.Lerp(Color.blue, Color.red, heightMap[index]) :
                        Color.Lerp(Color.blue, Color.red, heightMap[forwardIndex]);

                    // Vertical face between the two heights
                    Vector3 p0 = new Vector3(worldX, minHeight, worldZ);
                    Vector3 p1 = new Vector3(worldX + cellSize, minHeight, worldZ);
                    Vector3 p2 = new Vector3(worldX + cellSize, maxHeight, worldZ);
                    Vector3 p3 = new Vector3(worldX, maxHeight, worldZ);

                    // Face orientation depends on which side is higher
                    if (heightBack > heightForward)
                    {
                        // Back is higher, face points forward (+Z direction)
                        AddQuad(vertices, triangles, colors, p1, p0, p3, p2, color);
                    }
                    else
                    {
                        // Forward is higher, face points back (-Z direction)
                        AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);
                    }
                }
            }
        }

        // Step 4: Create edge walls (perimeter)
        for (int x = 0; x < mapSize; x++)
        {
            // Bottom edge (z = 0)
            int index = x;
            float height = heightMap[index] * heightScale;
            float worldX = ((x / (float)mapSize) * 2f - 1f) * terrainScale;
            float worldZ = -terrainScale;
            Color color = Color.Lerp(Color.blue, Color.red, heightMap[index]);

            Vector3 p0 = new Vector3(worldX, 0, worldZ);
            Vector3 p1 = new Vector3(worldX + cellSize, 0, worldZ);
            Vector3 p2 = new Vector3(worldX + cellSize, height, worldZ);
            Vector3 p3 = new Vector3(worldX, height, worldZ);
            AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);

            // Top edge (z = mapSize - 1)
            index = (mapSize - 1) * mapSize + x;
            height = heightMap[index] * heightScale;
            worldZ = -terrainScale + (terrainScale * 2f);
            color = Color.Lerp(Color.blue, Color.red, heightMap[index]);

            p0 = new Vector3(worldX, 0, worldZ);
            p1 = new Vector3(worldX + cellSize, 0, worldZ);
            p2 = new Vector3(worldX + cellSize, height, worldZ);
            p3 = new Vector3(worldX, height, worldZ);
            AddQuad(vertices, triangles, colors, p1, p0, p3, p2, color);
        }

        for (int z = 0; z < mapSize; z++)
        {
            // Left edge (x = 0)
            int index = z * mapSize;
            float height = heightMap[index] * heightScale;
            float worldX = -terrainScale;
            float worldZ = ((z / (float)mapSize) * 2f - 1f) * terrainScale;
            Color color = Color.Lerp(Color.blue, Color.red, heightMap[index]);

            Vector3 p0 = new Vector3(worldX, 0, worldZ);
            Vector3 p1 = new Vector3(worldX, 0, worldZ + cellSize);
            Vector3 p2 = new Vector3(worldX, height, worldZ + cellSize);
            Vector3 p3 = new Vector3(worldX, height, worldZ);
            AddQuad(vertices, triangles, colors, p1, p0, p3, p2, color);

            // Right edge (x = mapSize - 1)
            index = z * mapSize + (mapSize - 1);
            height = heightMap[index] * heightScale;
            worldX = -terrainScale + (terrainScale * 2f);
            color = Color.Lerp(Color.blue, Color.red, heightMap[index]);

            p0 = new Vector3(worldX, 0, worldZ);
            p1 = new Vector3(worldX, 0, worldZ + cellSize);
            p2 = new Vector3(worldX, height, worldZ + cellSize);
            p3 = new Vector3(worldX, height, worldZ);
            AddQuad(vertices, triangles, colors, p0, p1, p2, p3, color);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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

        // Two triangles to make a quad (counter-clockwise winding)
        tris.Add(startIndex + 0);
        tris.Add(startIndex + 2);
        tris.Add(startIndex + 1);

        tris.Add(startIndex + 0);
        tris.Add(startIndex + 3);
        tris.Add(startIndex + 2);
    }

    void OnDrawGizmosSelected()
    {
        // Draw bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            transform.position + Vector3.up * (heightScale * 0.5f),
            new Vector3(terrainScale * 2f, heightScale, terrainScale * 2f)
        );
    }
}