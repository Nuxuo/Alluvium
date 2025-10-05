using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace TerrainErosion
{
    public class TerrainData
    {
        public int width;
        public int height;

        public float[,] heightMap;
        public SoilType[,] surfaceMaterial;
        public float[,] waterDepth;
        public float[,] groundwaterSaturation;

        public TerrainData(int w, int h)
        {
            width = w;
            height = h;
            heightMap = new float[w, h];
            surfaceMaterial = new SoilType[w, h];
            waterDepth = new float[w, h];
            groundwaterSaturation = new float[w, h];
        }
    }
}