
//this doesnt work correctly!!

using System.Collections.Generic;
using UnityEngine;

public class ProceduralTerrain : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GridElement groundBlockPrefab; // regular ground block
    [SerializeField] private GridElement slopeBlockPrefab;  // slope block when elevation increases

    [Header("Grid Settings")]
    [SerializeField] private int mapSize = 20;
    [SerializeField] private float blockSpacingHorizontal = 14f;
    [SerializeField] private float blockSpacingVertical = 4f;

    [Header("Height Settings")]
    [Tooltip("How often we step up by 1 when walking through the maze (0 = flat, 1 = very hilly).")]
    [SerializeField, Range(0f, 1f)]
    private float hilliness = 0.3f;

    [SerializeField] private int maxElevation = 6;

    [Header("Slope Tuning")]
    [Tooltip("Local offset inside the tile for the slope mesh (if its pivot is not centered nicely).")]
    [SerializeField] private Vector3 slopeLocalOffset = Vector3.zero;

    // Logical data
    private int[,] elevations;     // elevation per cell
    private Vector2Int[,] fromDir; // direction we came from (parent -> this cell)

    private void Start()
    {
        GenerateMazeWithElevation();
        BuildTilesFromData();
    }

    /// <summary>
    /// 1st pass: generate a maze (spanning tree) using DFS,
    /// and assign elevation + direction-from-parent for each cell.
    /// </summary>
    private void GenerateMazeWithElevation()
    {
        elevations = new int[mapSize, mapSize];
        fromDir = new Vector2Int[mapSize, mapSize];
        bool[,] visited = new bool[mapSize, mapSize];

        int totalCells = mapSize * mapSize;
        int visitedCount = 0;

        // Maze stack for DFS
        Stack<Vector2Int> stack = new Stack<Vector2Int>();

        // Start at a random cell
        int startX = Random.Range(0, mapSize);
        int startZ = Random.Range(0, mapSize);

        Vector2Int start = new Vector2Int(startX, startZ);
        stack.Push(start);
        visited[startX, startZ] = true;
        elevations[startX, startZ] = 0;
        fromDir[startX, startZ] = Vector2Int.zero;
        visitedCount++;

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            int cx = current.x;
            int cz = current.y;
            int currentElevation = elevations[cx, cz];

            // Get all unvisited 4-directional neighbors
            List<Vector2Int> neighbors = GetUnvisitedNeighbors(cx, cz, visited);

            if (neighbors.Count == 0)
            {
                // Dead-end, backtrack
                stack.Pop();
                continue;
            }

            // Pick a random neighbor to continue the maze
            Vector2Int next = neighbors[Random.Range(0, neighbors.Count)];
            int nx = next.x;
            int nz = next.y;

            // Direction from current -> next
            Vector2Int stepDir = new Vector2Int(nx - cx, nz - cz);

            // Decide new elevation: same as parent or +1 (max 1 step up per move)
            int newElevation = currentElevation;
            float rand = Random.value;

            // Raise elevation with probability (hilliness / 2) to keep it not too crazy
            if (rand < (hilliness / 2f))
            {
                newElevation = Mathf.Min(currentElevation + 1, maxElevation);
            }

            // Store logical maze/elevation data
            visited[nx, nz] = true;
            elevations[nx, nz] = newElevation;
            fromDir[nx, nz] = stepDir; // we came from (cx,cz) with this direction
            visitedCount++;

            // Continue DFS from next
            stack.Push(next);

            if (visitedCount >= totalCells)
                break;
        }

        Debug.Log("Maze with elevation generated. Visited cells: " + visitedCount);
    }

    /// <summary>
    /// 2nd pass: use elevations + fromDir to actually spawn tiles and slopes.
    /// </summary>
    private void BuildTilesFromData()
    {
        for (int x = 0; x < mapSize; x++)
        {
            for (int z = 0; z < mapSize; z++)
            {
                int elevation = elevations[x, z];

                // 1. Fill support column so nothing floats
                for (int y = 0; y < elevation; y++)
                {
                    Vector3 supportPos = new Vector3(
                        x * blockSpacingHorizontal,
                        y * blockSpacingVertical,
                        z * blockSpacingHorizontal);

                    Instantiate(groundBlockPrefab, supportPos, Quaternion.identity, transform);
                }

                // 2. Decide if tile top is ground or slope
                bool isSlope = false;
                Vector2Int slopeDir = Vector2Int.zero;

                Vector2Int step = fromDir[x, z];
                if (step != Vector2Int.zero)
                {
                    int px = x - step.x;
                    int pz = z - step.y;
                    int parentElevation = elevations[px, pz];

                    if (elevation > parentElevation)
                    {
                        isSlope = true;
                        slopeDir = step;
                    }
                }

                CreateTile(x, z, elevation, isSlope, slopeDir);
            }
        }
    }

    /// <summary>
    /// Spawns a tile root at the correct grid position, and under it
    /// either a ground block or a slope, using the stored direction.
    /// </summary>
    private void CreateTile(int x, int z, int elevation, bool isSlope, Vector2Int slopeDirection)
    {
        // Root that aligns perfectly to the grid
        Vector3 tilePos = new Vector3(
            x * blockSpacingHorizontal,
            elevation * blockSpacingVertical,
            z * blockSpacingHorizontal);

        GameObject tileRoot = new GameObject($"Tile_{x}_{z}_h{elevation}");
        tileRoot.transform.SetParent(transform, false);
        tileRoot.transform.position = tilePos;
        tileRoot.transform.rotation = Quaternion.identity;

        GridElement prefabToUse = isSlope ? slopeBlockPrefab : groundBlockPrefab;

        // Rotation for the slope (child)
        Quaternion rot = Quaternion.identity;
        if (isSlope && slopeDirection != Vector2Int.zero)
        {
            // slopeDirection = (dx, dz) from parent -> this cell
            Vector3 dir3 = new Vector3(slopeDirection.x, 0f, slopeDirection.y).normalized;
            // Assumes the slope mesh "climbs" along +Z in its local space.
            rot = Quaternion.LookRotation(dir3, Vector3.up);
        }

        // Instantiate child under tile root
        GridElement element = Instantiate(prefabToUse, tileRoot.transform);
        element.transform.localRotation = rot;
        element.transform.localPosition = isSlope ? slopeLocalOffset : Vector3.zero;

        // Debug: color by elevation (black = 0, white = max)
        float t = (maxElevation > 0) ? (float)elevation / maxElevation : 0f;
        Color color = Color.Lerp(Color.black, Color.white, t);

        element.Init(new Vector3Int(x, elevation, z), color);
    }

    private List<Vector2Int> GetUnvisitedNeighbors(int x, int z, bool[,] visited)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        TryAddNeighbor(x + 1, z, visited, result);
        TryAddNeighbor(x - 1, z, visited, result);
        TryAddNeighbor(x, z + 1, visited, result);
        TryAddNeighbor(x, z - 1, visited, result);

        return result;
    }

    private void TryAddNeighbor(int nx, int nz, bool[,] visited, List<Vector2Int> list)
    {
        if (nx < 0 || nx >= mapSize || nz < 0 || nz >= mapSize)
            return;

        if (!visited[nx, nz])
        {
            list.Add(new Vector2Int(nx, nz));
        }
    }
}

