using UnityEngine;

namespace TerrainErosion
{
    public class TerrainErosionSystem : MonoBehaviour
    {
        [Header("Terrain Setup")]
        public int width = 256;
        public int height = 256;
        public int seed = 1337;
        public int poolSize = 1000000;

        [Header("Erosion Settings")]
        public int erosionIterations = 100000;
        public int particlesPerFrame = 100;
        public bool autoRunErosion = false;

        [Header("Thermal Weathering")]
        public bool applyThermalWeathering = true;
        public int thermalIterations = 1;

        private LayerMap layerMap;
        private ErosionSimulator erosionSim;
        private int currentIteration = 0;

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            if (autoRunErosion && currentIteration < erosionIterations)
            {
                erosionSim.SimulateStep(particlesPerFrame);
                currentIteration += particlesPerFrame;
            }
        }

        public void Initialize()
        {
            SoilProperties.Initialize();

            layerMap = new LayerMap(width, height, poolSize);
            layerMap.GenerateFromNoise(seed);

            erosionSim = new ErosionSimulator(layerMap);
            erosionSim.applyThermalWeathering = applyThermalWeathering;
            erosionSim.thermalIterations = thermalIterations;

            currentIteration = 0;

            Debug.Log($"Terrain Erosion System Initialized: {width}x{height}");
        }

        public void RunErosion()
        {
            int totalSteps = erosionIterations / particlesPerFrame;

            for (int i = 0; i < totalSteps; i++)
            {
                erosionSim.SimulateStep(particlesPerFrame);

                if (i % 100 == 0)
                {
                    float progress = (float)i / totalSteps * 100f;
                    Debug.Log($"Erosion Progress: {progress:F1}%");
                }
            }

            Debug.Log("Erosion Complete!");
        }

        public TerrainData ExportTerrainData()
        {
            TerrainData data = new TerrainData(width, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    data.heightMap[x, y] = layerMap.GetTotalHeight(x, y);
                    data.surfaceMaterial[x, y] = layerMap.GetSurfaceType(x, y);
                    data.waterDepth[x, y] = layerMap.GetWaterDepth(x, y);
                }
            }

            return data;
        }

        public Texture2D ExportHeightTexture()
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.R16, false);

            float maxHeight = 0f;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    maxHeight = Mathf.Max(maxHeight, layerMap.GetTotalHeight(x, y));

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float h = layerMap.GetTotalHeight(x, y) / maxHeight;
                    texture.SetPixel(x, y, new Color(h, h, h, 1f));
                }
            }

            texture.Apply();
            return texture;
        }

        public Texture2D ExportMaterialTexture()
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.R8, false);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float id = (float)layerMap.GetSurfaceType(x, y) / 10f;
                    texture.SetPixel(x, y, new Color(id, id, id, 1f));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}