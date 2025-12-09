using System.Collections.Generic;
using UnityEngine;

public class FlatProceduralTerrain : MonoBehaviour
{
    [Header("Prefabs (all same size in XZ)")]
    [SerializeField] private GameObject[] groundBlocks;   // required: random ground tiles
    [SerializeField] private GameObject[] treePrefabs;    // optional: trees
    [SerializeField] private GameObject[] structurePrefabs; // optional: buildings / rocks / etc.
    [SerializeField] private GameObject[] grassPrefabs;   // optional: grass / bushes / small deco

    [Header("Boundary Walls")]
    [SerializeField] private bool generateBoundaryWalls = true;
    [Tooltip("Prefab for a single wall tile (same footprint as ground tiles).")]
    [SerializeField] private GameObject wallPrefab;

    [Header("Map Settings")]
    [Tooltip("Grid will be size x size")]
    [SerializeField] private int size = 20;

    [Tooltip("Distance between tile centers on X and Z")]
    [SerializeField] private float tileSpacing = 14f;

    [Header("Content Settings")]
    [Tooltip("How many structures to place in total on the grid")]
    [SerializeField] private int structureCount = 30;  // SC

    [Tooltip("Chance (0–1) for a tree to spawn on a non-structure tile")]
    [SerializeField, Range(0f, 1f)]
    private float treeFrequency = 0.2f;  // TF

    [Tooltip("Chance (0–1) for grass to spawn on a non-structure tile")]
    [SerializeField, Range(0f, 1f)]
    private float grassFrequency = 0.4f; // GF

    [Header("Ground Height")]
    [Tooltip("If auto-detect fails, this value (half-height) is used to place trees/grass on top of ground.")]
    [SerializeField] private float groundHalfHeightOverride = 0.5f;

    private float groundHalfHeight = 0f;

    [Header("Player Spawn")]
    [Tooltip("Player Transform that will be moved onto a random non-structure tile")]
    [SerializeField] private GameObject playerPrefab;
    private GameObject spawnedPlayer;

    [SerializeField] private float Yoffset = 2f;

    // Internals
    private bool[,] hasStructure;
    private Vector3[,] tilePositions;

    private void Start()
    {
        ComputeGroundHalfHeight();
        Generate();
    }

    private void ComputeGroundHalfHeight()
    {
        groundHalfHeight = groundHalfHeightOverride;

        if (groundBlocks != null && groundBlocks.Length > 0 && groundBlocks[0] != null)
        {
            // Try to get a renderer from the first ground prefab
            Renderer r = groundBlocks[0].GetComponentInChildren<Renderer>();
            if (r != null)
            {
                groundHalfHeight = r.bounds.size.y * 0.5f;
                // Debug.Log($"[Terrain] Auto groundHalfHeight = {groundHalfHeight}");
            }
        }
    }