////////////////////////////
//using System.Collections.Generic;
//using UnityEngine;

//public class ProceduralTerrain : MonoBehaviour
//{
//    [Header("Prefabs")]
//    [SerializeField] private GridElement groundBlockPrefab; // regular ground block
//    [SerializeField] private GridElement slopeBlockPrefab;  // slope block when elevation increases

//    [Header("Grid Settings")]
//    [SerializeField] private int mapSize = 20;
//    [SerializeField] private float blockSpacingHorizontal = 14f;
//    [SerializeField] private float blockSpacingVertical = 4f;

//    [Header("Height Settings")]
//    [SerializeField, Range(0f, 1f)]
//    private float hilliness = 0.3f;  // how often elevation is raised
//    [SerializeField] private int maxElevation = 6;

//    [Header("Slope Tuning")]
//    [Tooltip("Extra local offset applied only to slope blocks if your mesh pivot is not centered nicely.")]
//    [SerializeField] private Vector3 slopePositionOffset = Vector3.zero;

//    private GridElement[,] gridElements;
//    private int[,] elevations;

//    private void Start()
//    {
//        GenerateConnectedHillyMap();
//    }

//    private void GenerateConnectedHillyMap()
//    {
//        gridElements = new GridElement[mapSize, mapSize];
//        elevations = new int[mapSize, mapSize];
//        bool[,] filled = new bool[mapSize, mapSize];

//        int totalCells = mapSize * mapSize;
//        int filledCount = 0;

//        // Order in which we placed blocks (for backtracking)
//        List<Vector2Int> placedOrder = new List<Vector2Int>();

