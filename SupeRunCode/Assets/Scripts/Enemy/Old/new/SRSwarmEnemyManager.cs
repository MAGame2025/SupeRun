//// Note: This is the full file. Only key behavior modifications were added.

//using System.Collections.Generic;
//using UnityEngine;

//public class SRSwarmEnemyManager : MonoBehaviour
//{
//    [Header("Rendering")]
//    [SerializeField] private Mesh enemyMesh;
//    [SerializeField] private Material enemyMaterial;
//    [SerializeField] private int maxEnemies = 10000;
//    private Material instancedMaterial;

//    [Header("Movement")]
//    [SerializeField] private float minSpeed = 3f;
//    [SerializeField] private float maxSpeed = 6f;
//    [SerializeField] private float stopDistance = 1.5f;

//    [Header("Spawning")]
//    [SerializeField] private Transform player;
//    [SerializeField] private float spawnRadius = 30f;
//    [SerializeField] private int spawnPerSecond = 500;
//    [SerializeField] private int spawnPerFrame = 50;

//    [Header("Separation")]
//    [SerializeField] private float gridCellSize = 2f;
//    [SerializeField] private float separationRadius = 1f;
//    [SerializeField] private float separationForce = 2f;

//    [Header("Climbing (player height)")]
//    [SerializeField] private float climbSpeed = 3f;
//    [SerializeField] private float maxClimbDelta = 4f;

//    [Header("Obstacle Climbing")]
//    [SerializeField] private LayerMask obstacleMask = ~0;
//    [SerializeField] private float obstacleProbeHeight = 0.5f;
//    [SerializeField] private float obstacleProbeDistance = 0.75f;
//    [SerializeField] private int obstacleCheckStride = 8;

//    [Header("Crowd Climbing")]
//    [SerializeField] private int neighborClimbThreshold = 5;
//    [SerializeField] private float crowdClimbSpeed = 2f;

//    private Vector3[] positions;
//    private float[] speeds;
//    private bool[] alive;
//    private int aliveCount;
//    private readonly Matrix4x4[] matrices = new Matrix4x4[1023];
//    private float spawnAccumulator;
//    private readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
//    private int frameIndex;

//    private void Awake()
//    {
//        positions = new Vector3[maxEnemies];
//        speeds = new float[maxEnemies];
//        alive = new bool[maxEnemies];

//        if (player == null && SREnemyManager.Instance != null && SREnemyManager.Instance.Player != null)
//        {
//            player = SREnemyManager.Instance.Player;
//        }

//        if (enemyMaterial != null)
//        {
//            instancedMaterial = new Material(enemyMaterial) { enableInstancing = true };
//        }
//    }

//    private void Update()
//    {
//        frameIndex++;

//        if (player == null)
//        {
//            if (SREnemyManager.Instance?.Player != null)
//                player = SREnemyManager.Instance.Player;
//            else return;
//        }

//        if (enemyMesh == null || (enemyMaterial == null && instancedMaterial == null))
//            return;

//        if (instancedMaterial == null && enemyMaterial != null)
//            instancedMaterial = new Material(enemyMaterial) { enableInstancing = true };

//        float dt = Time.deltaTime;
//        HandleSpawning(dt);

//        if (aliveCount <= 0) return;

//        BuildSpatialGrid();
//        SimulateEnemies(dt);
//        RenderEnemies();
//    }

//    private void HandleSpawning(float dt)
//    {
//        if (aliveCount >= maxEnemies || spawnPerSecond <= 0) return;

//        spawnAccumulator += spawnPerSecond * dt;
//        int toSpawn = Mathf.Min((int)spawnAccumulator, spawnPerFrame);
//        if (toSpawn <= 0) return;
//        spawnAccumulator -= toSpawn;

//        for (int i = 0; i < toSpawn; i++)
//        {
//            if (aliveCount >= maxEnemies) break;
//            int index = FindFreeSlot();
//            if (index == -1) break;

//            Vector2 circle = Random.insideUnitCircle.normalized;
//            Vector3 offset = new Vector3(circle.x, 0f, circle.y) * spawnRadius;
//            Vector3 pos = player.position + offset;

//            positions[index] = pos;
//            speeds[index] = Random.Range(minSpeed, maxSpeed);
//            alive[index] = true;
//            aliveCount++;
//        }
//    }

//    private int FindFreeSlot()
//    {
//        for (int i = 0; i < maxEnemies; i++)
//            if (!alive[i]) return i;
//        return -1;
//    }

