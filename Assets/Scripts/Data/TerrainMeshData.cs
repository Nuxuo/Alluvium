namespace VoxelTerrain.Data
{
    /// <summary>
    /// Data transfer object containing all information needed to generate a terrain mesh
    /// </summary>
    public class TerrainMeshData
    {
        // Height map data
        public float[] map;
        public int mapSize;
        public int mapSizeWithBorder;
        public int erosionBrushRadius;

        // World space parameters
        public float scale;
        public float elevationScale;
        public float skirtHeight;

        // Voxel-specific settings
        public float voxelSize;
        public bool generateVoxelSkirt;

        // Block type thresholds (normalized 0-1)
        public float sandHeightThreshold = 0.3f;  // Below this = sand
        public float snowHeightThreshold = 0.75f; // Above this = snow
    }
}