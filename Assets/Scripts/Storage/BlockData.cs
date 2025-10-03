using UnityEngine;
using VoxelTerrain.Data;

namespace VoxelTerrain.Storage
{
    /// <summary>
    /// Manages voxel block data storage and retrieval
    /// Each voxel stores its block type in a compact uint format
    /// </summary>
    public class BlockData
    {
        // Block data storage (first byte = block type, remaining bytes for future use)
        private uint[] blockData;
        private int width;
        private int height;

        public int Width => width;
        public int Height => height;
        public uint[] Data => blockData;

        public BlockData(int width, int height)
        {
            this.width = width;
            this.height = height;
            blockData = new uint[width * height];
        }

        /// <summary>
        /// Get block type at position
        /// </summary>
        public BlockType GetBlockType(int x, int z)
        {
            if (x < 0 || x >= width || z < 0 || z >= height)
                return BlockType.Dirt;

            int index = z * width + x;
            uint data = blockData[index];
            byte blockType = (byte)(data & 0xFF); // First byte

            return (BlockType)blockType;
        }

        /// <summary>
        /// Set block type at position
        /// </summary>
        public void SetBlockType(int x, int z, BlockType type)
        {
            if (x < 0 || x >= width || z < 0 || z >= height)
                return;

            int index = z * width + x;
            // Keep other bytes, only update first byte
            blockData[index] = (blockData[index] & 0xFFFFFF00) | (byte)type;
        }

        /// <summary>
        /// Get raw data at position (all 4 bytes)
        /// </summary>
        public uint GetRawData(int x, int z)
        {
            if (x < 0 || x >= width || z < 0 || z >= height)
                return 0;

            return blockData[z * width + x];
        }

        /// <summary>
        /// Set raw data at position
        /// </summary>
        public void SetRawData(int x, int z, uint data)
        {
            if (x < 0 || x >= width || z < 0 || z >= height)
                return;

            blockData[z * width + x] = data;
        }

        /// <summary>
        /// Clear all block data
        /// </summary>
        public void Clear()
        {
            System.Array.Clear(blockData, 0, blockData.Length);
        }

        /// <summary>
        /// Create a compute buffer from this data
        /// </summary>
        public ComputeBuffer CreateComputeBuffer()
        {
            ComputeBuffer buffer = new ComputeBuffer(blockData.Length, sizeof(uint));
            buffer.SetData(blockData);
            return buffer;
        }

        /// <summary>
        /// Update from compute buffer
        /// </summary>
        public void UpdateFromBuffer(ComputeBuffer buffer)
        {
            if (buffer.count != blockData.Length)
            {
                return;
            }

            buffer.GetData(blockData);
        }
    }
}