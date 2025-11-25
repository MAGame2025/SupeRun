using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private int totalSteps = 100;
    [SerializeField] private int tileSize = 10;
    [SerializeField] private int heightVariationChance = 20; // % chance to go up/down
    [SerializeField] private int turnChance = 30; // % chance to change direction
    [SerializeField] private int roomChance = 10; // % chance to spawn a wider area (room)

    [Header("Prefabs (Must have Colliders)")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject rampPrefab; // Assumes a ramp that goes UP +Forward
    [SerializeField] private GameObject playerPrefab;

    [Header("Container")]
    [SerializeField] private Transform levelParent;

    // Internal State
    private Dictionary<Vector3Int, GameObject> spawnedTiles = new Dictionary<Vector3Int, GameObject>();
    private Vector3Int currentPos;
    private Vector3Int currentDir;

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        // 1. Cleanup
        foreach (Transform child in levelParent)
        {
            Destroy(child.gameObject);
        }
        spawnedTiles.Clear();

        // 2. Initialize
        currentPos = Vector3Int.zero;
        currentDir = Vector3Int.forward; // Start moving forward

        // Spawn Start Platform
        SpawnTile(currentPos, floorPrefab);

        // Move Player to Start
        if (playerPrefab != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = Instantiate(playerPrefab, Vector3.up * 2, Quaternion.identity);
            }

            // Reset player physics
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.transform.position = new Vector3(0, 5, 0);
            if (cc) cc.enabled = true;
        }

        // 3. The Walk
        for (int i = 0; i < totalSteps; i++)
        {
            Step();
        }

        // 4. Fill Walls (Optional: Close off the edges)
        GenerateWalls();
    }

    private void Step()
    {
        // A. Randomly Change Direction
        if (Random.Range(0, 100) < turnChance)
        {
            // Pick a new generic X/Z direction
            int rand = Random.Range(0, 4);
            switch (rand)
            {
                case 0: currentDir = Vector3Int.forward; break;
                case 1: currentDir = Vector3Int.back; break;
                case 2: currentDir = Vector3Int.left; break;
                case 3: currentDir = Vector3Int.right; break;
            }
        }

        // B. Height Change (Ramps)
        bool spawnedRamp = false;
        if (Random.Range(0, 100) < heightVariationChance)
        {
            // 50/50 to go up or down
            bool goUp = Random.Range(0, 2) == 0;

            // If going up, we place a ramp at current pos, then move up and forward
            // If going down, we move forward and down, place a ramp there rotated

            if (goUp)
            {
                // Place Ramp Here facing currentDir
                // Note: You might need to adjust rotation based on your prefab
                Quaternion rot = Quaternion.LookRotation(new Vector3(currentDir.x, 0, currentDir.z));
                SpawnTile(currentPos, rampPrefab, rot, true); // Force replace floor with ramp

                // Move Logical Position
                currentPos += currentDir; // Forward
                currentPos += Vector3Int.up; // Up
            }
            else // Down
            {
                currentPos += currentDir; // Forward first
                currentPos += Vector3Int.down; // Then Down

                // Calculate rotation to look "down" (usually ramp rotated 180 relative to forward, or standard ramp placed lower)
                // For simplicity: We place a ramp facing the OPPOSITE of movement, so we slide down it
                Quaternion rot = Quaternion.LookRotation(new Vector3(-currentDir.x, 0, -currentDir.z));
                SpawnTile(currentPos, rampPrefab, rot, true);
            }
            spawnedRamp = true;
        }

        // C. Standard Floor
        if (!spawnedRamp)
        {
            currentPos += currentDir;
            SpawnTile(currentPos, floorPrefab);

            // Room Generation (Fat snake)
            if (Random.Range(0, 100) < roomChance)
            {
                // Spawn a 3x3 floor around current pos
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        SpawnTile(currentPos + new Vector3Int(x, 0, z), floorPrefab);
                    }
                }
            }
        }
    }

    private void SpawnTile(Vector3Int gridPos, GameObject prefab, Quaternion rotation = default, bool forceReplace = false)
    {
        if (spawnedTiles.ContainsKey(gridPos))
        {
            if (forceReplace)
            {
                // Remove old, place new
                Destroy(spawnedTiles[gridPos]);
                spawnedTiles.Remove(gridPos);
            }
            else
            {
                return; // Already something here
            }
        }

        Vector3 worldPos = new Vector3(gridPos.x * tileSize, gridPos.y * tileSize, gridPos.z * tileSize);

        // Default rotation if none provided
        if (rotation == default) rotation = Quaternion.identity;

        GameObject newObj = Instantiate(prefab, worldPos, rotation, levelParent);
        spawnedTiles.Add(gridPos, newObj);
    }

    private void GenerateWalls()
    {
        // Copy keys to avoid modification errors
        List<Vector3Int> floorPositions = new List<Vector3Int>(spawnedTiles.Keys);

        foreach (Vector3Int pos in floorPositions)
        {
            // Check 4 neighbors
            CheckAndSpawnWall(pos, Vector3Int.forward);
            CheckAndSpawnWall(pos, Vector3Int.back);
            CheckAndSpawnWall(pos, Vector3Int.left);
            CheckAndSpawnWall(pos, Vector3Int.right);
        }
    }

    private void CheckAndSpawnWall(Vector3Int origin, Vector3Int dir)
    {
        Vector3Int neighbor = origin + dir;

        // If there is NO floor at the neighbor, and also NO ramp at the neighbor
        if (!spawnedTiles.ContainsKey(neighbor))
        {
            // Determine position
            // We want the wall on the edge of the tile
            Vector3 worldPos = new Vector3(origin.x * tileSize, origin.y * tileSize, origin.z * tileSize);

            // Offset to edge
            Vector3 offset = new Vector3(dir.x, 0, dir.z) * (tileSize / 2f);
            // Raise slightly so it sits on floor (assuming floor pivot is center)
            Vector3 wallPos = worldPos + offset + (Vector3.up * (tileSize / 2f));

            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));

            GameObject wall = Instantiate(wallPrefab, wallPos, rot, levelParent);
            // Don't add walls to dictionary, we don't build off them
        }
    }
}