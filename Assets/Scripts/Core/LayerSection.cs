using UnityEngine;

namespace TerrainErosion
{
    public class LayerSection
    {
        public LayerSection next;
        public LayerSection prev;

        public SoilType soilType;
        public float height;
        public float floor;
        public float saturation;

        public LayerSection(float h, SoilType type)
        {
            height = h;
            soilType = type;
            saturation = 0f;
        }

        public void Reset()
        {
            next = null;
            prev = null;
            soilType = SoilType.Bedrock;
            height = 0f;
            floor = 0f;
            saturation = 0f;
        }
    }
}