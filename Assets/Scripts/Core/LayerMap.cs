using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace TerrainErosion
{
    public class LayerMap
    {
        private LayerSection[,] topSections;
        private SectionPool pool;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public LayerMap(int width, int height, int poolSize)
        {
            Width = width;
            Height = height;
            pool = new SectionPool(poolSize);
            topSections = new LayerSection[width, height];
        }

        public void GenerateFromNoise(int seed)
        {
            FastNoiseLite noise = new FastNoiseLite(seed);
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(6);
            noise.SetFrequency(0.01f);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float noiseValue = noise.GetNoise(x, y);
                    float baseHeight = (noiseValue + 1f) * 0.5f; // Normalize to 0-1

                    // Create bedrock layer
                    float bedrockHeight = baseHeight * 50f;
                    AddMaterial(x, y, bedrockHeight, SoilType.Bedrock);

                    // Add soil on top
                    float soilHeight = baseHeight * 5f;
                    AddMaterial(x, y, soilHeight, SoilType.Soil);
                }
            }
        }

        public float GetTotalHeight(int x, int y)
        {
            if (!IsValid(x, y)) return 0f;
            LayerSection section = topSections[x, y];
            return section != null ? section.floor + section.height : 0f;
        }

        public SoilType GetSurfaceType(int x, int y)
        {
            if (!IsValid(x, y)) return SoilType.Bedrock;
            LayerSection section = topSections[x, y];
            return section != null ? section.soilType : SoilType.Bedrock;
        }

        public float GetWaterDepth(int x, int y)
        {
            if (!IsValid(x, y)) return 0f;
            LayerSection section = topSections[x, y];
            if (section == null) return 0f;

            SoilProperties props = SoilProperties.Get(section.soilType);
            return section.height * section.saturation * props.porosity;
        }

        public void AddMaterial(int x, int y, float amount, SoilType type)
        {
            if (!IsValid(x, y) || amount <= 0f) return;

            LayerSection current = topSections[x, y];

            // Empty position
            if (current == null)
            {
                topSections[x, y] = pool.Get(amount, type);
                return;
            }

            // Same type - merge
            if (current.soilType == type)
            {
                current.height += amount;
                return;
            }

            // Different type - add new layer
            LayerSection newSection = pool.Get(amount, type);
            newSection.prev = current;
            newSection.floor = current.floor + current.height;
            current.next = newSection;
            topSections[x, y] = newSection;
        }

        public LayerSection GetTopSection(int x, int y)
        {
            if (!IsValid(x, y)) return null;
            return topSections[x, y];
        }

        public float RemoveMaterial(int x, int y, float amount)
        {
            if (!IsValid(x, y) || amount <= 0f) return 0f;

            LayerSection current = topSections[x, y];
            if (current == null) return 0f;

            // Remove zero-height sections
            while (current != null && current.height <= 0f)
            {
                LayerSection toRemove = current;
                topSections[x, y] = current.prev;
                if (current.prev != null)
                    current.prev.next = null;
                pool.Release(toRemove);
                current = topSections[x, y];
            }

            if (current == null || amount <= 0f) return 0f;

            float removed = Mathf.Min(amount, current.height);
            current.height -= removed;
            float remaining = amount - removed;

            // Section depleted
            if (current.height <= 0f)
            {
                LayerSection toRemove = current;
                topSections[x, y] = current.prev;
                if (current.prev != null)
                    current.prev.next = null;
                pool.Release(toRemove);

                // Recursively remove remaining amount
                if (remaining > 0f)
                    return RemoveMaterial(x, y, remaining);
            }

            return remaining;
        }

        private bool IsValid(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
    }
}