using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Dictionary<uint, Enemy> enemies = new Dictionary<uint, Enemy>();

    private uint id;        // running ID counter
    private int numEnemies; // for debug / stats

    public static EnemyController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform player;

    public Transform Player => player;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fallback: find player
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
        }
    }

    private void FixedUpdate()
    {
        // Update every enemy's physics here (just like screenshot)
        float dt = Time.fixedDeltaTime;

        foreach (var enemy in enemies.Values)
        {
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                enemy.MyFixedUpdate(dt);
            }
        }
    }

    // ----------- Spawn methods ------------

    // 1) High-level spawn: choose position first (like screenshot)
    public Enemy SpawnEnemy(EnemyData enemyData, int summonerId)
    {
        Vector3 pos = SpawnPositions.GetEnemySpawnPosition(enemyData, player);

        if (pos == SpawnPositions.INVALID_POS)
        {
            Debug.LogError("Failed to find a spawn position for enemy.");
            return null;
        }

        // In the screenshot they also pass waveNumber and forceSpawn.
        // We'll just use waveNumber = 0 and forceSpawn = true for now.
        return SpawnEnemy(enemyData, pos, summonerId, waveNumber: 0, forceSpawn: true);
    }

    // 2) Low-level spawn: you already know the position
    public Enemy SpawnEnemy(EnemyData enemyData, Vector3 pos, int summonerId, int waveNumber, bool forceSpawn = false)
    {
        if (enemyData == null || enemyData.prefab == null)
        {
            Debug.LogError("EnemyData or prefab missing in SpawnEnemy.");
            return null;
        }

        Enemy enemyInstance = Instantiate(enemyData.prefab, pos, Quaternion.identity);
        uint newId = ++id;

        enemyInstance.Initialize(newId, enemyData, player, summonerId, waveNumber);

        enemies[newId] = enemyInstance;
        numEnemies = enemies.Count;

        return enemyInstance;
    }

    // Optional: remove enemy later if you need
    public void DespawnEnemy(uint enemyId)
    {
        if (enemies.TryGetValue(enemyId, out Enemy enemy))
        {
            if (enemy != null)
                Destroy(enemy.gameObject);

            enemies.Remove(enemyId);
            numEnemies = enemies.Count;
        }
    }
}
