using UnityEngine;
using System.Collections.Generic;
using VoxelTerrain.Data;
using VoxelTerrain.Interfaces;

namespace VoxelTerrain.Generators
{
    /// <summary>
    /// Generates voxel-style terrain meshes from terrain data
    /// Now uses TerrainType instead of BlockType
    /// </summary>
    public class MeshGenerator : IMeshGenerator
    {
        public Mesh GenerateMesh(TerrainMeshData data)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            float cellSize = (data.scale * 2f) / (data.mapSize - 1);
            float actualVoxelSize = cellSize * data.voxelSize;

            // Create height grid
            float[,] heightGrid = new float[data.mapSize, data.mapSize];

            for (int z = 0; z < data.mapSize; z++)
            {
                for (int x = 0; x < data.mapSize; x++)
                {
                    int borderedMapIndex = (z + data.erosionBrushRadius) * data.mapSizeWithBorder + x + data.erosionBrushRadius;
                    float normalizedHeight = data.map[borderedMapIndex];
                    float worldHeight = normalizedHeight * data.elevationScale;

                    // Snap to voxel grid
                    int voxelLayers = Mathf.Max(1, Mathf.RoundToInt(worldHeight / actualVoxelSize));
                    heightGrid[x, z] = voxelLayers * actualVoxelSize;
                }
            }

            // Generate voxel mesh
            for (int z = 0; z < data.mapSize; z++)
            {
                for (int x = 0; x < data.mapSize; x++)
                {
                    Vector2 percent = new Vector2(x / (data.mapSize - 1f), z / (data.mapSize - 1f));
                    Vector3 basePos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * data.scale;
                    float height = heightGrid[x, z];

                    // Get terrain type from block data (stored as uint)
                    uint terrainTypeData = data.blockData.Data[z * data.mapSize + x];
                    TerrainType terrainType = (TerrainType)terrainTypeData;

                    Color terrainColor = GetTerrainColor(terrainType);

                    // Create top face
                    float halfCell = cellSize * 0.5f;
                    int startIdx = verts.Count;

                    verts.Add(basePos + new Vector3(-halfCell, height, -halfCell));
                    verts.Add(basePos + new Vector3(-halfCell, height, halfCell));
                    verts.Add(basePos + new Vector3(halfCell, height, halfCell));
                    verts.Add(basePos + new Vector3(halfCell, height, -halfCell));

                    float uvValue = height / data.elevationScale;
                    for (int i = 0; i < 4; i++)
                    {
                        uvs.Add(new Vector2(uvValue, uvValue));
                        colors.Add(terrainColor);
                    }

                    triangles.AddRange(new int[] {
                        startIdx, startIdx + 1, startIdx + 2,
                        startIdx, startIdx + 2, startIdx + 3
                    });

                    // Generate vertical sides
                    GenerateVerticalSides(x, z, data.mapSize, heightGrid, basePos, height, halfCell,
                                        uvValue, terrainColor, verts, triangles, uvs, colors);

                    // Add bottom faces at edges
                    GenerateEdgeFaces(x, z, data.mapSize, basePos, height, halfCell,
                                    uvValue, terrainColor, verts, triangles, uvs, colors);
                }
            }

            // Add skirt
            if (data.generateVoxelSkirt)
            {
                AddConnectedSkirt(verts, triangles, uvs, colors, heightGrid, data.blockData, cellSize, data);
            }

            Mesh mesh = new Mesh();
            mesh.name = "Voxel Terrain Mesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private Color GetTerrainColor(TerrainType type)
        {
            // Encode terrain type in vertex color for shader
            // Using R channel to store type index (0-7)
            float typeIndex = (float)type / 10f; // Simple encoding

            return new Color(typeIndex, 0, 0, 1);
        }

