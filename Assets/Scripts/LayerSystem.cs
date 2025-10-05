using UnityEngine;
using System.Collections.Generic;

// Enum for different soil/material types
public enum SoilType
{
    Rock,
    Dirt,
    Sand,
    Grass,
    Stone,
    Gravel
}

// Represents a single layer in a voxel column (each layer is 0.1 units tall)
[System.Serializable]
public class VoxelLayer
{
    public SoilType soilType;

    public VoxelLayer(SoilType type)
    {
        soilType = type;
    }
}

// Represents all layers in a single voxel column (x, z position)
// Each layer is 0.1 units tall
[System.Serializable]
public class VoxelColumn
{
    public List<VoxelLayer> layers;

    public VoxelColumn()
    {
        layers = new List<VoxelLayer>();
    }

    // Get total height (number of layers * 0.1)
    public float GetTotalHeight()
    {
        return layers.Count * 0.1f;
    }

    // Get the number of layers
    public int GetLayerCount()
    {
        return layers.Count;
    }

    // Add layers of a specific type
    public void AddLayers(SoilType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            layers.Add(new VoxelLayer(type));
        }
    }

    // Get the soil type at a specific layer index (0 = bottom)
    public SoilType GetSoilTypeAtLayer(int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < layers.Count)
        {
            return layers[layerIndex].soilType;
        }
        return SoilType.Rock;
    }

    // Get the soil type at a specific height
    public SoilType GetSoilTypeAtHeight(float height)
    {
        int layerIndex = Mathf.FloorToInt(height / 0.1f);
        return GetSoilTypeAtLayer(layerIndex);
    }
}

// Helper class for soil type colors
public static class SoilTypeColors
{
    public static Color GetColor(SoilType type)
    {
        switch (type)
        {
            case SoilType.Rock:
                return new Color(0.4f, 0.4f, 0.45f); // Gray
            case SoilType.Dirt:
                return new Color(0.4f, 0.25f, 0.15f); // Brown
            case SoilType.Sand:
                return new Color(0.9f, 0.85f, 0.6f); // Sandy yellow
            case SoilType.Grass:
                return new Color(0.2f, 0.6f, 0.2f); // Green
            case SoilType.Stone:
                return new Color(0.5f, 0.5f, 0.5f); // Light gray
            case SoilType.Gravel:
                return new Color(0.45f, 0.45f, 0.4f); // Dark gray
            default:
                return Color.white;
        }
    }
}