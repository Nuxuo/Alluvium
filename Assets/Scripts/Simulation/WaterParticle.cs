using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace TerrainErosion
{
    public class WaterParticle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float volume = 1.0f;
        public float sediment = 0.0f;
        public SoilType carriedType;
        public bool isAlive = true;

        private const float minVolume = 0.01f;
        private const float evaporationRate = 0.001f;
        private const float gravity = 4.0f;
        private const float inertia = 0.05f;

        public WaterParticle(LayerMap map)
        {
            position = new Vector2(
                Random.Range(0, map.Width),
                Random.Range(0, map.Height)
            );
            velocity = Vector2.zero;
            carriedType = SoilType.Soil;
        }

        public bool Move(LayerMap map)
        {
            if (volume < minVolume) return false;

            Vector2Int ipos = new Vector2Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y)
            );

            // Get surface normal/gradient
            Vector3 normal = CalculateNormal(map, ipos);

            // Update velocity based on gradient
            Vector2 direction = new Vector2(normal.x, normal.z);
            velocity = velocity * inertia + direction * (1f - inertia) * gravity;

            // Move particle
            position += velocity;

            // Check bounds
            if (position.x < 0 || position.x >= map.Width - 1 ||
                position.y < 0 || position.y >= map.Height - 1)
            {
                isAlive = false;
                return false;
            }

            return true;
        }

        public bool Interact(LayerMap map)
        {
            Vector2Int ipos = new Vector2Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y)
            );

            SoilType surfaceType = map.GetSurfaceType(ipos.x, ipos.y);
            SoilProperties props = SoilProperties.Get(surfaceType);

            // Calculate height difference for erosion capacity
            float heightDiff = map.GetTotalHeight(ipos.x, ipos.y) -
                              GetInterpolatedHeight(map, position);

            // Sediment capacity
            float capacity = Mathf.Max(0f, props.solubility * heightDiff * velocity.magnitude);

            // Erosion or deposition
            float sedimentDiff = capacity - sediment;

            if (sedimentDiff > 0) // Erode
            {
                float toErode = Mathf.Min(sedimentDiff, props.equilibriumRate) * volume;
                float removed = map.RemoveMaterial(ipos.x, ipos.y, toErode);
                sediment += (toErode - removed);
                carriedType = surfaceType;
            }
            else if (sedimentDiff < 0) // Deposit
            {
                float toDeposit = Mathf.Min(-sedimentDiff, props.equilibriumRate) * sediment;
                map.AddMaterial(ipos.x, ipos.y, toDeposit, carriedType);
                sediment -= toDeposit;
            }

            // Evaporation
            volume *= (1f - evaporationRate);
            sediment /= (1f - evaporationRate);
            sediment = Mathf.Min(sediment, 1f);

            return volume > minVolume;
        }

        public void Deposit(LayerMap map)
        {
            Vector2Int ipos = new Vector2Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y)
            );

            if (sediment > 0f)
            {
                map.AddMaterial(ipos.x, ipos.y, sediment * volume, carriedType);
            }
        }

        private Vector3 CalculateNormal(LayerMap map, Vector2Int pos)
        {
            if (pos.x <= 0 || pos.x >= map.Width - 1 ||
                pos.y <= 0 || pos.y >= map.Height - 1)
                return Vector3.up;

            float heightL = map.GetTotalHeight(pos.x - 1, pos.y);
            float heightR = map.GetTotalHeight(pos.x + 1, pos.y);
            float heightD = map.GetTotalHeight(pos.x, pos.y - 1);
            float heightU = map.GetTotalHeight(pos.x, pos.y + 1);

            Vector3 normal = new Vector3(heightL - heightR, 2f, heightD - heightU);
            return normal.normalized;
        }

        private float GetInterpolatedHeight(LayerMap map, Vector2 pos)
        {
            int x0 = Mathf.FloorToInt(pos.x);
            int y0 = Mathf.FloorToInt(pos.y);
            int x1 = Mathf.Min(x0 + 1, map.Width - 1);
            int y1 = Mathf.Min(y0 + 1, map.Height - 1);

            float fx = pos.x - x0;
            float fy = pos.y - y0;

            float h00 = map.GetTotalHeight(x0, y0);
            float h10 = map.GetTotalHeight(x1, y0);
            float h01 = map.GetTotalHeight(x0, y1);
            float h11 = map.GetTotalHeight(x1, y1);

            float h0 = Mathf.Lerp(h00, h10, fx);
            float h1 = Mathf.Lerp(h01, h11, fx);

            return Mathf.Lerp(h0, h1, fy);
        }
    }
}