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
    MeshCollider meshCollider; // Add this line

    // Pathfinding
    private LineRenderer pathLineRenderer;
    private Vector3 pathStartPoint;
    private Vector3 pathEndPoint;
    private bool hasPathPoints = false;

    void Start()
    {
        if (Application.isPlaying)
        {
            GenerateHeightMap();
            ContructMesh();
            BakeNavMesh();
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
        // Clear crater mask when generating new mesh
        ExplosionManager explosionMgr = GetComponent<ExplosionManager>();
        if (explosionMgr != null)
        {
            explosionMgr.ResetCraterMask();
        }

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
        meshCollider.sharedMesh = mesh; // Assign mesh to the collider
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
        // Add or get MeshCollider
        if (!meshHolder.GetComponent<MeshCollider>())
        {
            meshHolder.gameObject.AddComponent<MeshCollider>();
        }

        pathLineRenderer = meshHolder.GetComponent<LineRenderer>();
        if (pathLineRenderer == null)
        {
            pathLineRenderer = meshHolder.gameObject.AddComponent<LineRenderer>();
            pathLineRenderer.startWidth = 0.5f;
            pathLineRenderer.endWidth = 0.5f;
            pathLineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            pathLineRenderer.startColor = Color.green;
            pathLineRenderer.endColor = Color.green;
            pathLineRenderer.positionCount = 0;
        }


        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
        navMeshSurface = meshHolder.GetComponent<NavMeshSurface>();
        meshCollider = meshHolder.GetComponent<MeshCollider>(); // Get the collider
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
            Debug.Log("NavMesh baked successfully at runtime!");
        }
        else
        {
            Debug.LogWarning("NavMeshSurface component not found on Mesh Holder object. Please add one.");
        }
    }

    public void TestPathfinding()
    {
        Vector3 startPos, endPos;
        if (!GetRandomPointOnNavMesh(out startPos) || !GetRandomPointOnNavMesh(out endPos))
        {
            return;
        }

        pathStartPoint = startPos;
        pathEndPoint = endPos;
        hasPathPoints = true;

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                Debug.Log("Path found!");
                UpdatePathVisuals(path); // Use the new method
            }
            else
            {
                Debug.LogWarning($"Path found but is incomplete. Status: {path.status}");
                pathLineRenderer.positionCount = 0;
            }
        }
        else
        {
            Debug.LogError("Failed to calculate path.");
            pathLineRenderer.positionCount = 0;
        }
    }

    // NEW METHOD to drape the line over the terrain
    void UpdatePathVisuals(NavMeshPath path)
    {
        if (path.corners.Length < 2)
        {
            pathLineRenderer.positionCount = 0;
            return;
        }

        List<Vector3> drapedPathPoints = new List<Vector3>();
        float subdivisionDensity = 1.0f; // Add a point every 1 unit

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 startCorner = path.corners[i];
            Vector3 endCorner = path.corners[i + 1];
            drapedPathPoints.Add(startCorner); // Always add the corner itself

            float distance = Vector3.Distance(startCorner, endCorner);
            int subdivisions = Mathf.FloorToInt(distance * subdivisionDensity);

            for (int j = 1; j < subdivisions; j++)
            {
                float t = (float)j / subdivisions;
                Vector3 pointOnLine = Vector3.Lerp(startCorner, endCorner, t);

                // Raycast down to find the terrain surface
                RaycastHit hit;
                if (Physics.Raycast(pointOnLine + Vector3.up * 20, Vector3.down, out hit, 40.0f))
                {
                    drapedPathPoints.Add(hit.point + Vector3.up * 0.1f); // Add a small offset to prevent z-fighting
                }
            }
        }
        drapedPathPoints.Add(path.corners[path.corners.Length - 1]); // Add the final corner

        pathLineRenderer.positionCount = drapedPathPoints.Count;
        pathLineRenderer.SetPositions(drapedPathPoints.ToArray());
    }

    private bool GetRandomPointOnNavMesh(out Vector3 point)
    {
        point = Vector3.zero;
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshFilter not found, cannot get a random point.");
            return false;
        }

        int randomIndex = Random.Range(0, mapSize * mapSize);
        Vector3 randomLocalPos = meshFilter.sharedMesh.vertices[randomIndex];
        Vector3 randomWorldPos = meshFilter.transform.TransformPoint(randomLocalPos);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomWorldPos, out hit, 20.0f, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }
        else
        {
            Debug.LogError($"Failed to find a valid point on the NavMesh near {randomWorldPos}. Is the NavMesh baked and is the area walkable?");
            return false;
        }
    }

    void OnDrawGizmos()
    {
        if (hasPathPoints)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pathStartPoint, 1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pathEndPoint, 1f);
        }
    }
}