    private void Generate()
    {
        if (size <= 0)
        {
            Debug.LogError("FlatProceduralTerrain: size must be > 0");
            return;
        }

        int totalTiles = size * size;
        hasStructure = new bool[size, size];
        tilePositions = new Vector3[size, size];

        // 1. Ground layer: flat grid, random ground block per tile
        if (groundBlocks == null || groundBlocks.Length == 0)
        {
            Debug.LogWarning("FlatProceduralTerrain: groundBlocks array is empty, no ground will be spawned.");
        }

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                Vector3 pos = new Vector3(x * tileSpacing, 0f, z * tileSpacing);
                tilePositions[x, z] = pos;

                if (groundBlocks != null && groundBlocks.Length > 0)
                {
                    GameObject groundPrefab = groundBlocks[Random.Range(0, groundBlocks.Length)];
                    Instantiate(groundPrefab, pos, Quaternion.identity, transform);
                }
            }
        }

        // 2. Build an index array [0..N-1] and shuffle it
        int[] indices = new int[totalTiles];
        for (int i = 0; i < totalTiles; i++)
            indices[i] = i;

        Shuffle(indices);

        // 3. Structures: take first SC indices after shuffle
        int structuresToPlace = 0;
        if (structurePrefabs != null && structurePrefabs.Length > 0)
        {
            structuresToPlace = Mathf.Clamp(structureCount, 0, totalTiles);
        }

        for (int i = 0; i < structuresToPlace; i++)
        {
            int idx = indices[i];

            // Convert linear index → (x,z)
            int x = idx / size;
            int z = idx % size;

            hasStructure[x, z] = true;

            GameObject structurePrefab = structurePrefabs[Random.Range(0, structurePrefabs.Length)];
            Vector3 pos = tilePositions[x, z];
            pos.y += groundHalfHeight;
            Instantiate(structurePrefab, pos, Quaternion.identity, transform);
        }

        // 4. Trees & Grass on all tiles *without* structures (the rest of the shuffled array)
        List<Vector2Int> freeTilesForPlayer = new List<Vector2Int>();

        bool treesEnabled = treePrefabs != null && treePrefabs.Length > 0;
        bool grassEnabled = grassPrefabs != null && grassPrefabs.Length > 0;

        for (int i = structuresToPlace; i < totalTiles; i++)
        {
            int idx = indices[i];
            int x = idx / size;
            int z = idx % size;

            Vector3 basePos = tilePositions[x, z];
            Vector3 onGroundPos = basePos;
            onGroundPos.y += groundHalfHeight;

            // Tree
            if (treesEnabled && Random.value < treeFrequency)
            {
                GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                Instantiate(treePrefab, onGroundPos, Quaternion.identity, transform);
            }

            // Grass
            if (grassEnabled && Random.value < grassFrequency)
            {
                GameObject grassPrefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
                Instantiate(grassPrefab, onGroundPos, Quaternion.identity, transform);
            }


            // This tile has no structure → eligible for player spawn
            if (!hasStructure[x, z])
            {
                freeTilesForPlayer.Add(new Vector2Int(x, z));
            }
        }

        // 5. Spawn / move player on a random non-structure tile
        if (playerPrefab != null)
        {
            if (freeTilesForPlayer.Count > 0)
            {
                Vector2Int chosen = freeTilesForPlayer[Random.Range(0, freeTilesForPlayer.Count)];
                Vector3 spawnPos = tilePositions[chosen.x, chosen.y ];

                // Small Y offset so the player is not stuck inside the ground
                spawnPos.y += (Yoffset + 1f);

                Debug.Log($"[Terrain] Spawning player at {spawnPos}");

                // actually spawn the player first
                spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

                // tell XP manager which player instance to track
                if (SRXpManager.Instance != null)
                {
                    SRXpManager.Instance.RegisterPlayer(spawnedPlayer.transform);
                }

                // Tell the enemy manager about this runtime-spawned player
                var enemyManager = SREnemyManager.Instance;
                if (enemyManager != null)
                {
                    enemyManager.SetPlayer(spawnedPlayer.transform);
                }
                else
                {
                    Debug.LogWarning("[FlatProceduralTerrain] No SREnemyManager instance found to bind player to.");
                }
            }
            else
            {
                Debug.LogWarning("FlatProceduralTerrain: No free tiles without structures available for player spawn.");
            }
        }
        else
        {
            Debug.LogWarning("FlatProceduralTerrain: No player Transform assigned; player spawn skipped.");
        }

        // 6. Create a ring of wall tiles around the grid
        if (generateBoundaryWalls && wallPrefab != null)
        {
            CreateBoundaryWalls();
        }
        else if (generateBoundaryWalls && wallPrefab == null)
        {
            Debug.LogWarning("FlatProceduralTerrain: generateBoundaryWalls is true but wallPrefab is not assigned.");
        }


    }

    private void CreateBoundaryWalls()
    {
        Transform wallsParent = new GameObject("BoundaryWalls").transform;
        wallsParent.SetParent(transform, false);

        // We treat the playable grid as x,z in [0, size-1]
        // Walls go on coordinates from -1 to size inclusive, skipping the interior.
        for (int x = -1; x <= size; x++)
        {
            for (int z = -1; z <= size; z++)
            {
                bool insidePlayable =
                    x >= 0 && x < size &&
                    z >= 0 && z < size;

                if (insidePlayable)
                    continue; // skip the actual play tiles

                // World position for this wall tile (same formula as ground tiles)
                Vector3 pos = new Vector3(
                    x * tileSpacing,
                    0f,
                    z * tileSpacing);

                Instantiate(wallPrefab, pos, Quaternion.identity, wallsParent);
            }
        }
    }


    /// Fisher–Yates shuffle of a 1D array, in-place.
    private void Shuffle(int[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = array[i];
            array[i] = array[j];
            array[j] = tmp;
        }
    }
}
