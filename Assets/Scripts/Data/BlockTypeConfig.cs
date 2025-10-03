using UnityEngine;

namespace VoxelTerrain.Data
{
    /// <summary>
    /// Height calculation mode for block type rules
    /// </summary>
    public enum HeightMode
    {
        Normalized = 0,  // Use 0-1 normalized height
        VoxelLayers = 1  // Use absolute voxel layer numbers
    }

    /// <summary>
    /// Configuration for a single block type's generation rules
    /// Defines when and how a block type appears in the terrain
    /// </summary>
    [System.Serializable]
    public class BlockTypeConfig
    {
        [Header("Block Identity")]
        public BlockType blockType = BlockType.Dirt;
        public string displayName = "Dirt";
        public Color previewColor = new Color(0.4f, 0.3f, 0.2f);

        [Header("Height Mode")]
        [Tooltip("Use normalized height (0-1) or absolute voxel layers")]
        public HeightMode heightMode = HeightMode.Normalized;

        [Header("Height Rules - Normalized (0-1)")]
        [Tooltip("Minimum normalized height (0-1) where this block can appear")]
        [Range(0f, 1f)]
        public float minHeight = 0f;

        [Tooltip("Maximum normalized height (0-1) where this block can appear")]
        [Range(0f, 1f)]
        public float maxHeight = 1f;

        [Tooltip("How smoothly this block blends at its height boundaries")]
        [Range(0.01f, 0.5f)]
        public float heightBlendAmount = 0.1f;

        [Header("Height Rules - Voxel Layers (Absolute)")]
        [Tooltip("Minimum voxel layer (0 = ground level)")]
        public int minVoxelLayer = 0;

        [Tooltip("Maximum voxel layer")]
        public int maxVoxelLayer = 100;

        [Tooltip("How many layers to blend at boundaries")]
        [Range(1, 20)]
        public int voxelLayerBlend = 5;

        [Header("Slope Rules")]
        [Tooltip("Minimum slope (0-2) where this block appears. 0=flat, 2=very steep")]
        [Range(0f, 2f)]
        public float minSlope = 0f;

        [Tooltip("Maximum slope (0-2) where this block appears")]
        [Range(0f, 2f)]
        public float maxSlope = 2f;

        [Tooltip("How smoothly this block blends at its slope boundaries")]
        [Range(0.01f, 0.5f)]
        public float slopeBlendAmount = 0.1f;

        [Header("Priority & Blending")]
        [Tooltip("Higher priority blocks override lower priority ones in overlap zones")]
        [Range(0, 10)]
        public int priority = 5;

        [Tooltip("Overall strength/influence of this block type (0-1)")]
        [Range(0f, 1f)]
        public float strength = 1f;

        [Header("Advanced")]
        [Tooltip("Use noise to add variation to boundaries")]
        public bool useNoiseVariation = true;

        [Tooltip("Scale of noise variation")]
        [Range(0f, 1f)]
        public float noiseInfluence = 0.3f;

        /// <summary>
        /// Convert to compute shader compatible format
        /// </summary>
        public BlockTypeConfigData ToShaderData()
        {
            return new BlockTypeConfigData
            {
                blockType = (int)blockType,
                heightMode = (int)heightMode,
                minHeight = minHeight,
                maxHeight = maxHeight,
                heightBlendAmount = heightBlendAmount,
                minVoxelLayer = minVoxelLayer,
                maxVoxelLayer = maxVoxelLayer,
                voxelLayerBlend = voxelLayerBlend,
                minSlope = minSlope,
                maxSlope = maxSlope,
                slopeBlendAmount = slopeBlendAmount,
                priority = priority,
                strength = strength,
                useNoiseVariation = useNoiseVariation ? 1 : 0,
                noiseInfluence = noiseInfluence
            };
        }
    }

    /// <summary>
    /// Struct format for passing to compute shader
    /// Must match the struct in BlockTypeAssignment.compute
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BlockTypeConfigData
    {
        public int blockType;
        public int heightMode;
        public float minHeight;
        public float maxHeight;
        public float heightBlendAmount;
        public int minVoxelLayer;
        public int maxVoxelLayer;
        public int voxelLayerBlend;
        public float minSlope;
        public float maxSlope;
        public float slopeBlendAmount;
        public int priority;
        public float strength;
        public int useNoiseVariation;
        public float noiseInfluence;
        public float padding; // Ensure 16-byte alignment
    }
}