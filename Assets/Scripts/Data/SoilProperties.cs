using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace TerrainErosion
{
    [System.Serializable]
    public class SoilProperties
    {
        public string name;
        public float density = 1.0f;
        public float porosity = 0.3f;
        public float solubility = 1.0f;
        public float equilibriumRate = 0.1f;
        public float friction = 0.15f;
        public float maxDiff = 0.01f;
        public float settlingRate = 0.1f;

        private static Dictionary<SoilType, SoilProperties> properties;

        public static void Initialize()
        {
            properties = new Dictionary<SoilType, SoilProperties>
            {
                { SoilType.Bedrock, new SoilProperties
                    { name = "Bedrock", density = 2.7f, porosity = 0.01f, solubility = 0.1f,
                      equilibriumRate = 0.01f, friction = 0.3f, maxDiff = 0.1f, settlingRate = 0.01f } },

                { SoilType.Rock, new SoilProperties
                    { name = "Rock", density = 2.5f, porosity = 0.1f, solubility = 0.5f,
                      equilibriumRate = 0.05f, friction = 0.25f, maxDiff = 0.05f, settlingRate = 0.05f } },

                { SoilType.Gravel, new SoilProperties
                    { name = "Gravel", density = 2.0f, porosity = 0.3f, solubility = 0.8f,
                      equilibriumRate = 0.1f, friction = 0.2f, maxDiff = 0.02f, settlingRate = 0.1f } },

                { SoilType.Sand, new SoilProperties
                    { name = "Sand", density = 1.6f, porosity = 0.4f, solubility = 1.0f,
                      equilibriumRate = 0.2f, friction = 0.15f, maxDiff = 0.01f, settlingRate = 0.2f } },

                { SoilType.Soil, new SoilProperties
                    { name = "Soil", density = 1.3f, porosity = 0.5f, solubility = 1.0f,
                      equilibriumRate = 0.15f, friction = 0.1f, maxDiff = 0.01f, settlingRate = 0.15f } },

                { SoilType.Clay, new SoilProperties
                    { name = "Clay", density = 1.5f, porosity = 0.45f, solubility = 0.9f,
                      equilibriumRate = 0.1f, friction = 0.12f, maxDiff = 0.015f, settlingRate = 0.12f } }
            };
        }

        public static SoilProperties Get(SoilType type)
        {
            if (properties == null) Initialize();
            return properties.ContainsKey(type) ? properties[type] : properties[SoilType.Bedrock];
        }
    }
}