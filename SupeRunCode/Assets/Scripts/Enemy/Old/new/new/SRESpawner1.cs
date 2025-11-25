//using UnityEngine;

//public class SREnemySpawner : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private Transform player;

//    [Tooltip("Optional. If empty, enemies will spawn in a ring around the player instead of fixed points.")]
//    [SerializeField] private Transform[] spawnPoints;

//    [Header("Wave Settings")]
//    [Tooltip("How many enemies to spawn in the first wave.")]
//    [SerializeField] private int initialWaveSize = 20;

//    [Tooltip("How many extra enemies each new wave adds.")]
//    [SerializeField] private int waveSizeIncrement = 10;

//    [Tooltip("Maximum size a single wave can reach.")]
//    [SerializeField] private int maxWaveSizeCap = 500;

//    [Tooltip("Max enemies allowed in the entire scene at once.")]
//    [SerializeField] private int globalMaxAlive = 1000;

//    [Tooltip("How many enemies we spawn per frame during an active wave.")]
//    [SerializeField] private int enemiesPerFrame = 10;

//    [Tooltip("Cooldown between waves, in seconds.")]
//    [SerializeField] private float waveCooldown = 10f;

//    [Header("Spawn Around Player")]
//    [Tooltip("If true, spawn enemies in a ring around the player instead of fixed spawn points.")]
//    [SerializeField] private bool spawnAroundPlayer = true;

//    [Tooltip("Minimum distance from player when spawning around them.")]
//    [SerializeField] private float minSpawnRadius = 15f;

//    [Tooltip("Maximum distance from player when spawning around them.")]
//    [SerializeField] private float maxSpawnRadius = 25f;

//    [Tooltip("Height above ground to raycast from when spawning around player.")]
//    [SerializeField] private float spawnRayHeight = 20f;

//    [Tooltip("Layer mask for ground when spawning around player.")]
//    [SerializeField] private LayerMask groundMask = ~0;

//    [Header("Pool Prewarm Override")]
//    [Tooltip("If > 0, force-create this many pooled enemies on Start.")]
//    [SerializeField] private int prewarmCount = 0;

//    private float nextWaveStartTime;
//    private int currentWaveSize;
//    private int spawnedThisWave;
//    private bool waveActive;
//    private int waveIndex;

//    private void Start()
//    {
//        if (SREnemyPool.Instance == null)
//        {
//            Debug.LogError("SREnemySpawner: No SREnemyPool in scene.");
//            enabled = false;
//            return;
//        }

//        if (SREnemyManager.Instance == null)
//        {
//            Debug.LogError("SREnemySpawner: No SREnemyManager in scene.");
//            enabled = false;
//            return;
//        }

//        if (player == null)
//        {
//            Debug.LogError("SREnemySpawner: Player reference not set.");
//            enabled = false;
//            return;
//        }

//        if (!spawnAroundPlayer && (spawnPoints == null || spawnPoints.Length == 0))
//        {
//            Debug.LogWarning("SREnemySpawner: No spawn points assigned, falling back to spawnAroundPlayer.");
//            spawnAroundPlayer = true;
//        }

//        // Optional: force extra prewarm
//        if (prewarmCount > 0)
//        {
//            for (int i = 0; i < prewarmCount; ++i)
//            {
//                var e = SREnemyPool.Instance.GetFromPool();
//                SREnemyPool.Instance.ReturnToPool(e);
//            }
//        }

//        currentWaveSize = initialWaveSize;
//        spawnedThisWave = 0;
//        waveIndex = 0;
//        waveActive = false;

//        // First wave after a short delay so everything initializes
//        nextWaveStartTime = Time.time + 2f;
//    }

//    private void Update()
//    {
//        var mgr = SREnemyManager.Instance;
//        if (mgr == null)
//            return;

//        int alive = mgr.ActiveCount;

//        // Global cap: if we've reached globalMaxAlive, just wait.
//        if (alive >= globalMaxAlive)
//            return;

//        if (!waveActive)
//        {
//            // Waiting for next wave to start
//            if (Time.time >= nextWaveStartTime)
//            {
//                waveActive = true;
//                spawnedThisWave = 0;
//                waveIndex++;
//                // You can log this if you want
//                // Debug.Log($"Wave {waveIndex} started. Size: {currentWaveSize}");
//            }
//        }
//        else
//        {
//            // Wave is active: spawn up to enemiesPerFrame each frame, until
//            // 1) we've spawned the wave size, or
//            // 2) we hit the global max alive
//            int remainingWave = currentWaveSize - spawnedThisWave;
//            int remainingGlobal = globalMaxAlive - alive;
//            int canSpawn = Mathf.Min(remainingWave, remainingGlobal, enemiesPerFrame);

//            if (canSpawn <= 0)
//            {
//                EndWave();
//                return;
//            }

//            for (int i = 0; i < canSpawn; ++i)
//            {
//                SpawnOne();
//            }

//            spawnedThisWave += canSpawn;

//            // If we've spawned the entire wave, end it and schedule next
//            if (spawnedThisWave >= currentWaveSize)
//            {
//                EndWave();
//            }
//        }
//    }

//    private void EndWave()
//    {
//        waveActive = false;

//        // Increase wave size for next wave
//        currentWaveSize += waveSizeIncrement;
//        if (currentWaveSize > maxWaveSizeCap)
//            currentWaveSize = maxWaveSizeCap;

//        // Optionally also increase global max with time, if you want:
//        // globalMaxAlive = Mathf.Min(globalMaxAlive + waveSizeIncrement, someHardCap);

//        nextWaveStartTime = Time.time + waveCooldown;
//        // Debug.Log($"Wave {waveIndex} ended. Next in {waveCooldown} seconds. Next wave size: {currentWaveSize}");
//    }

//    private void SpawnOne()
//    {
//        Transform spawnPointTransform;

//        if (spawnAroundPlayer)
//        {
//            Vector3 pos = GetRandomPositionAroundPlayer();
//            spawnPointTransform = null; // we'll set position directly
//            SREnemyBase enemy = SREnemyPool.Instance.GetFromPool();
//            enemy.transform.position = pos;
//            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

//            enemy.OnSpawn(player);
//            SREnemyManager.Instance.RegisterEnemy(enemy);
//        }
//        else
//        {
//            spawnPointTransform = spawnPoints[Random.Range(0, spawnPoints.Length)];

//            SREnemyBase enemy = SREnemyPool.Instance.GetFromPool();
//            enemy.transform.position = spawnPointTransform.position;
//            enemy.transform.rotation = spawnPointTransform.rotation;

//            enemy.OnSpawn(player);
//            SREnemyManager.Instance.RegisterEnemy(enemy);
//        }
//    }

//    private Vector3 GetRandomPositionAroundPlayer()
//    {
//        // Random angle and radius
//        float angle = Random.Range(0f, Mathf.PI * 2f);
//        float radius = Random.Range(minSpawnRadius, maxSpawnRadius);

//        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
//        Vector3 center = player.position + offset;

//        // Raycast down to find ground
//        Vector3 origin = center + Vector3.up * spawnRayHeight;
//        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, spawnRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
//        {
//            return hit.point + Vector3.up * 0.1f;
//        }

//        // If nothing hit, just use player's Y
//        return new Vector3(center.x, player.position.y, center.z);
//    }
//}
