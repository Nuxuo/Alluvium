using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace TerrainErosion
{
    public class ThermalWeathering
    {
        private static readonly Vector2Int[] neighbors = new Vector2Int[]
        {
            new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
            new Vector2Int(0, -1),                          new Vector2Int(0, 1),
            new Vector2Int(1, -1),  new Vector2Int(1, 0),  new Vector2Int(1, 1)
        };

        public static void ApplyCascade(LayerMap map, Vector2Int pos, int iterations = 1)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                Cascade(map, pos);
            }
        }

        private static void Cascade(LayerMap map, Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= map.Width || pos.y < 0 || pos.y >= map.Height)
                return;

            float currentHeight = map.GetTotalHeight(pos.x, pos.y);
            SoilType surfaceType = map.GetSurfaceType(pos.x, pos.y);
            SoilProperties props = SoilProperties.Get(surfaceType);

            // Find steepest neighbor
            foreach (Vector2Int offset in neighbors)
            {
                Vector2Int neighborPos = pos + offset;

                if (neighborPos.x < 0 || neighborPos.x >= map.Width ||
                    neighborPos.y < 0 || neighborPos.y >= map.Height)
                    continue;

                float neighborHeight = map.GetTotalHeight(neighborPos.x, neighborPos.y);
                float heightDiff = currentHeight - neighborHeight;

                if (heightDiff <= 0) continue;

                // Check if slope exceeds stable angle
                float excess = heightDiff - props.maxDiff;
                if (excess <= 0) continue;

                // Transfer material
                float transfer = props.settlingRate * excess * 0.5f;

                // Limit transfer to available material
                LayerSection topSection = map.GetTopSection(pos.x, pos.y);
                if (topSection != null && transfer > topSection.height)
                    transfer = topSection.height;

                // Remove from source
                float remaining = map.RemoveMaterial(pos.x, pos.y, transfer);

                // Add to destination
                map.AddMaterial(neighborPos.x, neighborPos.y, transfer - remaining, surfaceType);
            }
        }

        public static void ApplyToMap(LayerMap map, int iterations = 1)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    for (int y = 0; y < map.Height; y++)
                    {
                        Cascade(map, new Vector2Int(x, y));
                    }
                }
            }
        }
    }
}