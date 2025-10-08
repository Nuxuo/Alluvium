using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(TerrainGenerator))]
public class Pathfinder : MonoBehaviour
{
    private TerrainGenerator terrainGenerator;
    private Vector3 pathStartPoint;
    private Vector3 pathEndPoint;
    private bool hasPathPoints = false;
    private List<Vector3> pathCorners = new List<Vector3>();

    void Awake()
    {
        // Get a reference to the TerrainGenerator to access the mesh
        terrainGenerator = GetComponent<TerrainGenerator>();
    }

    /// <summary>
    /// Ensures the Pathfinder has a reference to the TerrainGenerator.
    /// </summary>
    private void EnsureReferences()
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = GetComponent<TerrainGenerator>();
        }
    }

    /// <summary>
    /// Clears the currently displayed path visuals.
    /// </summary>
    public void ClearPath()
    {
        hasPathPoints = false;
        pathCorners.Clear();
    }


    /// <summary>
    /// Finds a path between two random points on the terrain's NavMesh and draws it.
    /// </summary>
    public void FindAndDrawPath()
    {
        EnsureReferences(); // Make sure references are set up

        Vector3 startPos, endPos;
        // Try several times to find valid start and end points before giving up.
        if (!FindValidPointWithRetries(out startPos) || !FindValidPointWithRetries(out endPos))
        {
            return; // Could not find two valid points.
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
                // Store the path corners to be drawn by Gizmos
                pathCorners = new List<Vector3>(path.corners);
            }
            else
            {
                Debug.LogWarning($"Path found but is incomplete. Status: {path.status}");
                pathCorners.Clear();
            }
        }
        else
        {
            Debug.LogError("Failed to calculate path.");
            pathCorners.Clear();
        }
    }

    /// <summary>
    /// Tries to find a valid random point on the NavMesh, attempting multiple times.
    /// </summary>
    /// <param name="foundPoint">The valid point that was found.</param>
    /// <param name="maxAttempts">The maximum number of attempts to find a point.</param>
    /// <returns>True if a valid point was found, otherwise false.</returns>
    private bool FindValidPointWithRetries(out Vector3 foundPoint, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            if (GetRandomPointOnNavMesh(out foundPoint))
            {
                return true;
            }
        }

        foundPoint = Vector3.zero;
        Debug.LogError($"Could not find a valid point on the NavMesh after {maxAttempts} attempts. Is the NavMesh baked correctly?");
        return false;
    }

    private bool GetRandomPointOnNavMesh(out Vector3 point)
    {
        point = Vector3.zero;
        MeshFilter meshFilter = terrainGenerator.GetMeshFilter();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            // This can happen if terrain hasn't been generated yet.
            Debug.LogError("MeshFilter not found. Please generate the terrain first.");
            return false;
        }

        // Use the mapSize from the terrain generator to ensure we only pick points on the top surface
        int topVertexCount = terrainGenerator.mapSize * terrainGenerator.mapSize;
        int randomIndex = Random.Range(0, topVertexCount);

        Vector3 randomLocalPos = meshFilter.sharedMesh.vertices[randomIndex];
        Vector3 randomWorldPos = meshFilter.transform.TransformPoint(randomLocalPos);

        NavMeshHit hit;
        // Sample for a point on the NavMesh near our random vertex
        if (NavMesh.SamplePosition(randomWorldPos, out hit, 20.0f, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        // Return false if no valid point was found in the search radius
        return false;
    }

    void OnDrawGizmos()
    {
        // Draw the start and end point spheres
        if (hasPathPoints)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pathStartPoint, 1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pathEndPoint, 1f);
        }

        // Draw the path using a gizmo line that sticks to the terrain
        if (pathCorners.Count > 1)
        {
            Gizmos.color = Color.yellow;
            // Iterate through each segment of the path
            for (int i = 0; i < pathCorners.Count - 1; i++)
            {
                Vector3 startCorner = pathCorners[i];
                Vector3 endCorner = pathCorners[i + 1];

                float distance = Vector3.Distance(startCorner, endCorner);
                int subdivisions = Mathf.Max(1, Mathf.FloorToInt(distance)); // Subdivide based on distance

                Vector3 previousPoint = startCorner;

                // Subdivide the line segment to drape it over the terrain
                for (int j = 1; j <= subdivisions; j++)
                {
                    float t = (float)j / subdivisions;
                    Vector3 pointOnLine = Vector3.Lerp(startCorner, endCorner, t);

                    // Raycast down to find the terrain surface
                    RaycastHit hit;
                    Vector3 currentPoint;
                    if (Physics.Raycast(pointOnLine + Vector3.up * 20, Vector3.down, out hit, 40.0f))
                    {
                        currentPoint = hit.point + Vector3.up * 0.2f; // Add a small offset to prevent z-fighting
                    }
                    else
                    {
                        currentPoint = pointOnLine + Vector3.up * 0.2f; // Fallback if no terrain is hit
                    }

                    // For the first point in the segment, find its height as well
                    if (j == 1)
                    {
                        if (Physics.Raycast(previousPoint + Vector3.up * 20, Vector3.down, out hit, 40.0f))
                        {
                            previousPoint = hit.point + Vector3.up * 0.2f;
                        }
                    }

                    Gizmos.DrawLine(previousPoint, currentPoint);
                    previousPoint = currentPoint;
                }
            }
        }
    }
}