//        // 1. Pick a random start cell
//        int startX = Random.Range(0, mapSize);
//        int startZ = Random.Range(0, mapSize);

//        int startElevation = 0;
//        PlaceBlock(startX, startZ, startElevation, isSlope: false, slopeDirection: Vector2Int.zero,
//                   filled: filled, filledCount: ref filledCount);

//        Vector2Int current = new Vector2Int(startX, startZ);
//        placedOrder.Add(current);

//        // 2. Keep going until the whole grid is filled
//        while (filledCount < totalCells)
//        {
//            int currentElevation = elevations[current.x, current.y];

//            // neighbors of the current cell
//            List<Vector2Int> freeNeighbors = GetFreeNeighbors(current.x, current.y, filled);

//            if (freeNeighbors.Count > 0)
//            {
//                // There is at least one free neighbor; pick one randomly
//                Vector2Int next = freeNeighbors[Random.Range(0, freeNeighbors.Count)];

//                // direction in grid space: (dx, dz)
//                Vector2Int dir = new Vector2Int(next.x - current.x, next.y - current.y);

//                // Decide whether to raise elevation based on Hilliness
//                int newElevation = currentElevation;
//                float rand = Random.value; // 0..1

//                // As requested: raise if rand < (hilliness / 2f) to keep it from going too crazy
//                if (rand < (hilliness / 2f))
//                {
//                    newElevation = Mathf.Min(currentElevation + 1, maxElevation);
//                }

//                bool isSlope = newElevation > currentElevation;

//                PlaceBlock(next.x, next.y, newElevation, isSlope, dir,
//                           filled, ref filledCount);

//                current = next;
//                placedOrder.Add(current);
//            }
//            else
//            {
//                // No free neighbors from current; scan from the start of the chain
//                bool foundNewBranchStart = false;

//                for (int i = 0; i < placedOrder.Count; i++)
//                {
//                    Vector2Int candidate = placedOrder[i];
//                    List<Vector2Int> candidateNeighbors = GetFreeNeighbors(candidate.x, candidate.y, filled);

//                    if (candidateNeighbors.Count > 0)
//                    {
//                        current = candidate;
//                        foundNewBranchStart = true;
//                        break;
//                    }
//                }

//                if (!foundNewBranchStart)
//                {
//                    // Should not happen if the algorithm is correct
//                    Debug.LogWarning("No cells with free neighbors found, but grid not fully filled.");
//                    break;
//                }
//            }
//        }

//        Debug.Log("Connected hilly map generation complete. Filled cells: " + filledCount);
//    }

//    private void PlaceBlock(int x, int z, int elevation, bool isSlope, Vector2Int slopeDirection,
//                            bool[,] filled, ref int filledCount)
//    {
//        elevations[x, z] = elevation;

//        // Fill column underneath so we don't have floating tiles
//        if (elevation > 0)
//        {
//            FillColumnBelow(x, z, elevation);
//        }

//        GridElement element = CreateElement(x, z, elevation, isSlope, slopeDirection);
//        gridElements[x, z] = element;

//        filled[x, z] = true;
//        filledCount++;
//    }

//    private void FillColumnBelow(int x, int z, int elevation)
//    {
//        // Spawn ground blocks from y=0 up to y=elevation-1
//        for (int y = 0; y < elevation; y++)
//        {
//            Vector3 pos = new Vector3(
//                x * blockSpacingHorizontal,
//                y * blockSpacingVertical,
//                z * blockSpacingHorizontal);

//            // These are just support blocks; we don't need to track them in gridElements.
//            Instantiate(
//                groundBlockPrefab,
//                pos,
//                Quaternion.identity,
//                transform);
//        }
//    }

//    private GridElement CreateElement(int x, int z, int elevation, bool isSlope, Vector2Int slopeDirection)
//    {
//        Vector3 worldPos = new Vector3(
//            x * blockSpacingHorizontal,
//            elevation * blockSpacingVertical,
//            z * blockSpacingHorizontal);

//        GridElement prefabToUse = isSlope ? slopeBlockPrefab : groundBlockPrefab;

//        // Rotation: for slopes, rotate so they "face" the direction we climbed.
//        Quaternion rot = Quaternion.identity;

//        if (isSlope && slopeDirection != Vector2Int.zero)
//        {
//            // dir (dx, dz) → world direction
//            Vector3 dir3 = new Vector3(slopeDirection.x, 0f, slopeDirection.y).normalized;
//            // Assumes your slope mesh points "forward" (its long side) along +Z.
//            rot = Quaternion.LookRotation(dir3, Vector3.up);
//        }