        private void GenerateVerticalSides(int x, int z, int mapSize, float[,] heightGrid,
                                          Vector3 basePos, float height, float halfCell,
                                          float uvValue, Color terrainColor,
                                          List<Vector3> verts, List<int> triangles,
                                          List<Vector2> uvs, List<Color> colors)
        {
            // Check right neighbor (positive X)
            if (x < mapSize - 1)
            {
                float neighborHeight = heightGrid[x + 1, z];
                if (height > neighborHeight)
                {
                    AddVerticalQuad(verts, triangles, uvs, colors,
                        basePos + new Vector3(halfCell, neighborHeight, -halfCell),
                        basePos + new Vector3(halfCell, neighborHeight, halfCell),
                        basePos + new Vector3(halfCell, height, halfCell),
                        basePos + new Vector3(halfCell, height, -halfCell),
                        uvValue, terrainColor);
                }
            }

            // Check forward neighbor (positive Z)
            if (z < mapSize - 1)
            {
                float neighborHeight = heightGrid[x, z + 1];
                if (height > neighborHeight)
                {
                    AddVerticalQuad(verts, triangles, uvs, colors,
                        basePos + new Vector3(halfCell, neighborHeight, halfCell),
                        basePos + new Vector3(-halfCell, neighborHeight, halfCell),
                        basePos + new Vector3(-halfCell, height, halfCell),
                        basePos + new Vector3(halfCell, height, halfCell),
                        uvValue, terrainColor);
                }
            }

            // Check left neighbor (negative X)
            if (x > 0)
            {
                float neighborHeight = heightGrid[x - 1, z];
                if (height > neighborHeight)
                {
                    AddVerticalQuad(verts, triangles, uvs, colors,
                        basePos + new Vector3(-halfCell, neighborHeight, halfCell),
                        basePos + new Vector3(-halfCell, neighborHeight, -halfCell),
                        basePos + new Vector3(-halfCell, height, -halfCell),
                        basePos + new Vector3(-halfCell, height, halfCell),
                        uvValue, terrainColor);
                }
            }

            // Check back neighbor (negative Z)
            if (z > 0)
            {
                float neighborHeight = heightGrid[x, z - 1];
                if (height > neighborHeight)
                {
                    AddVerticalQuad(verts, triangles, uvs, colors,
                        basePos + new Vector3(-halfCell, neighborHeight, -halfCell),
                        basePos + new Vector3(halfCell, neighborHeight, -halfCell),
                        basePos + new Vector3(halfCell, height, -halfCell),
                        basePos + new Vector3(-halfCell, height, -halfCell),
                        uvValue, terrainColor);
                }
            }
        }

