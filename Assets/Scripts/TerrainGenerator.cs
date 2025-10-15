using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class TerrainGenerator : MonoBehaviour
{
    public bool printTimers;

    [Header("Mesh Settings")]
    public int mapSize = 1024;
    public float scale = 20;
    public float elevationScale = 10;
    public Material material;

    [Header("Chunk Settings")]
    public int chunkSize = 64;

    [Header("Side Settings")]
    public bool generateSides = true;
    public float sideDepth = 0f;
    public bool generateBottom = false;
    public Material sideMaterial;

    [Header("Erosion Settings")]
    public ComputeShader erosion;
    public int numErosionIterations = 600000;
    public int erosionBrushRadius = 12;

    public int maxLifetime = 160;
    public float sedimentCapacityFactor = 4;
    public float minSedimentCapacity = .01f;
    public float depositSpeed = 0.3f;
    public float erodeSpeed = 0.2f;

    public float evaporateSpeed = .005f;
    public float gravity = 4;
    public float startSpeed = 3;
    public float startWater = 1;
    [Range(0, 1)]
    public float inertia = 0.1f;

    // Internal
    float[] map;
    int mapSizeWithBorder;

    // Chunk data
    class ChunkData
    {
        public GameObject gameObject;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public MeshCollider meshCollider;
        public Mesh mesh;
        public int chunkX, chunkZ;
    }

    List<ChunkData> terrainChunks = new List<ChunkData>();
    GameObject sidesHolder;
    MeshFilter sidesMeshFilter;
    MeshRenderer sidesMeshRenderer;
    MeshCollider sidesMeshCollider;
    Mesh sidesMesh;

    NavMeshSurface navMeshSurface;

    public void GenerateTerrain()
    {
        // Clear any existing paths
        if (TryGetComponent(out Pathfinder pathfinder))
        {
            pathfinder.ClearPath();
        }

        var sw = new System.Diagnostics.Stopwatch();
        if (printTimers) sw.Start();

        GenerateHeightMap();
        if (printTimers)
        {
            Debug.Log($"{mapSize}x{mapSize} heightmap generated in {sw.ElapsedMilliseconds}ms");
            sw.Restart();
        }

        Erode();
        if (printTimers)
        {
            string numIterationsString = numErosionIterations >= 1000 ? (numErosionIterations / 1000) + "k" : numErosionIterations.ToString();
            Debug.Log($"{numIterationsString} erosion iterations completed in {sw.ElapsedMilliseconds}ms");
            sw.Restart();
        }

        ConstructChunkedMesh();
        if (printTimers)
        {
            Debug.Log($"Chunked mesh constructed in {sw.ElapsedMilliseconds}ms");
            sw.Restart();
        }

        BakeNavMesh();
        if (printTimers)
        {
            Debug.Log($"NavMesh baked in {sw.ElapsedMilliseconds}ms");
            sw.Stop();
        }
    }

    public void GenerateHeightMap()
    {
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;
        map = FindObjectOfType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
    }

    public void Erode()
    {
        int numThreads = numErosionIterations / 1024;

        // Create brush
        List<int> brushIndexOffsets = new List<int>();
        List<float> brushWeights = new List<float>();

        float weightSum = 0;
        for (int brushY = -erosionBrushRadius; brushY <= erosionBrushRadius; brushY++)
        {
            for (int brushX = -erosionBrushRadius; brushX <= erosionBrushRadius; brushX++)
            {
                float sqrDst = brushX * brushX + brushY * brushY;
                if (sqrDst < erosionBrushRadius * erosionBrushRadius)
                {
                    brushIndexOffsets.Add(brushY * mapSize + brushX);
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

        // Send brush data to compute shader
        ComputeBuffer brushIndexBuffer = new ComputeBuffer(brushIndexOffsets.Count, sizeof(int));
        ComputeBuffer brushWeightBuffer = new ComputeBuffer(brushWeights.Count, sizeof(int));
        brushIndexBuffer.SetData(brushIndexOffsets);
        brushWeightBuffer.SetData(brushWeights);
        erosion.SetBuffer(0, "brushIndices", brushIndexBuffer);
        erosion.SetBuffer(0, "brushWeights", brushWeightBuffer);

        // Generate random indices for droplet placement
        int[] randomIndices = new int[numErosionIterations];
        for (int i = 0; i < numErosionIterations; i++)
        {
            int randomX = Random.Range(erosionBrushRadius, mapSize + erosionBrushRadius);
            int randomY = Random.Range(erosionBrushRadius, mapSize + erosionBrushRadius);
            randomIndices[i] = randomY * mapSize + randomX;
        }

        // Send random indices to compute shader
        ComputeBuffer randomIndexBuffer = new ComputeBuffer(randomIndices.Length, sizeof(int));
        randomIndexBuffer.SetData(randomIndices);
        erosion.SetBuffer(0, "randomIndices", randomIndexBuffer);

        // Heightmap buffer
        ComputeBuffer mapBuffer = new ComputeBuffer(map.Length, sizeof(float));
        mapBuffer.SetData(map);
        erosion.SetBuffer(0, "map", mapBuffer);

        // Settings
        erosion.SetInt("borderSize", erosionBrushRadius);
        erosion.SetInt("mapSize", mapSizeWithBorder);
        erosion.SetInt("brushLength", brushIndexOffsets.Count);
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

        // Run compute shader
        erosion.Dispatch(0, numThreads, 1, 1);
        mapBuffer.GetData(map);

        // Release buffers
        mapBuffer.Release();
        randomIndexBuffer.Release();
        brushIndexBuffer.Release();
        brushWeightBuffer.Release();
    }

    void ConstructChunkedMesh()
    {
        // Clear existing chunks
        foreach (var chunk in terrainChunks)
        {
            if (chunk.gameObject != null)
                DestroyImmediate(chunk.gameObject);
        }
        terrainChunks.Clear();

        // Clear sides mesh
        if (sidesHolder != null)
            DestroyImmediate(sidesHolder);

        // Validate chunk size
        if (mapSize % chunkSize != 0)
        {
            Debug.LogError($"Chunk size {chunkSize} must divide map size {mapSize} evenly!");
            chunkSize = 64;
        }

        int numChunks = mapSize / chunkSize;

        // Create terrain holder if needed
        Transform terrainHolder = transform.Find("Terrain Chunks");
        if (terrainHolder == null)
        {
            GameObject holder = new GameObject("Terrain Chunks");
            holder.transform.parent = transform;
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = new Vector3(1, 1, 1);

            terrainHolder = holder.transform;
        }

        // Generate chunks
        for (int chunkZ = 0; chunkZ < numChunks; chunkZ++)
        {
            for (int chunkX = 0; chunkX < numChunks; chunkX++)
            {
                CreateChunk(chunkX, chunkZ, terrainHolder);
            }
        }

        // FIX: Smooth normals across chunk boundaries
        SmoothChunkNormals();

        // Generate sides as separate mesh
        if (generateSides)
        {
            CreateSidesMesh();
        }

        // Set material properties
        if (material != null)
        {
            material.SetFloat("_MaxHeight", elevationScale);
        }
    }

    void CreateChunk(int chunkX, int chunkZ, Transform parent)
    {
        ChunkData chunk = new ChunkData();
        chunk.chunkX = chunkX;
        chunk.chunkZ = chunkZ;

        // Create chunk GameObject
        string chunkName = $"Chunk_{chunkX}_{chunkZ}";
        chunk.gameObject = new GameObject(chunkName);
        chunk.gameObject.transform.parent = parent;
        chunk.gameObject.transform.localPosition = Vector3.zero;
        chunk.gameObject.transform.localRotation = Quaternion.identity;
        chunk.gameObject.transform.localScale = new Vector3(1, 1, 1);

        // Add components
        chunk.meshFilter = chunk.gameObject.AddComponent<MeshFilter>();
        chunk.meshRenderer = chunk.gameObject.AddComponent<MeshRenderer>();
        chunk.meshCollider = chunk.gameObject.AddComponent<MeshCollider>();

        // Generate chunk mesh
        chunk.mesh = GenerateChunkMesh(chunkX, chunkZ);
        chunk.meshFilter.sharedMesh = chunk.mesh;
        chunk.meshCollider.sharedMesh = chunk.mesh;
        chunk.meshRenderer.sharedMaterial = material;

        terrainChunks.Add(chunk);
    }

    Mesh GenerateChunkMesh(int chunkX, int chunkZ)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> triangles = new List<int>();

        int startX = chunkX * chunkSize;
        int startZ = chunkZ * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, mapSize - 1);
        int endZ = Mathf.Min(startZ + chunkSize, mapSize - 1);

        // Generate vertices (include overlap for seamless chunks)
        for (int z = startZ; z <= endZ; z++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int borderedMapIndex = (z + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;

                Vector2 percent = new Vector2(x / (mapSize - 1f), z / (mapSize - 1f));
                Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;

                float normalizedHeight = map[borderedMapIndex];
                pos += Vector3.up * normalizedHeight * elevationScale;
                verts.Add(pos);
            }
        }

        // Generate triangles
        int width = endX - startX + 1;
        for (int z = 0; z < endZ - startZ; z++)
        {
            for (int x = 0; x < endX - startX; x++)
            {
                int i = z * width + x;

                triangles.Add(i + width);
                triangles.Add(i + width + 1);
                triangles.Add(i);

                triangles.Add(i + width + 1);
                triangles.Add(i + 1);
                triangles.Add(i);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"TerrainChunk_{chunkX}_{chunkZ}";
        mesh.vertices = verts.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void SmoothChunkNormals()
    {
        // Create a dictionary to store vertex positions and their accumulated normals
        Dictionary<Vector3, Vector3> vertexNormals = new Dictionary<Vector3, Vector3>();
        Dictionary<Vector3, int> vertexCounts = new Dictionary<Vector3, int>();

        // First pass: accumulate normals for all vertices
        foreach (var chunk in terrainChunks)
        {
            Vector3[] vertices = chunk.mesh.vertices;
            Vector3[] normals = chunk.mesh.normals;
            Transform chunkTransform = chunk.meshFilter.transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                // Convert to world position for comparison
                Vector3 worldPos = chunkTransform.TransformPoint(vertices[i]);
                // Round to avoid floating point precision issues
                Vector3 roundedPos = new Vector3(
                    Mathf.Round(worldPos.x * 1000f) / 1000f,
                    Mathf.Round(worldPos.y * 1000f) / 1000f,
                    Mathf.Round(worldPos.z * 1000f) / 1000f
                );

                Vector3 worldNormal = chunkTransform.TransformDirection(normals[i]);

                if (!vertexNormals.ContainsKey(roundedPos))
                {
                    vertexNormals[roundedPos] = Vector3.zero;
                    vertexCounts[roundedPos] = 0;
                }

                vertexNormals[roundedPos] += worldNormal;
                vertexCounts[roundedPos]++;
            }
        }

        // Average the normals
        List<Vector3> keys = new List<Vector3>(vertexNormals.Keys);
        foreach (var key in keys)
        {
            vertexNormals[key] = (vertexNormals[key] / vertexCounts[key]).normalized;
        }

        // Second pass: apply smoothed normals back to chunks
        foreach (var chunk in terrainChunks)
        {
            Vector3[] vertices = chunk.mesh.vertices;
            Vector3[] normals = chunk.mesh.normals;
            Transform chunkTransform = chunk.meshFilter.transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = chunkTransform.TransformPoint(vertices[i]);
                Vector3 roundedPos = new Vector3(
                    Mathf.Round(worldPos.x * 1000f) / 1000f,
                    Mathf.Round(worldPos.y * 1000f) / 1000f,
                    Mathf.Round(worldPos.z * 1000f) / 1000f
                );

                if (vertexNormals.ContainsKey(roundedPos))
                {
                    // Convert smoothed world normal back to local space
                    normals[i] = chunkTransform.InverseTransformDirection(vertexNormals[roundedPos]);
                }
            }

            chunk.mesh.normals = normals;
        }
    }

    void CreateSidesMesh()
    {
        sidesHolder = new GameObject("Terrain Sides");
        sidesHolder.transform.parent = transform;
        sidesHolder.transform.localPosition = Vector3.zero;
        sidesHolder.transform.localRotation = Quaternion.identity;
        sidesHolder.transform.localScale = new Vector3(1, 1, 1);

        sidesMeshFilter = sidesHolder.AddComponent<MeshFilter>();
        sidesMeshRenderer = sidesHolder.AddComponent<MeshRenderer>();
        sidesMeshCollider = sidesHolder.AddComponent<MeshCollider>();

        List<Vector3> verts = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Get edge vertices from heightmap
        List<Vector3> topEdgeVerts = new List<Vector3>();

        // Front edge (z = 0)
        for (int x = 0; x < mapSize; x++)
        {
            int borderedMapIndex = erosionBrushRadius * mapSizeWithBorder + x + erosionBrushRadius;
            Vector2 percent = new Vector2(x / (mapSize - 1f), 0);
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
            pos += Vector3.up * map[borderedMapIndex] * elevationScale;
            topEdgeVerts.Add(pos);
        }

        // Right edge (x = mapSize-1)
        for (int z = 1; z < mapSize; z++)
        {
            int borderedMapIndex = (z + erosionBrushRadius) * mapSizeWithBorder + (mapSize - 1) + erosionBrushRadius;
            Vector2 percent = new Vector2(1, z / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
            pos += Vector3.up * map[borderedMapIndex] * elevationScale;
            topEdgeVerts.Add(pos);
        }

        // Back edge (z = mapSize-1, reversed)
        for (int x = mapSize - 2; x >= 0; x--)
        {
            int borderedMapIndex = ((mapSize - 1) + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;
            Vector2 percent = new Vector2(x / (mapSize - 1f), 1);
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
            pos += Vector3.up * map[borderedMapIndex] * elevationScale;
            topEdgeVerts.Add(pos);
        }

        // Left edge (x = 0, reversed)
        for (int z = mapSize - 2; z > 0; z--)
        {
            int borderedMapIndex = (z + erosionBrushRadius) * mapSizeWithBorder + erosionBrushRadius;
            Vector2 percent = new Vector2(0, z / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
            pos += Vector3.up * map[borderedMapIndex] * elevationScale;
            topEdgeVerts.Add(pos);
        }

        // Create side mesh
        int edgeVertCount = topEdgeVerts.Count;

        // Add top edge vertices
        verts.AddRange(topEdgeVerts);

        // Add bottom edge vertices
        for (int i = 0; i < edgeVertCount; i++)
        {
            Vector3 bottomVert = new Vector3(topEdgeVerts[i].x, sideDepth, topEdgeVerts[i].z);
            verts.Add(bottomVert);
        }

        // Create side triangles
        for (int i = 0; i < edgeVertCount; i++)
        {
            int next = (i + 1) % edgeVertCount;
            int topCurrent = i;
            int topNext = next;
            int bottomCurrent = edgeVertCount + i;
            int bottomNext = edgeVertCount + next;

            // Create quad
            triangles.Add(topCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomCurrent);

            triangles.Add(topNext);
            triangles.Add(bottomNext);
            triangles.Add(bottomCurrent);
        }

        // Optional bottom face
        if (generateBottom)
        {
            int centerIndex = verts.Count;
            verts.Add(new Vector3(0, sideDepth, 0)); // Center point for bottom

            for (int i = 0; i < edgeVertCount; i++)
            {
                int next = (i + 1) % edgeVertCount;
                triangles.Add(centerIndex);
                triangles.Add(edgeVertCount + next);
                triangles.Add(edgeVertCount + i);
            }
        }

        sidesMesh = new Mesh();
        sidesMesh.name = "TerrainSides";
        sidesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        sidesMesh.vertices = verts.ToArray();
        sidesMesh.triangles = triangles.ToArray();
        sidesMesh.RecalculateNormals();
        sidesMesh.RecalculateBounds();

        sidesMeshFilter.sharedMesh = sidesMesh;
        sidesMeshCollider.sharedMesh = sidesMesh;
        sidesMeshRenderer.sharedMaterial = sideMaterial != null ? sideMaterial : material;
    }

    public MeshFilter GetMeshFilter()
    {
        // Return first chunk's mesh filter for compatibility
        if (terrainChunks.Count > 0)
            return terrainChunks[0].meshFilter;
        return null;
    }

    void BakeNavMesh()
    {
        // Find or create NavMeshSurface on parent
        if (navMeshSurface == null)
            navMeshSurface = GetComponent<NavMeshSurface>();

        if (navMeshSurface == null)
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh baked successfully!");
        }
        else
        {
            Debug.LogWarning("NavMeshSurface component not found. Please add one.");
        }
    }
}