//        // Position: allow a tweakable offset for slopes (in case the mesh pivot is weird)
//        Vector3 finalPos = worldPos + (isSlope ? slopePositionOffset : Vector3.zero);

//        GridElement element = Instantiate(
//            prefabToUse,
//            finalPos,
//            rot,
//            transform);

//        // Brightness based on elevation: black → white
//        float t = (maxElevation > 0) ? (float)elevation / maxElevation : 0f;
//        Color color = Color.Lerp(Color.black, Color.white, t);

//        element.Init(new Vector3Int(x, elevation, z), color);

//        return element;
//    }

//    private List<Vector2Int> GetFreeNeighbors(int x, int z, bool[,] filled)
//    {
//        List<Vector2Int> result = new List<Vector2Int>();

//        TryAddNeighbor(x + 1, z, filled, result);
//        TryAddNeighbor(x - 1, z, filled, result);
//        TryAddNeighbor(x, z + 1, filled, result);
//        TryAddNeighbor(x, z - 1, filled, result);

//        return result;
//    }

//    private void TryAddNeighbor(int nx, int nz, bool[,] filled, List<Vector2Int> list)
//    {
//        if (nx < 0 || nx >= mapSize || nz < 0 || nz >= mapSize)
//            return;

//        if (!filled[nx, nz])
//        {
//            list.Add(new Vector2Int(nx, nz));
//        }
//    }
//}


/////////////////////////////
//workign nelevation bad slopes 

//using System.Collections.Generic;
//using UnityEngine;

//public class ProceduralTerrain : MonoBehaviour
//{
//    [Header("Prefabs")]
//    [SerializeField] private GridElement groundBlockPrefab; // regular ground block
//    [SerializeField] private GridElement slopeBlockPrefab;  // slope block when elevation increases

//    [Header("Grid Settings")]
//    [SerializeField] private int mapSize = 20;
//    [SerializeField] private float blockSpacingHorizontal = 14f;
//    [SerializeField] private float blockSpacingVertical = 4f;

//    [Header("Height Settings")]
//    [SerializeField, Range(0f, 1f)]
//    private float hilliness = 0.3f;  // "Hilliness" slider in inspector
//    [SerializeField] private int maxElevation = 6;

//    private GridElement[,] gridElements;
//    private int[,] elevations;

//    private void Start()
//    {
//        GenerateConnectedHillyMap();
//    }

//    private void GenerateConnectedHillyMap()
//    {
//        gridElements = new GridElement[mapSize, mapSize];
//        elevations = new int[mapSize, mapSize];
//        bool[,] filled = new bool[mapSize, mapSize];

//        int totalCells = mapSize * mapSize;
//        int filledCount = 0;

//        // Order in which we placed blocks (for backtracking)
//        List<Vector2Int> placedOrder = new List<Vector2Int>();

//        // 1. Pick a random start cell
//        int startX = Random.Range(0, mapSize);
//        int startZ = Random.Range(0, mapSize);

//        int startElevation = 0;
//        PlaceBlock(startX, startZ, startElevation, isSlope: false, filled, ref filledCount);
//        Vector2Int current = new Vector2Int(startX, startZ);
//        placedOrder.Add(current);

//        // 2. Keep going until the whole grid is filled
//        while (filledCount < totalCells)
//        {
//            int currentElevation = elevations[current.x, current.y];

//            // neighbors of the current cell
//            List<Vector2Int> freeNeighbors = GetFreeNeighbors(current.x, current.y, filled);

//            if (freeNeighbors.Count > 0)
//            {
//                // There is at least one free neighbor; pick one randomly
//                Vector2Int next = freeNeighbors[Random.Range(0, freeNeighbors.Count)];

//                // Decide whether to raise elevation based on Hilliness
//                int newElevation = currentElevation;
//                float rand = Random.value; // 0..1

//                // We divide by 2 as requested: (hilliness / 2f)
//                if (rand < (hilliness / 2f))
//                {
//                    newElevation = Mathf.Min(currentElevation + 1, maxElevation);
//                }

//                bool isSlope = newElevation > currentElevation;

//                PlaceBlock(next.x, next.y, newElevation, isSlope, filled, ref filledCount);

//                current = next;
//                placedOrder.Add(current);
//            }
//            else
//            {
//                // No free neighbors from current; scan from the start of the chain
//                bool foundNewBranchStart = false;