//    private void BuildSpatialGrid()
//    {
//        grid.Clear();
//        float cellSize = gridCellSize;

//        for (int i = 0; i < maxEnemies; i++)
//        {
//            if (!alive[i]) continue;
//            Vector3 pos = positions[i];
//            Vector2Int key = new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.z / cellSize));
//            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
//            list.Add(i);
//        }
//    }

//    private void SimulateEnemies(float dt)
//    {
//        Vector3 playerPos = player.position;
//        float stopDistSq = stopDistance * stopDistance;

//        for (int i = 0; i < maxEnemies; i++)
//        {
//            if (!alive[i]) continue;

//            Vector3 pos = positions[i];
//            ApplySeparation(i, ref pos, dt);

//            Vector3 toPlayer = playerPos - pos;
//            toPlayer.y = 0f;
//            float distSq = toPlayer.sqrMagnitude;

//            if (distSq > stopDistSq)
//            {
//                Vector3 dir = toPlayer.normalized;
//                float speed = speeds[i];

//                if (obstacleCheckStride > 0 && (i + frameIndex) % obstacleCheckStride == 0)
//                {
//                    HandleObstacleClimb(ref pos, dir, dt);
//                }

//                pos += dir * speed * dt;
//            }

//            ApplyClimbing(ref pos, playerPos, dt);
//            positions[i] = pos;
//        }
//    }

//    private void ApplySeparation(int index, ref Vector3 pos, float dt)
//    {
//        float radiusSq = separationRadius * separationRadius;
//        float cellSize = gridCellSize;
//        Vector3 p = pos;
//        int cx = Mathf.FloorToInt(p.x / cellSize);
//        int cz = Mathf.FloorToInt(p.z / cellSize);
//        Vector3 force = Vector3.zero;
//        int neighborCount = 0;

//        for (int x = cx - 1; x <= cx + 1; x++)
//            for (int z = cz - 1; z <= cz + 1; z++)
//            {
//                Vector2Int key = new Vector2Int(x, z);
//                if (!grid.TryGetValue(key, out var bucket)) continue;

//                foreach (var other in bucket)
//                {
//                    if (other == index || !alive[other]) continue;
//                    Vector3 diff = p - positions[other];
//                    diff.y = 0f;
//                    float distSq = diff.sqrMagnitude;
//                    if (distSq < 0.0001f || distSq > radiusSq) continue;

//                    float dist = Mathf.Sqrt(distSq);
//                    float strength = separationForce / Mathf.Max(dist, 0.1f);
//                    force += diff.normalized * strength;
//                    neighborCount++;
//                }
//            }

//        if (force.sqrMagnitude > 0.0001f)
//            pos += force * dt;

//        if (neighborCount >= neighborClimbThreshold)
//            pos.y += crowdClimbSpeed * dt;
//    }

//    private void ApplyClimbing(ref Vector3 pos, Vector3 playerPos, float dt)
//    {
//        float dy = playerPos.y - pos.y;
//        if (dy > 0.1f && dy < maxClimbDelta)
//            pos.y += climbSpeed * dt;
//        else if (dy < -0.5f)
//            pos.y -= climbSpeed * dt * 0.5f;
//    }

//    private void HandleObstacleClimb(ref Vector3 pos, Vector3 dir, float dt)
//    {
//        Vector3 origin = pos + Vector3.up * obstacleProbeHeight;
//        if (Physics.Raycast(origin, dir, out var hit, obstacleProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore))
//        {
//            float targetY = hit.point.y;
//            float dy = targetY - pos.y;
//            if (dy > 0.05f && dy < maxClimbDelta)
//                pos.y = Mathf.MoveTowards(pos.y, targetY + 0.05f, climbSpeed * 1.5f * dt);
//        }
//    }

//    private void RenderEnemies()
//    {
//        if (instancedMaterial == null) return;

//        int batchCount = 0;

//        for (int i = 0; i < maxEnemies; i++)
//        {
//            if (!alive[i]) continue;
//            matrices[batchCount++] = Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.one);
//            if (batchCount == matrices.Length)
//            {
//                Graphics.DrawMeshInstanced(enemyMesh, 0, instancedMaterial, matrices, batchCount);
//                batchCount = 0;
//            }
//        }

//        if (batchCount > 0)
//        {
//            Graphics.DrawMeshInstanced(enemyMesh, 0, instancedMaterial, matrices, batchCount);
//        }
//    }
//}
