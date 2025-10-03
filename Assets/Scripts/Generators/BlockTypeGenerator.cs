using UnityEngine;
using VoxelTerrain.Data;
using VoxelTerrain.Utilities;
using System.Collections.Generic;

namespace VoxelTerrain.Generators
{
    /// <summary>
    /// GPU-accelerated dynamic block type generation
    /// Supports flexible, data-driven block type rules
    /// </summary>
    public static class BlockTypeGenerator
    {
        /// <summary>
        /// Generate block types using dynamic configuration system
        /// </summary>
        public static void GenerateBlockTypes(
            TerrainMeshData data,
            ComputeShader blockTypeShader,
            int seed,
            List<BlockTypeConfig> blockConfigs)
        {
            if (blockTypeShader == null)
            {
                return;
            }

            if (blockConfigs == null || blockConfigs.Count == 0)
            {
                return;
            }

            if (data.blockData == null)
            {
                data.blockData = new Storage.BlockData(data.mapSize, data.mapSize);
            }

            // Sort configurations by priority (highest first)
            var sortedConfigs = new List<BlockTypeConfig>(blockConfigs);
            sortedConfigs.Sort((a, b) => b.priority.CompareTo(a.priority));

            // Convert to shader data format
            BlockTypeConfigData[] configData = new BlockTypeConfigData[sortedConfigs.Count];
            for (int i = 0; i < sortedConfigs.Count; i++)
            {
                configData[i] = sortedConfigs[i].ToShaderData();
            }

            // Create buffers
            ComputeBuffer heightMapBuffer = new ComputeBuffer(data.map.Length, sizeof(float));
            ComputeBuffer blockDataBuffer = new ComputeBuffer(data.blockData.Data.Length, sizeof(uint));
            ComputeBuffer configBuffer = new ComputeBuffer(configData.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(BlockTypeConfigData)));

            // Upload data
            heightMapBuffer.SetData(data.map);
            configBuffer.SetData(configData);

            // Set shader parameters
            blockTypeShader.SetBuffer(0, "heightMap", heightMapBuffer);
            blockTypeShader.SetBuffer(0, "blockData", blockDataBuffer);
            blockTypeShader.SetBuffer(0, "blockConfigs", configBuffer);

            blockTypeShader.SetInt("mapSize", data.mapSize);
            blockTypeShader.SetInt("mapSizeWithBorder", data.mapSizeWithBorder);
            blockTypeShader.SetInt("borderSize", data.erosionBrushRadius);
            blockTypeShader.SetFloat("elevationScale", data.elevationScale);

            float cellSize = (data.scale * 2f) / (data.mapSize - 1);
            blockTypeShader.SetFloat("cellSize", cellSize);

            blockTypeShader.SetInt("numBlockTypes", configData.Length);
            blockTypeShader.SetInt("seed", seed);
            blockTypeShader.SetFloat("blendNoiseScale", 0.1f);

            // Dispatch compute shader
            ComputeHelper.Dispatch(blockTypeShader, data.mapSize, data.mapSize, 1, 0);

            // Read results
            blockDataBuffer.GetData(data.blockData.Data);

            // Cleanup
            heightMapBuffer.Release();
            blockDataBuffer.Release();
            configBuffer.Release();
        }
    }
}