using System.Collections.Generic;
using UnityEngine;

public class SREnemySpawner : MonoBehaviour
{
    public static SREnemySpawner Instance { get; private set; }

    [System.Serializable]
    public class EnemyPool
    {
        public SREnemyLite prefab;

        [Tooltip("How many instances of this type to create and keep in the pool at start.")]
        public int prewarmCount = 20;

        [HideInInspector] public Queue<SREnemyLite> pool;
    }

    [System.Serializable]
    public class WaveOverride
    {
        [Min(1)]
        [Tooltip("1-based wave number (1 = first wave).")]
        public int waveNumber = 1;

        [Tooltip("Specific prefab to use for this wave. If not in any pool, it will be instantiated directly (no pooling).")]
        public SREnemyLite enemyPrefab;

        [Tooltip("If > 0, overrides how many enemies this wave will spawn. If 0 or less, the default wave formula is used.")]
        public int customCount = 0;

        [Tooltip("If > 0, overrides the time until this wave starts (seconds). If 0 or less, uses the global scaling formula.")]
        public float customInterval = 0f;
    }

    [Header("Pools")]
    [SerializeField] private EnemyPool[] enemyPools;

    [Header("Spawn Ring")]
    [SerializeField] private float spawnRadius = 25f;
    [SerializeField] private float spawnRaycastHeight = 20f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Waves")]
    [SerializeField] private int initialEnemiesPerWave = 10;
    [SerializeField] private int enemiesPerWaveIncrease = 3;

    [Tooltip("Time between waves at the start (seconds).")]
    [SerializeField] private float initialWaveInterval = 4f;

    [Tooltip("Minimum time between waves at high difficulty.")]
    [SerializeField] private float minWaveInterval = 0.75f;

    [Tooltip("Each wave interval is multiplied by this (e.g. 0.95 makes waves faster over time).")]
    [SerializeField] private float waveIntervalMultiplier = 0.95f;

    [Header("Elites")]
    [Range(0f, 1f)]
    [SerializeField] private float eliteChance = 0.1f;

    [Header("Enemy Cap")]
    [Tooltip("Starting cap on how many enemies can exist at once.")]
    [SerializeField] private int baseMaxEnemies = 200;

    [Tooltip("How much to increase the max-enemy cap each wave.")]
    [SerializeField] private int maxEnemiesIncreasePerWave = 40;

    [Tooltip("Absolute hard cap on enemies, even late-game.")]
    [SerializeField] private int hardMaxEnemies = 800;

    [Header("Spawn Smoothing")]
    [Tooltip("How many enemies we are allowed to spawn per frame (to avoid spikes).")]
    [SerializeField] private int spawnPerFrame = 10;

    [Header("Wave Selection Mode")]
    [Tooltip("If true, every wave uses a random pool. If false, use WaveOverrides where defined, otherwise random.")]
    [SerializeField] private bool randomWaves = true;

    [Tooltip("Per-wave overrides (used only when randomWaves == false).")]
    [SerializeField] private List<WaveOverride> waveOverrides = new List<WaveOverride>();

    private int currentWave = 0;
    private float timeToNextWave;

    // Current wave selection
    private EnemyPool currentWavePool;
    private int currentWavePoolIndex = -1;
    private WaveOverride currentWaveOverride;
    private SREnemyLite currentWaveOverridePrefab;
    private bool currentWaveUsesPooling = true;

    // queued spawns for current wave
    private int pendingToSpawnThisWave;
    private Transform cachedPlayer;

    // cached refs
    private SREnemyManager enemyManager;
    private Transform cachedTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        cachedTransform = transform;

        if (enemyPools == null || enemyPools.Length == 0)
        {
            Debug.LogWarning("[SREnemySpawner] Disabled: no enemy pools or no SREnemyManager instance.");
            enabled = false;
            return;
        }

