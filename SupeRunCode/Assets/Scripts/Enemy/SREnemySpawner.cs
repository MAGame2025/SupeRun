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

    [Header("Spawn Ring (Random Radius)")]
    [Tooltip("Minimum spawn radius from the player.")]
    [SerializeField] private float spawnRadiusMin = 20f;

    [Tooltip("Maximum spawn radius from the player.")]
    [SerializeField] private float spawnRadiusMax = 35f;

    [SerializeField] private float spawnRaycastHeight = 20f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Spawn Height")]
    [SerializeField] private float spawnHeightOffset = 0.6f;

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

    [Header("Gradual Spawn Timing")]
    [Tooltip("Only the first portion of the wave interval spawns enemies. Example: 0.5 = first half spawns, second half no spawns.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float spawnWindowFraction = 0.5f;

    [Tooltip("Never spawn more frequently than this, even if the wave wants a lot of enemies.")]
    [SerializeField] private float minSecondsBetweenSpawns = 0.08f;

    [Tooltip("Safety cap: maximum number of enemies we will spawn in a single Update tick.")]
    [SerializeField] private int maxSpawnPerTick = 5;

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

    // gradual spawn state
    private float currentWaveInterval;
    private float spawnWindowTimeRemaining;
    private float spawnCooldownTimer;
    private float plannedSecondsBetweenSpawns;

    // cached refs
    private SREnemyManager enemyManager;
    private Transform cachedTransform;

    private void OnValidate()
    {
        if (spawnRadiusMin < 0f) spawnRadiusMin = 0f;
        if (spawnRadiusMax < spawnRadiusMin) spawnRadiusMax = spawnRadiusMin;

        if (minSecondsBetweenSpawns < 0.01f) minSecondsBetweenSpawns = 0.01f;
        if (maxSpawnPerTick < 1) maxSpawnPerTick = 1;

        if (hardMaxEnemies < baseMaxEnemies) hardMaxEnemies = baseMaxEnemies;
    }

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
            if (pool == null || pool.prefab == null)
                continue;

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

        // 1) Gradual spawning for current wave (only during the spawn window)
        if (pendingToSpawnThisWave > 0 && spawnWindowTimeRemaining > 0f)
        {
            spawnWindowTimeRemaining -= Time.deltaTime;

            if (spawnCooldownTimer > 0f)
                spawnCooldownTimer -= Time.deltaTime;

            int spawnsThisTick = 0;
            while (pendingToSpawnThisWave > 0 &&
                   spawnWindowTimeRemaining > 0f &&
                   spawnCooldownTimer <= 0f &&
                   spawnsThisTick < maxSpawnPerTick)
            {
                SpawnOneEnemy();
                pendingToSpawnThisWave--;
                spawnsThisTick++;

                // Schedule next spawn
                spawnCooldownTimer = plannedSecondsBetweenSpawns;
            }
        }

        // 2) Wave timer
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
            ChooseRandomPool();
            return;
        }

        currentWaveOverride = GetWaveOverrideFor(currentWave);

        if (currentWaveOverride != null && currentWaveOverride.enemyPrefab != null)
        {
            currentWaveOverridePrefab = currentWaveOverride.enemyPrefab;

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

            if (currentWavePoolIndex == -1 || currentWavePool == null)
            {
                currentWaveUsesPooling = false;
            }
        }
        else
        {
            ChooseRandomPool();
        }
    }

    private void StartNewWave()
    {
        if (enemyPools.Length == 0 || enemyManager == null)
            return;

        SetupWaveSource();

        int enemiesThisWave;

        if (!randomWaves &&
            currentWaveOverride != null &&
            currentWaveOverride.customCount > 0)
        {
            enemiesThisWave = currentWaveOverride.customCount;
        }
        else
        {
            enemiesThisWave = initialEnemiesPerWave + (currentWave - 1) * enemiesPerWaveIncrease;
        }

        int currentMaxEnemies = baseMaxEnemies + (currentWave - 1) * maxEnemiesIncreasePerWave;
        currentMaxEnemies = Mathf.Min(currentMaxEnemies, hardMaxEnemies);

        int active = enemyManager.ActiveEnemyCount;
        int freeSlots = currentMaxEnemies - active;

        if (freeSlots <= 0)
        {
            pendingToSpawnThisWave = 0;
            spawnWindowTimeRemaining = 0f;
            return;
        }

        pendingToSpawnThisWave = Mathf.Min(enemiesThisWave, freeSlots);

        // ---- Gradual spawn setup ----
        // We want all spawns to occur during the FIRST half of the wave interval.
        float spawnWindow = Mathf.Max(0.01f, currentWaveInterval * spawnWindowFraction);
        spawnWindowTimeRemaining = spawnWindow;

        // Space out spawns evenly across the window, but never faster than minSecondsBetweenSpawns.
        if (pendingToSpawnThisWave > 0)
        {
            float evenSpacing = spawnWindow / pendingToSpawnThisWave;
            plannedSecondsBetweenSpawns = Mathf.Max(minSecondsBetweenSpawns, evenSpacing);
        }
        else
        {
            plannedSecondsBetweenSpawns = minSecondsBetweenSpawns;
        }

        // Start spawning immediately
        spawnCooldownTimer = 0f;
    }

    private void ScheduleNextWave()
    {
        currentWave++;

        float interval = initialWaveInterval * Mathf.Pow(waveIntervalMultiplier, currentWave - 1);
        if (interval < minWaveInterval)
            interval = minWaveInterval;

        if (!randomWaves)
        {
            var ov = GetWaveOverrideFor(currentWave);
            if (ov != null && ov.customInterval > 0f)
            {
                interval = ov.customInterval;
            }
        }

        currentWaveInterval = interval;
        timeToNextWave = interval;
    }

    private void SpawnOneEnemy()
    {
        if (cachedPlayer == null)
            return;

        bool isElite = Random.value < eliteChance;

        SREnemyLite enemy = null;

        if (currentWaveUsesPooling && currentWavePool != null)
        {
            enemy = GetEnemyFromPool(currentWavePool);
        }
        else if (currentWaveOverridePrefab != null)
        {
            enemy = Instantiate(currentWaveOverridePrefab, cachedTransform);
        }
        else if (enemyPools.Length > 0)
        {
            int fallbackIndex = Mathf.Clamp(currentWavePoolIndex, 0, enemyPools.Length - 1);
            enemy = GetEnemyFromPool(enemyPools[fallbackIndex]);
            currentWavePool = enemyPools[fallbackIndex];
            currentWavePoolIndex = fallbackIndex;
            currentWaveUsesPooling = true;
        }

        if (enemy == null)
            return;

        Vector3 spawnPos = GetSpawnPositionAroundPlayer(cachedPlayer.position);

        // --- per-prefab safe y offset (cheap) ---
        float yOffset = spawnHeightOffset;

        var cc = enemy.GetComponent<CharacterController>();
        if (cc != null)
        {
            // bottom of capsule relative to transform position:
            // bottomLocal = center.y - height/2
            float bottomLocal = cc.center.y - (cc.height * 0.5f);

            // we want bottom to be above ground by ~0.05
            yOffset = Mathf.Max(yOffset, -bottomLocal + 0.05f);
        }

        spawnPos.y += yOffset;
        enemy.transform.position = spawnPos;


        enemy.gameObject.SetActive(true);

        int poolIndexForEnemy = currentWaveUsesPooling ? currentWavePoolIndex : -1;

        enemy.Initialize(cachedPlayer, isElite, poolIndexForEnemy);
        enemy.ResetHealthIfAny();
    }

    private SREnemyLite GetEnemyFromPool(EnemyPool pool)
    {
        if (pool != null && pool.pool != null && pool.pool.Count > 0)
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
        // Random ring radius between min/max
        Vector2 dir2 = Random.insideUnitCircle;
        if (dir2.sqrMagnitude < 0.0001f)
            dir2 = Vector2.right;
        dir2.Normalize();

        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        Vector3 flatOffset = new Vector3(dir2.x, 0f, dir2.y) * radius;
        Vector3 worldPos = playerPos + flatOffset;

        Vector3 rayOrigin = worldPos + Vector3.up * spawnRaycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, spawnRaycastHeight * 2f, groundMask))
            return hit.point;

        return worldPos;
    }
}
