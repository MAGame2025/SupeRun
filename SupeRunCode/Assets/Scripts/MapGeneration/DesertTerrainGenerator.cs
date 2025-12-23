using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class DesertTerrainGenerator : MonoBehaviour
{
    [Header("Terrain Size")]
    [SerializeField] private int heightmapResolution = 513;
    [SerializeField] private int terrainSize = 600;
    [SerializeField] private float terrainHeight = 80f;

    [Header("Perlin Noise (Dunes)")]
    [SerializeField] private float baseScale = 0.015f; // lower = larger dunes
    [SerializeField] private int octaves = 4;
    [SerializeField] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2.0f;

    [Header("Random")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomSeedOnStart = true;

    private void Start()
    {
        if (randomSeedOnStart)
            seed = Random.Range(int.MinValue, int.MaxValue);

        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        var terrain = GetComponent<Terrain>();
        var data = terrain.terrainData;

        data.heightmapResolution = heightmapResolution;
        data.size = new Vector3(terrainSize, terrainHeight, terrainSize);

        float[,] heights = new float[heightmapResolution, heightmapResolution];

        var prng = new System.Random(seed);
        float offsetX = prng.Next(-100000, 100000);
        float offsetZ = prng.Next(-100000, 100000);

        for (int z = 0; z < heightmapResolution; z++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                float nx = (float)x / (heightmapResolution - 1);
                float nz = (float)z / (heightmapResolution - 1);

                float noiseValue = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxPossible = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (nx * terrainSize * baseScale * frequency) + offsetX;
                    float sampleZ = (nz * terrainSize * baseScale * frequency) + offsetZ;

                    float p = Mathf.PerlinNoise(sampleX, sampleZ);
                    noiseValue += p * amplitude;

                    maxPossible += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                noiseValue /= maxPossible;

                // Make it more "dune-ish": flatten lows a bit and keep crests
                float dune = Mathf.Pow(noiseValue, 1.6f);

                heights[z, x] = dune; // 0..1
            }
        }

        data.SetHeights(0, 0, heights);

        // Make sure TerrainCollider matches
        var col = terrain.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = data;
    }
}