        PrewarmPools();
        ScheduleNextWave();
    }

    private void PrewarmPools()
    {
        foreach (var pool in enemyPools)
        {
            int count = Mathf.Max(0, pool.prewarmCount);
            pool.pool = new Queue<SREnemyLite>(count);

            for (int i = 0; i < count; i++)
            {
                var enemy = Instantiate(pool.prefab, cachedTransform);
                enemy.gameObject.SetActive(false);
                pool.pool.Enqueue(enemy);
            }
        }
    }

    private void Update()
    {
        if (enemyManager == null)
        {
            enemyManager = SREnemyManager.Instance;
        }
        if (enemyManager == null || enemyManager.Player == null)
            return;

        cachedPlayer = enemyManager.Player;

        // 1. Continue spawning any pending enemies for this wave
        if (pendingToSpawnThisWave > 0)
        {
            SpawnPendingBatch();
        }

        // 2. Wave timer
        timeToNextWave -= Time.deltaTime;
        if (timeToNextWave <= 0f)
        {
            StartNewWave();
            ScheduleNextWave();
        }
    }

    private WaveOverride GetWaveOverrideFor(int waveNumber)
    {
        if (waveOverrides == null)
            return null;

        for (int i = 0; i < waveOverrides.Count; i++)
        {
            var ov = waveOverrides[i];
            if (ov != null && ov.waveNumber == waveNumber)
                return ov;
        }
        return null;
    }

    private void ChooseRandomPool()
    {
        currentWavePoolIndex = -1;
        currentWavePool = null;
        currentWaveOverridePrefab = null;
        currentWaveUsesPooling = true;

        if (enemyPools.Length > 0)
        {
            currentWavePoolIndex = Random.Range(0, enemyPools.Length);
            currentWavePool = enemyPools[currentWavePoolIndex];
        }
    }

    private void SetupWaveSource()
    {
        currentWavePool = null;
        currentWavePoolIndex = -1;
        currentWaveOverride = null;
        currentWaveOverridePrefab = null;
        currentWaveUsesPooling = true;

        if (randomWaves)
        {
            // Pure random mode: ignore overrides entirely.
            ChooseRandomPool();
            return;
        }

        // Scripted mode: try to find an override for this wave.
        currentWaveOverride = GetWaveOverrideFor(currentWave);

        if (currentWaveOverride != null && currentWaveOverride.enemyPrefab != null)
        {
            // Use the override prefab.
            currentWaveOverridePrefab = currentWaveOverride.enemyPrefab;

            // Try to find a pool that uses this prefab.
            currentWavePoolIndex = -1;
            for (int p = 0; p < enemyPools.Length; p++)
            {
                if (enemyPools[p].prefab == currentWaveOverridePrefab)
                {
                    currentWavePoolIndex = p;
                    currentWavePool = enemyPools[p];
                    currentWaveUsesPooling = true;
                    break;
                }
            }

            // If no pool found, we instantiate directly (no pooling).
            if (currentWavePoolIndex == -1 || currentWavePool == null)
            {
                currentWaveUsesPooling = false;
            }
        }
        else
        {
            // No override for this wave: fallback to random pool.
            ChooseRandomPool();
        }
    }

    private void StartNewWave()
    {
        if (enemyPools.Length == 0 || enemyManager == null)
            return;

        // Decide which source this wave will use (random or override).
        SetupWaveSource();

        // How many this wave wants to add (before cap).
        int enemiesThisWave;

        if (!randomWaves &&
            currentWaveOverride != null &&
            currentWaveOverride.customCount > 0)
        {
            // Use the custom count for this specific wave.
            enemiesThisWave = currentWaveOverride.customCount;
        }
        else
        {
            // Default formula.
            enemiesThisWave = initialEnemiesPerWave + (currentWave - 1) * enemiesPerWaveIncrease;
        }

        // Compute current cap.
        int currentMaxEnemies = baseMaxEnemies + (currentWave - 1) * maxEnemiesIncreasePerWave;
        currentMaxEnemies = Mathf.Min(currentMaxEnemies, hardMaxEnemies);

        int active = enemyManager.ActiveEnemyCount;
        int freeSlots = currentMaxEnemies - active;

        if (freeSlots <= 0)
        {
            pendingToSpawnThisWave = 0;
            return;
        }

        pendingToSpawnThisWave = Mathf.Min(enemiesThisWave, freeSlots);
    }

    private void ScheduleNextWave()
    {
        currentWave++;

        // Default formula-based interval
        float interval = initialWaveInterval * Mathf.Pow(waveIntervalMultiplier, currentWave - 1);

        if (interval < minWaveInterval)
            interval = minWaveInterval;

        // If we're in scripted mode, allow this wave to override the interval
        if (!randomWaves)
        {
            var ov = GetWaveOverrideFor(currentWave);
            if (ov != null && ov.customInterval > 0f)
            {
                interval = ov.customInterval;
            }
        }

        timeToNextWave = interval;
    }


    private void SpawnPendingBatch()
    {
        if (cachedPlayer == null)
            return;

        int toSpawnNow = Mathf.Min(spawnPerFrame, pendingToSpawnThisWave);
        pendingToSpawnThisWave -= toSpawnNow;

        for (int i = 0; i < toSpawnNow; i++)
        {
            bool isElite = Random.value < eliteChance;

            SREnemyLite enemy = null;

            // 1) If this wave uses pooling and has a pool, use it.
            if (currentWaveUsesPooling && currentWavePool != null)
            {
                enemy = GetEnemyFromPool(currentWavePool);
            }
            // 2) If we have an override prefab but no pool, instantiate directly (boss/special wave).
            else if (currentWaveOverridePrefab != null)
            {
                enemy = Instantiate(currentWaveOverridePrefab, cachedTransform);
            }
            // 3) Fallback safety: pick from some pool so we don't spawn null.
            else if (enemyPools.Length > 0)
            {
                int fallbackIndex = Mathf.Clamp(currentWavePoolIndex, 0, enemyPools.Length - 1);
                enemy = GetEnemyFromPool(enemyPools[fallbackIndex]);
                currentWavePool = enemyPools[fallbackIndex];
                currentWavePoolIndex = fallbackIndex;
                currentWaveUsesPooling = true;
            }

            if (enemy == null)
                continue;

            Vector3 spawnPos = GetSpawnPositionAroundPlayer(cachedPlayer.position);
            enemy.transform.position = spawnPos;
            enemy.gameObject.SetActive(true);

            // If we are instantiating something not in any pool, report -1
            // so DespawnEnemy will just SetActive(false) and not enqueue.
            int poolIndexForEnemy = currentWaveUsesPooling ? currentWavePoolIndex : -1;

            enemy.Initialize(cachedPlayer, isElite, poolIndexForEnemy);
            var health = enemy.GetComponent<SREnemyHealth>();
            if (health != null)
            {
                health.Initialize();
            }

        }
    }

    private SREnemyLite GetEnemyFromPool(EnemyPool pool)
    {
        if (pool.pool.Count > 0)
            return pool.pool.Dequeue();

        var enemy = Instantiate(pool.prefab, cachedTransform);
        enemy.gameObject.SetActive(false);
        return enemy;
    }

    public void DespawnEnemy(SREnemyLite enemy, int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= enemyPools.Length)
        {
            enemy.gameObject.SetActive(false);
            return;
        }

        enemy.gameObject.SetActive(false);
        enemyPools[poolIndex].pool.Enqueue(enemy);
    }

    private Vector3 GetSpawnPositionAroundPlayer(Vector3 playerPos)
    {
        Vector2 circle = Random.insideUnitCircle.normalized;
        Vector3 flatOffset = new Vector3(circle.x, 0f, circle.y) * spawnRadius;
        Vector3 worldPos = playerPos + flatOffset;

        Vector3 rayOrigin = worldPos + Vector3.up * spawnRaycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, spawnRaycastHeight * 2f, groundMask))
            return hit.point;

        return worldPos;
    }
}
