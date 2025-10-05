// C# wrapper - copy the FastNoiseLite.cs from the provided header file
// Or use the Unity Package: https://github.com/Auburn/FastNoiseLite
// The C# implementation is too large to include here completely.
// Use the provided C++ header and convert to C#, or download from the repo.

// For quick implementation, here's a simplified noise wrapper:
using UnityEngine;

public class FastNoiseLite
{
    public enum NoiseType { OpenSimplex2, Perlin, Value }
    public enum FractalType { None, FBm, Ridged }

    private int seed;
    private float frequency = 0.01f;
    private int octaves = 3;

    public FastNoiseLite(int seed = 1337)
    {
        this.seed = seed;
    }

    public void SetNoiseType(NoiseType type) { }
    public void SetFractalType(FractalType type) { }
    public void SetFractalOctaves(int octaves) { this.octaves = octaves; }
    public void SetFrequency(float freq) { this.frequency = freq; }

    public float GetNoise(float x, float y)
    {
        // Use Unity's built-in Perlin as fallback
        return Mathf.PerlinNoise(x * frequency + seed, y * frequency + seed) * 2f - 1f;
    }
}