//                for (int i = 0; i < placedOrder.Count; i++)
//                {
//                    Vector2Int candidate = placedOrder[i];
//                    List<Vector2Int> candidateNeighbors = GetFreeNeighbors(candidate.x, candidate.y, filled);

//                    if (candidateNeighbors.Count > 0)
//                    {
//                        current = candidate;
//                        foundNewBranchStart = true;
//                        break;
//                    }
//                }

//                if (!foundNewBranchStart)
//                {
//                    // Should not happen if the algorithm is correct
//                    Debug.LogWarning("No cells with free neighbors found, but grid not fully filled.");
//                    break;
//                }
//            }
//        }

//        Debug.Log("Connected hilly map generation complete. Filled cells: " + filledCount);
//    }

//    private void PlaceBlock(int x, int z, int elevation, bool isSlope, bool[,] filled, ref int filledCount)
//    {
//        elevations[x, z] = elevation;

//        GridElement element = CreateElement(x, z, elevation, isSlope);
//        gridElements[x, z] = element;

//        filled[x, z] = true;
//        filledCount++;
//    }

//    private GridElement CreateElement(int x, int z, int elevation, bool isSlope)
//    {
//        Vector3 worldPos = new Vector3(
//            x * blockSpacingHorizontal,
//            elevation * blockSpacingVertical,
//            z * blockSpacingHorizontal);

//        GridElement prefabToUse = isSlope ? slopeBlockPrefab : groundBlockPrefab;

//        GridElement element = Instantiate(
//            prefabToUse,
//            worldPos,
//            Quaternion.identity,
//            transform);

//        // "Perlin-like" brightness: low elevation = black, high elevation = white
//        float t = (maxElevation > 0) ? (float)elevation / maxElevation : 0f;
//        Color color = Color.Lerp(Color.black, Color.white, t);

//        element.Init(new Vector3Int(x, elevation, z), color);

//        return element;
//    }

//    private List<Vector2Int> GetFreeNeighbors(int x, int z, bool[,] filled)
//    {
//        List<Vector2Int> result = new List<Vector2Int>();

//        TryAddNeighbor(x + 1, z, filled, result);
//        TryAddNeighbor(x - 1, z, filled, result);
//        TryAddNeighbor(x, z + 1, filled, result);
//        TryAddNeighbor(x, z - 1, filled, result);

//        return result;
//    }

//    private void TryAddNeighbor(int nx, int nz, bool[,] filled, List<Vector2Int> list)
//    {
//        if (nx < 0 || nx >= mapSize || nz < 0 || nz >= mapSize)
//            return;

//        if (!filled[nx, nz])
//        {
//            list.Add(new Vector2Int(nx, nz));
//        }
//    }
//}



//working flat grid

//using System.Collections.Generic;
//using UnityEngine;

//public class ProceduralTerrain : MonoBehaviour
//{
//    [Header("Prefabs")]
//    [SerializeField] private GridElement groundBlockPrefab; // regular ground block
//    // [SerializeField] private GridElement slopeBlockPrefab; // slope block (not used yet in flat stage)

//    [Header("Grid Settings")]
//    [SerializeField] private int mapSize = 20;
//    [SerializeField] private float blockSpacingHorizontal = 14f;
//    [SerializeField] private float blockSpacingVertical = 4f;

//    private GridElement[,] gridElements;

//    private void Start()
//    {
//        GenerateConnectedFlatMap();
//    }

//    private void GenerateConnectedFlatMap()
//    {
//        gridElements = new GridElement[mapSize, mapSize];
//        bool[,] filled = new bool[mapSize, mapSize];

//        int totalCells = mapSize * mapSize;
//        int filledCount = 0;

//        // The order in which we placed blocks (needed for the backtracking step).
//        List<Vector2Int> placedOrder = new List<Vector2Int>();

//        // 1. Pick a random start cell inside the playable grid
//        int startX = Random.Range(0, mapSize);
//        int startZ = Random.Range(0, mapSize);

//        PlaceBlock(startX, 0, startZ, filled, ref filledCount);
//        Vector2Int current = new Vector2Int(startX, startZ);
//        placedOrder.Add(current);

//        // 2. Keep going until the whole grid is filled
//        while (filledCount < totalCells)
//        {
//            // Get free neighbors around the current cell
//            List<Vector2Int> freeNeighbors = GetFreeNeighbors(current.x, current.y, filled);

