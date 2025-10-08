using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class TerrainGenerator : MonoBehaviour
{
    public bool printTimers;

    [Header("Mesh Settings")]
    public int mapSize = 255;
    public float scale = 20;
    public float elevationScale = 10;
    public Material material;

    [Header("Side Settings")]
    public bool generateSides = true;
    public float sideDepth = 0f;
    public bool generateBottom = false;

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
    int mapSizeWithBorder;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    NavMeshSurface navMeshSurface;
    MeshCollider meshCollider;

    public void GenerateTerrain()
    {
        // Try to find the Pathfinder component and clear any existing paths
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

        ContructMesh();
        if (printTimers)
        {
            Debug.Log($"Mesh constructed in {sw.ElapsedMilliseconds}ms");
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

    public void ContructMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Generate top surface vertices
        for (int i = 0; i < mapSize * mapSize; i++)
        {
            int x = i % mapSize;
            int y = i / mapSize;
            int borderedMapIndex = (y + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;

            Vector2 percent = new Vector2(x / (mapSize - 1f), y / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;

            float normalizedHeight = map[borderedMapIndex];
            pos += Vector3.up * normalizedHeight * elevationScale;
            verts.Add(pos);
        }

        // Generate top surface triangles
        for (int y = 0; y < mapSize - 1; y++)
        {
            for (int x = 0; x < mapSize - 1; x++)
            {
                int i = y * mapSize + x;

                triangles.Add(i + mapSize);
                triangles.Add(i + mapSize + 1);
                triangles.Add(i);

                triangles.Add(i + mapSize + 1);
                triangles.Add(i + 1);
                triangles.Add(i);
            }
        }

        if (generateSides)
        {
            int topVertCount = verts.Count;

            // Add bottom edge vertices
            for (int x = 0; x < mapSize; x++)
            {
                Vector3 topVert = verts[x];
                verts.Add(new Vector3(topVert.x, sideDepth, topVert.z));
            }
            for (int x = 0; x < mapSize; x++)
            {
                Vector3 topVert = verts[(mapSize - 1) * mapSize + x];
                verts.Add(new Vector3(topVert.x, sideDepth, topVert.z));
            }
            for (int y = 1; y < mapSize - 1; y++)
            {
                Vector3 topVert = verts[y * mapSize];
                verts.Add(new Vector3(topVert.x, sideDepth, topVert.z));
            }
            for (int y = 1; y < mapSize - 1; y++)
            {
                Vector3 topVert = verts[y * mapSize + mapSize - 1];
                verts.Add(new Vector3(topVert.x, sideDepth, topVert.z));
            }

            int bottomStart = topVertCount;

            // Front side
            for (int x = 0; x < mapSize - 1; x++)
            {
                int topLeft = x;
                int topRight = x + 1;
                int bottomLeft = bottomStart + x;
                int bottomRight = bottomStart + x + 1;

                triangles.Add(topLeft);
                triangles.Add(topRight);
                triangles.Add(bottomLeft);

                triangles.Add(topRight);
                triangles.Add(bottomRight);
                triangles.Add(bottomLeft);
            }

            // Back side
            int backOffset = bottomStart + mapSize;
            for (int x = 0; x < mapSize - 1; x++)
            {
                int topLeft = (mapSize - 1) * mapSize + x;
                int topRight = (mapSize - 1) * mapSize + x + 1;
                int bottomLeft = backOffset + x;
                int bottomRight = backOffset + x + 1;

                triangles.Add(bottomLeft);
                triangles.Add(topRight);
                triangles.Add(topLeft);

                triangles.Add(bottomLeft);
                triangles.Add(bottomRight);
                triangles.Add(topRight);
            }

            // Left side
            int leftOffset = bottomStart + mapSize * 2;
            for (int y = 0; y < mapSize - 1; y++)
            {
                int topCurrent = y * mapSize;
                int topNext = (y + 1) * mapSize;
                int bottomCurrent = (y == 0) ? bottomStart : leftOffset + y - 1;
                int bottomNext = (y == mapSize - 2) ? backOffset : leftOffset + y;

                triangles.Add(bottomCurrent);
                triangles.Add(topNext);
                triangles.Add(topCurrent);

                triangles.Add(bottomCurrent);
                triangles.Add(bottomNext);
                triangles.Add(topNext);
            }

            // Right side
            int rightOffset = bottomStart + mapSize * 2 + mapSize - 2;
            for (int y = 0; y < mapSize - 1; y++)
            {
                int topCurrent = y * mapSize + mapSize - 1;
                int topNext = (y + 1) * mapSize + mapSize - 1;
                int bottomCurrent = (y == 0) ? bottomStart + mapSize - 1 : rightOffset + y - 1;
                int bottomNext = (y == mapSize - 2) ? backOffset + mapSize - 1 : rightOffset + y;

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
                int b0 = bottomStart;
                int b1 = bottomStart + mapSize - 1;
                int b2 = backOffset;
                int b3 = backOffset + mapSize - 1;

                triangles.Add(b0);
                triangles.Add(b2);
                triangles.Add(b1);

                triangles.Add(b1);
                triangles.Add(b2);
                triangles.Add(b3);
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh();
        }
        else
        {
            mesh.Clear();
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        AssignMeshComponents();
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;

        material.SetFloat("_MaxHeight", elevationScale);
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
        if (!meshHolder.GetComponent<NavMeshSurface>())
        {
            meshHolder.gameObject.AddComponent<NavMeshSurface>();
        }
        if (!meshHolder.GetComponent<MeshCollider>())
        {
            meshHolder.gameObject.AddComponent<MeshCollider>();
        }

        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
        navMeshSurface = meshHolder.GetComponent<NavMeshSurface>();
        meshCollider = meshHolder.GetComponent<MeshCollider>();
    }

    public MeshFilter GetMeshFilter()
    {
        if (meshFilter == null)
        {
            AssignMeshComponents();
        }
        return meshFilter;
    }

    public void BakeNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh baked successfully!");
        }
        else
        {
            Debug.LogWarning("NavMeshSurface component not found on Mesh Holder object. Please add one.");
        }
    }
}