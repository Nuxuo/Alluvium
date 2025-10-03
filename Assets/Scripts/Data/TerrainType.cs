namespace VoxelTerrain.Data
{
    /// <summary>
    /// Visual terrain types - affects appearance only
    /// </summary>
    public enum TerrainType
    {
        Grass = 0,      // Green grass - fertile land
        Sand = 1,       // Sandy desert/beach
        Rock = 2,       // Rocky mountain/cliff
        Snow = 3,       // Snow-covered peaks
        Dirt = 4,       // Bare dirt
        Clay = 5,       // Clay deposits
        Gravel = 6,     // Gravel/scree
        Tundra = 7      // Cold grass
    }

    /// <summary>
    /// Resource types - what can be mined/harvested
    /// </summary>
    public enum ResourceType
    {
        None = 0,

        // Ores
        IronOre = 1,
        CopperOre = 2,
        GoldOre = 3,
        CoalDeposit = 4,

        // Stone types
        Stone = 5,
        Granite = 6,
        Limestone = 7,

        // Other resources
        Clay = 8,
        Sand = 9,
        Gravel = 10,

        // Vegetation (for future use)
        Forest = 11,

        // Special
        CrystalDeposit = 12
    }

    /// <summary>
    /// Represents a single terrain cell with visual and resource data
    /// </summary>
    [System.Serializable]
    public struct TerrainCell
    {
        public TerrainType terrainType;
        public ResourceType resourceType;
        public byte resourceDensity; // 0-255, how rich the deposit is

        public bool HasResource => resourceType != ResourceType.None;

        public TerrainCell(TerrainType terrain, ResourceType resource = ResourceType.None, byte density = 0)
        {
            terrainType = terrain;
            resourceType = resource;
            resourceDensity = density;
        }
    }
}