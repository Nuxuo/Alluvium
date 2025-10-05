using UnityEngine;
using System.Collections.Generic;

namespace TerrainErosion
{
    public class ErosionSimulator
    {
        private LayerMap layerMap;
        private List<WaterParticle> activeParticles;

        public int maxParticles = 100;
        public bool applyThermalWeathering = true;
        public int thermalIterations = 1;

        public ErosionSimulator(LayerMap map)
        {
            layerMap = map;
            activeParticles = new List<WaterParticle>();
        }

        public void SimulateStep(int particleCount)
        {
            // Spawn new particles
            for (int i = 0; i < particleCount && activeParticles.Count < maxParticles; i++)
            {
                WaterParticle particle = new WaterParticle(layerMap);
                activeParticles.Add(particle);
            }

            // Update existing particles
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                WaterParticle particle = activeParticles[i];

                bool moved = particle.Move(layerMap);
                bool interacted = moved && particle.Interact(layerMap);

                if (!interacted || !particle.isAlive)
                {
                    particle.Deposit(layerMap);
                    activeParticles.RemoveAt(i);
                }
            }

            // Apply thermal weathering
            if (applyThermalWeathering)
            {
                ApplyThermalWeatheringToMap();
            }
        }

        private void ApplyThermalWeatheringToMap()
        {
            // Sample random positions for thermal weathering
            int samples = Mathf.Min(1000, layerMap.Width * layerMap.Height / 10);

            for (int i = 0; i < samples; i++)
            {
                int x = Random.Range(0, layerMap.Width);
                int y = Random.Range(0, layerMap.Height);

                ThermalWeathering.ApplyCascade(layerMap, new Vector2Int(x, y), thermalIterations);
            }
        }

        public void Clear()
        {
            activeParticles.Clear();
        }
    }
}