        private void GenerateEdgeFaces(int x, int z, int mapSize, Vector3 basePos,
                                      float height, float halfCell, float uvValue,
                                      Color terrainColor, List<Vector3> verts, List<int> triangles,
                                      List<Vector2> uvs, List<Color> colors)
        {
            if (x == 0)
            {
                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(-halfCell, 0, halfCell),
                    basePos + new Vector3(-halfCell, 0, -halfCell),
                    basePos + new Vector3(-halfCell, height, -halfCell),
                    basePos + new Vector3(-halfCell, height, halfCell),
                    uvValue, terrainColor);
            }
            if (x == mapSize - 1)
            {
                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(halfCell, 0, -halfCell),
                    basePos + new Vector3(halfCell, 0, halfCell),
                    basePos + new Vector3(halfCell, height, halfCell),
                    basePos + new Vector3(halfCell, height, -halfCell),
                    uvValue, terrainColor);
            }
            if (z == 0)
            {
                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(halfCell, 0, -halfCell),
                    basePos + new Vector3(-halfCell, 0, -halfCell),
                    basePos + new Vector3(-halfCell, height, -halfCell),
                    basePos + new Vector3(halfCell, height, -halfCell),
                    uvValue, terrainColor);
            }
            if (z == mapSize - 1)
            {
                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(-halfCell, 0, halfCell),
                    basePos + new Vector3(halfCell, 0, halfCell),
                    basePos + new Vector3(halfCell, height, halfCell),
                    basePos + new Vector3(-halfCell, height, halfCell),
                    uvValue, terrainColor);
            }
        }

        private void AddVerticalQuad(List<Vector3> verts, List<int> triangles, List<Vector2> uvs, List<Color> colors,
                                      Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, float uvValue, Color terrainColor)
        {
            int startIdx = verts.Count;
            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            Vector2 uv = new Vector2(uvValue, uvValue);
            for (int i = 0; i < 4; i++)
            {
                uvs.Add(uv);
                colors.Add(terrainColor);
            }

            triangles.AddRange(new int[] {
                startIdx, startIdx + 2, startIdx + 1,
                startIdx, startIdx + 3, startIdx + 2
            });
        }

        private void AddConnectedSkirt(List<Vector3> verts, List<int> triangles, List<Vector2> uvs, List<Color> colors,
                                        float[,] heightGrid, Storage.BlockData blockData, float cellSize, TerrainMeshData data)
        {
            float halfCell = cellSize * 0.5f;
            float skirtY = -data.skirtHeight;

            // Bottom edge (z = 0)
            for (int x = 0; x < data.mapSize; x++)
            {
                Vector2 percent = new Vector2(x / (data.mapSize - 1f), 0);
                Vector3 basePos = new Vector3(percent.x * 2 - 1, 0, -1) * data.scale;
                float height = heightGrid[x, 0];

                uint terrainTypeData = blockData.Data[x];
                TerrainType terrainType = (TerrainType)terrainTypeData;
                Color terrainColor = GetTerrainColor(terrainType);

                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(halfCell, skirtY, -halfCell),
                    basePos + new Vector3(-halfCell, skirtY, -halfCell),
                    basePos + new Vector3(-halfCell, height, -halfCell),
                    basePos + new Vector3(halfCell, height, -halfCell),
                    0, terrainColor);

                if (x < data.mapSize - 1)
                {
                    Vector3 nextPos = new Vector3((x + 1) / (data.mapSize - 1f) * 2 - 1, 0, -1) * data.scale;
                    AddVerticalQuad(verts, triangles, uvs, colors,
                        basePos + new Vector3(halfCell, skirtY, -halfCell),
                        nextPos + new Vector3(-halfCell, skirtY, -halfCell),
                        nextPos + new Vector3(-halfCell, skirtY, halfCell),
                        basePos + new Vector3(halfCell, skirtY, halfCell),
                        0, terrainColor);
                }
            }

            // Top edge (z = mapSize - 1)
            for (int x = 0; x < data.mapSize; x++)
            {
                Vector2 percent = new Vector2(x / (data.mapSize - 1f), 1);
                Vector3 basePos = new Vector3(percent.x * 2 - 1, 0, 1) * data.scale;
                float height = heightGrid[x, data.mapSize - 1];

                uint terrainTypeData = blockData.Data[(data.mapSize - 1) * data.mapSize + x];
                TerrainType terrainType = (TerrainType)terrainTypeData;
                Color terrainColor = GetTerrainColor(terrainType);

                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(-halfCell, skirtY, halfCell),
                    basePos + new Vector3(halfCell, skirtY, halfCell),
                    basePos + new Vector3(halfCell, height, halfCell),
                    basePos + new Vector3(-halfCell, height, halfCell),
                    0, terrainColor);
            }

            // Left edge (x = 0)
            for (int z = 0; z < data.mapSize; z++)
            {
                Vector2 percent = new Vector2(0, z / (data.mapSize - 1f));
                Vector3 basePos = new Vector3(-1, 0, percent.y * 2 - 1) * data.scale;
                float height = heightGrid[0, z];

                uint terrainTypeData = blockData.Data[z * data.mapSize];
                TerrainType terrainType = (TerrainType)terrainTypeData;
                Color terrainColor = GetTerrainColor(terrainType);

                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(-halfCell, skirtY, halfCell),
                    basePos + new Vector3(-halfCell, skirtY, -halfCell),
                    basePos + new Vector3(-halfCell, height, -halfCell),
                    basePos + new Vector3(-halfCell, height, halfCell),
                    0, terrainColor);
            }

            // Right edge (x = mapSize - 1)
            for (int z = 0; z < data.mapSize; z++)
            {
                Vector2 percent = new Vector2(1, z / (data.mapSize - 1f));
                Vector3 basePos = new Vector3(1, 0, percent.y * 2 - 1) * data.scale;
                float height = heightGrid[data.mapSize - 1, z];

                uint terrainTypeData = blockData.Data[z * data.mapSize + (data.mapSize - 1)];
                TerrainType terrainType = (TerrainType)terrainTypeData;
                Color terrainColor = GetTerrainColor(terrainType);

                AddVerticalQuad(verts, triangles, uvs, colors,
                    basePos + new Vector3(halfCell, skirtY, -halfCell),
                    basePos + new Vector3(halfCell, skirtY, halfCell),
                    basePos + new Vector3(halfCell, height, halfCell),
                    basePos + new Vector3(halfCell, height, -halfCell),
                    0, terrainColor);
            }
        }
    }
}