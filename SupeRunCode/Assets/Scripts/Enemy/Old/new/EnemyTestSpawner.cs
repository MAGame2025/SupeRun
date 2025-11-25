using UnityEngine;

public class EnemyTestSpawner : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private int totalToSpawn = 200;   // how many enemies you want
    [SerializeField] private int spawnPerFrame = 10;   // how many to spawn per frame
    [SerializeField] private int summonerId = 0;

    private void Start()
    {
        if (EnemyController.Instance != null && enemyData != null)
        {
            StartCoroutine(SpawnBatch());
        }
    }

    private System.Collections.IEnumerator SpawnBatch()
    {
        int spawned = 0;

        while (spawned < totalToSpawn)
        {
            int batch = Mathf.Min(spawnPerFrame, totalToSpawn - spawned);

            for (int i = 0; i < batch; i++)
            {
                EnemyController.Instance.SpawnEnemy(enemyData, summonerId);
            }

            spawned += batch;
            // spread work across frames
            yield return null;
        }
    }
}
