using System.Collections.Generic;
using UnityEngine;

public class PerlinDesertLevelGenerator : MonoBehaviour
{
    [Header("Terrain Ref")]
    [SerializeField] private Terrain terrain;

    [Header("Terrain Size")]
    [Tooltip("World size (X and Z).")]
    [SerializeField] private int terrainSize = 600;

    [Tooltip("Max terrain height (Y).")]
    [SerializeField] private float terrainHeight = 80f;

    [Tooltip("Heightmap resolution. Must be 2^n + 1 (e.g. 257, 513, 1025).")]
    [SerializeField] private int heightmapResolution = 513;

    [Header("Perlin Noise (Dunes)")]
    [Tooltip("Lower = bigger dunes. Good: 0.01 - 0.03")]
    [SerializeField] private float baseScale = 0.015f;

    [SerializeField] private int octaves = 4;
    [SerializeField, Range(0.1f, 0.99f)] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2.0f;

    [Tooltip("Makes dunes more 'dune-ish'. 1 = normal. 1.4 - 2.2 = more dunes.")]
    [SerializeField] private float dunePower = 1.6f;

    [Header("Seed")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomSeedOnStart = true;

    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private float playerSpawnYOffset = 2.0f;
    [Tooltip("Avoid spawning on steep slopes. Degrees.")]
    [SerializeField] private float maxPlayerSlope = 25f;

    [Header("Content Prefabs")]
    [SerializeField] private GameObject[] treePrefabs;          // optional
    [SerializeField] private GameObject[] structurePrefabs;     // rocks, pillars, buildings
    [SerializeField] private GameObject[] interactablePrefabs;  // chests, pickups, etc.

    [Header("Content Counts")]
    [SerializeField] private int treeCount = 80;
    [SerializeField] private int structureCount = 35;
    [SerializeField] private int interactableCount = 15;

    [Header("Placement Rules")]
    [Tooltip("Min distance between placed items (meters).")]
    [SerializeField] private float minItemSpacing = 10f;

    [Tooltip("Don’t place props on very steep slopes. Degrees.")]
    [SerializeField] private float maxPropSlope = 35f;

    [Header("Boundary Walls (Optional)")]
    [SerializeField] private bool generateBoundaryWalls = false;
    [SerializeField] private GameObject wallPrefab;
    [Tooltip("Distance from terrain edge.")]
    [SerializeField] private float wallInset = 5f;
    [Tooltip("How many wall segments per side.")]
    [SerializeField] private int wallSegmentsPerSide = 40;

    private GameObject spawnedPlayer;
    private readonly List<Vector3> placedPoints = new List<Vector3>();

    private void Start()
    {
        if (terrain == null)
        {
            Debug.LogError("[LevelGenerator] Terrain reference is missing.");
            return;
        }

        if (randomSeedOnStart)
            seed = Random.Range(int.MinValue, int.MaxValue);

        GenerateTerrain();
        PlaceAllContent();
        SpawnPlayerAndBindSystems();

        if (generateBoundaryWalls && wallPrefab != null)
            CreateBoundaryWalls();
    }

    private void GenerateTerrain()
    {
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

                    float p = Mathf.PerlinNoise(sampleX, sampleZ); // 0..1
                    noiseValue += p * amplitude;

                    maxPossible += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                noiseValue /= Mathf.Max(0.0001f, maxPossible);

                // Make dunes: push lows down, keep crests
                float dune = Mathf.Pow(noiseValue, Mathf.Max(0.01f, dunePower));

                heights[z, x] = Mathf.Clamp01(dune);
            }
        }

        data.SetHeights(0, 0, heights);

        // Make sure collider matches
        var col = terrain.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = data;

