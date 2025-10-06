using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionManager : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float explosionDepth = 3f;
    public float explosionDelay = 2f;
    public AnimationCurve craterFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Visual Settings")]
    public GameObject explosionMarkerPrefab;
    public ParticleSystem explosionEffect;

    private TerrainGenerator terrainGen;
    private List<GameObject> activeMarkers = new List<GameObject>();
    private Texture2D craterMask;
    private int textureSize = 512;

    void Start()
    {
        terrainGen = GetComponent<TerrainGenerator>();

        // Create crater mask texture
        craterMask = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
        Color[] clearPixels = new Color[textureSize * textureSize];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = Color.black;
        craterMask.SetPixels(clearPixels);
        craterMask.Apply();

        // Apply to material
        if (terrainGen.material != null)
        {
            terrainGen.material.SetTexture("_CraterMask", craterMask);
            terrainGen.material.SetFloat("_TerrainScale", terrainGen.scale);
        }

        // Create simple marker if none assigned
        if (explosionMarkerPrefab == null)
        {
            explosionMarkerPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosionMarkerPrefab.transform.localScale = Vector3.one * 0.5f;
            explosionMarkerPrefab.GetComponent<Renderer>().material.color = Color.red;
            explosionMarkerPrefab.SetActive(false);
        }
    }

    public void PlaceRandomExplosion()
    {
        if (terrainGen == null || terrainGen.GetMeshFilter() == null)
        {
            Debug.LogWarning("Terrain mesh not generated yet!");
            return;
        }

        Vector3 randomPoint = GetRandomPointOnTerrain();
        PlaceExplosion(randomPoint);
    }

    public void PlaceExplosion(Vector3 worldPosition)
    {
        // Create marker
        GameObject marker = Instantiate(explosionMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.SetActive(true);
        activeMarkers.Add(marker);

        // Start explosion countdown
        StartCoroutine(DetonateAfterDelay(worldPosition, marker));
    }

    IEnumerator DetonateAfterDelay(Vector3 position, GameObject marker)
    {
        yield return new WaitForSeconds(explosionDelay);

        // Remove marker
        if (marker != null)
        {
            activeMarkers.Remove(marker);
            Destroy(marker);
        }

        // Create explosion effect
        if (explosionEffect != null)
        {
            ParticleSystem effect = Instantiate(explosionEffect, position, Quaternion.identity);
            Destroy(effect.gameObject, 3f);
        }

        // Deform mesh
        DeformMesh(position);
    }

    void DeformMesh(Vector3 explosionCenter)
    {
        MeshFilter meshFilter = terrainGen.GetMeshFilter();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform meshTransform = meshFilter.transform;

        // Convert explosion center to local space
        Vector3 localExplosionCenter = meshTransform.InverseTransformPoint(explosionCenter);

        // Deform vertices
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            float distance = Vector3.Distance(new Vector3(vertex.x, 0, vertex.z),
                                             new Vector3(localExplosionCenter.x, 0, localExplosionCenter.z));

            if (distance < explosionRadius)
            {
                float normalizedDistance = distance / explosionRadius;
                float falloff = craterFalloff.Evaluate(normalizedDistance);
                vertices[i].y -= explosionDepth * falloff;
            }
        }

        // Update mesh
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Paint crater onto texture
        PaintCraterOnTexture(explosionCenter);
    }

    void PaintCraterOnTexture(Vector3 worldPosition)
    {
        if (craterMask == null) return;

        MeshFilter meshFilter = terrainGen.GetMeshFilter();
        Transform meshTransform = meshFilter.transform;
        Vector3 localPos = meshTransform.InverseTransformPoint(worldPosition);

        // Convert local position to UV (0-1 range)
        float uvX = (localPos.x / terrainGen.scale + 1f) * 0.5f;
        float uvY = (localPos.z / terrainGen.scale + 1f) * 0.5f;

        // Convert to texture coordinates
        int centerX = Mathf.RoundToInt(uvX * textureSize);
        int centerY = Mathf.RoundToInt(uvY * textureSize);

        // Paint crater in radius
        int pixelRadius = Mathf.RoundToInt((explosionRadius / terrainGen.scale) * textureSize * 0.5f);

        for (int y = -pixelRadius; y <= pixelRadius; y++)
        {
            for (int x = -pixelRadius; x <= pixelRadius; x++)
            {
                int px = centerX + x;
                int py = centerY + y;

                if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                {
                    float dist = Mathf.Sqrt(x * x + y * y);
                    if (dist <= pixelRadius)
                    {
                        float normalizedDist = dist / pixelRadius;
                        float intensity = craterFalloff.Evaluate(normalizedDist);

                        // Blend with existing value (darkest wins for accumulated damage)
                        Color currentColor = craterMask.GetPixel(px, py);
                        float newIntensity = Mathf.Max(currentColor.r, intensity);
                        craterMask.SetPixel(px, py, new Color(newIntensity, newIntensity, newIntensity, 1));
                    }
                }
            }
        }

        craterMask.Apply();
    }

    Vector3 GetRandomPointOnTerrain()
    {
        MeshFilter meshFilter = terrainGen.GetMeshFilter();
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform meshTransform = meshFilter.transform;

        // Get random vertex from top surface only (exclude side vertices if present)
        int mapSize = terrainGen.mapSize;
        int randomIndex = Random.Range(0, mapSize * mapSize);

        Vector3 localPoint = vertices[randomIndex];
        return meshTransform.TransformPoint(localPoint);
    }

    public void ClearAllMarkers()
    {
        foreach (var marker in activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeMarkers.Clear();
    }

    public void ResetCraterMask()
    {
        if (craterMask != null)
        {
            Color[] clearPixels = new Color[textureSize * textureSize];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = Color.black;
            craterMask.SetPixels(clearPixels);
            craterMask.Apply();
        }
    }

    void OnDestroy()
    {
        ClearAllMarkers();
        if (craterMask != null)
            Destroy(craterMask);
    }
}