//            if (freeNeighbors.Count > 0)
//            {
//                // 2a. If there are free neighbors, pick one at random and move there
//                Vector2Int next = freeNeighbors[Random.Range(0, freeNeighbors.Count)];
//                PlaceBlock(next.x, 0, next.y, filled, ref filledCount);
//                current = next;
//                placedOrder.Add(current);
//            }
//            else
//            {
//                // 2b. No free neighbors: scan from the start of placedOrder
//                bool foundNewBranchStart = false;

//                for (int i = 0; i < placedOrder.Count; i++)
//                {
//                    Vector2Int candidate = placedOrder[i];
//                    List<Vector2Int> candidateNeighbors = GetFreeNeighbors(candidate.x, candidate.y, filled);

//                    if (candidateNeighbors.Count > 0)
//                    {
//                        // Found a block with at least one free neighbor: continue from here
//                        current = candidate;
//                        foundNewBranchStart = true;
//                        break;
//                    }
//                }

//                if (!foundNewBranchStart)
//                {
//                    // Safety check: if we get here but filledCount < totalCells,
//                    // something is wrong with the neighbor logic.
//                    Debug.LogWarning("No cells with free neighbors found, but grid not fully filled.");
//                    break;
//                }
//            }
//        }

//        Debug.Log("Connected flat map generation complete. Filled cells: " + filledCount);
//    }

//    private void PlaceBlock(int x, int elevation, int z, bool[,] filled, ref int filledCount)
//    {
//        GridElement element = CreateElement(x, elevation, z);
//        gridElements[x, z] = element;
//        filled[x, z] = true;
//        filledCount++;
//    }

//    private GridElement CreateElement(int x, int y, int z)
//    {
//        Vector3 worldPos = new Vector3(
//            x * blockSpacingHorizontal,
//            y * blockSpacingVertical,
//            z * blockSpacingHorizontal);

//        GridElement element = Instantiate(
//            groundBlockPrefab,
//            worldPos,
//            Quaternion.identity,
//            transform);

//        // If later you add coordinates/elevation API on GridElement, you can initialize it here.
//        // element.SetCoordinates(new Vector3Int(x, y, z));

//        return element;
//    }

//    private List<Vector2Int> GetFreeNeighbors(int x, int z, bool[,] filled)
//    {
//        List<Vector2Int> result = new List<Vector2Int>();

//        TryAddNeighbor(x + 1, z, filled, result);
//        TryAddNeighbor(x - 1, z, filled, result);
//        TryAddNeighbor(x, z + 1, filled, result);
//        TryAddNeighbor(x, z - 1, filled, result);

//        return result;
//    }

//    private void TryAddNeighbor(int nx, int nz, bool[,] filled, List<Vector2Int> list)
//    {
//        if (nx < 0 || nx >= mapSize || nz < 0 || nz >= mapSize)
//            return;

//        if (!filled[nx, nz])
//        {
//            list.Add(new Vector2Int(nx, nz));
//        }
//    }
//}
///////////////////////

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class ProceduralTerrain : MonoBehaviour
//{
//    [SerializeField] private GridElement blockPrefab;
//    [SerializeField] private int mapSize = 20;

//    [SerializeField] private float blockSpacingHorizontal;
//    [SerializeField] private float blockSpacingVertical;

//    private GridElement[,] gridElements;

//    private void Start()
//    {
//        GenerateFlatMap();
//    }

//    private void GenerateFlatMap()
//    {
//        gridElements = new GridElement[mapSize, mapSize];
//        for (int i = 0; i < mapSize; i++)
//        {
//            for (int j = 0; j < mapSize; j++)
//            {
//                CreateElement(i, 0, j);
//            }
//        }

//    }
//    //private GridElement CreateElement(int x, int y, int z) 
//    //{
//    //    GridElement element = Instantiate(blockPrefab, new Vector3(x * blockSpacingHorizontal, y * blockSpacingVertical, z * blockSpacingHorizontal), Quaternion.identity, transform);
//    //    element.Coordinates = new Vector3(x, y, z);
//    //    gridElements[x,z] = element;
//    //    return element;
//    //}
//    private GridElement CreateElement(int x, int y, int z)
//    {
//        Vector3 worldPos = new Vector3(
//            x * blockSpacingHorizontal,
//            y * blockSpacingVertical,
//            z * blockSpacingHorizontal);

//        GridElement element = Instantiate(blockPrefab, worldPos, Quaternion.identity, transform);

//        // store logical grid coordinates inside the element
//        element.SetCoordinates(new Vector3Int(x, y, z));

//        gridElements[x, z] = element;
//        return element;
//    }


//}
