using UnityEngine;
using VoxelTerrain.Data;

namespace VoxelTerrain.Interfaces
{
    /// <summary>
    /// Interface for terrain mesh generation strategies.
    /// Implementations can create different mesh styles (voxel, smooth, etc.)
    /// </summary>
    public interface IMeshGenerator
    {
        /// <summary>
        /// Generates a mesh from the provided terrain data
        /// </summary>
        /// <param name="data">Complete terrain generation parameters</param>
        /// <returns>Generated Unity mesh</returns>
        Mesh GenerateMesh(TerrainMeshData data);
    }
}