        Debug.Log($"[LevelGenerator] Generated terrain. Seed={seed}");
    }

    private void PlaceAllContent()
    {
        placedPoints.Clear();

        // Structures first (big anchors)
        SpawnMany(structurePrefabs, structureCount);

        // Trees next
        SpawnMany(treePrefabs, treeCount);

        // Interactables last (chests etc.)
        SpawnMany(interactablePrefabs, interactableCount);
    }

    private void SpawnMany(GameObject[] prefabs, int count)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0)
            return;

        int placed = 0;
        int attempts = 0;

        // Prevent infinite loops if spacing is too strict
        int maxAttempts = Mathf.Max(2000, count * 200);

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;

            Vector3 pos;
            float slope;
            if (!TryGetRandomTerrainPoint(out pos, out slope))
                continue;

            if (slope > maxPropSlope)
                continue;

            if (!IsFarEnough(pos, minItemSpacing))
                continue;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

            // Random Y rotation
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Instantiate(prefab, pos, rot, transform);

            placedPoints.Add(pos);
            placed++;
        }

        Debug.Log($"[LevelGenerator] Placed {placed}/{count} of {(prefabs != null ? prefabs.Length : 0)} prefabs.");
    }

    private bool TryGetRandomTerrainPoint(out Vector3 worldPos, out float slopeDegrees)
    {
        // Pick random XZ within terrain bounds (world space)
        Vector3 tPos = terrain.transform.position;
        float x = tPos.x + Random.Range(0f, terrainSize);
        float z = tPos.z + Random.Range(0f, terrainSize);

        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + tPos.y;

        // Slope:
        Vector3 n = terrain.terrainData.GetInterpolatedNormal(
            (x - tPos.x) / terrainSize,
            (z - tPos.z) / terrainSize
        );
        slopeDegrees = Vector3.Angle(n, Vector3.up);

        worldPos = new Vector3(x, y, z);
        return true;
    }

    private bool IsFarEnough(Vector3 p, float minDist)
    {
        float sq = minDist * minDist;
        for (int i = 0; i < placedPoints.Count; i++)
        {
            if ((placedPoints[i] - p).sqrMagnitude < sq)
                return false;
        }
        return true;
    }

    private void SpawnPlayerAndBindSystems()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("[LevelGenerator] No playerPrefab assigned.");
            return;
        }

        // Try a bunch of times to find a not-too-steep spot
        for (int i = 0; i < 300; i++)
        {
            Vector3 p;
            float slope;
            if (!TryGetRandomTerrainPoint(out p, out slope))
                continue;

            if (slope > maxPlayerSlope)
                continue;

            p.y += playerSpawnYOffset;

            spawnedPlayer = Instantiate(playerPrefab, p, Quaternion.identity);
            Debug.Log($"[LevelGenerator] Spawned player at {p} (slope={slope:0.0})");

            // Bind XP manager
            if (SRXpManager.Instance != null)
                SRXpManager.Instance.RegisterPlayer(spawnedPlayer.transform);

            // Bind enemy manager
            var enemyManager = SREnemyManager.Instance;
            if (enemyManager != null)
                enemyManager.SetPlayer(spawnedPlayer.transform);
            else
                Debug.LogWarning("[LevelGenerator] No SREnemyManager instance found to bind player to.");

            return;
        }

        Debug.LogWarning("[LevelGenerator] Failed to find a good player spawn point (too steep?).");
    }

    private void CreateBoundaryWalls()
    {
        Transform wallsParent = new GameObject("BoundaryWalls").transform;
        wallsParent.SetParent(transform, false);

        Vector3 tPos = terrain.transform.position;

        float minX = tPos.x + wallInset;
        float maxX = tPos.x + terrainSize - wallInset;
        float minZ = tPos.z + wallInset;
        float maxZ = tPos.z + terrainSize - wallInset;

        // Four sides, evenly spaced
        for (int i = 0; i < wallSegmentsPerSide; i++)
        {
            float t = (wallSegmentsPerSide <= 1) ? 0f : (float)i / (wallSegmentsPerSide - 1);

            SpawnWallAt(new Vector3(Mathf.Lerp(minX, maxX, t), 0f, minZ), wallsParent); // bottom
            SpawnWallAt(new Vector3(Mathf.Lerp(minX, maxX, t), 0f, maxZ), wallsParent); // top
            SpawnWallAt(new Vector3(minX, 0f, Mathf.Lerp(minZ, maxZ, t)), wallsParent); // left
            SpawnWallAt(new Vector3(maxX, 0f, Mathf.Lerp(minZ, maxZ, t)), wallsParent); // right
        }
    }

    private void SpawnWallAt(Vector3 xz, Transform parent)
    {
        Vector3 tPos = terrain.transform.position;
        float y = terrain.SampleHeight(xz) + tPos.y;
        Vector3 pos = new Vector3(xz.x, y, xz.z);
        Instantiate(wallPrefab, pos, Quaternion.identity, parent);
    }
}
