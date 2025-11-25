using System.Collections.Generic;
using UnityEngine;

public class SREnemyManager : MonoBehaviour
{
    public static SREnemyManager Instance { get; private set; }

    [SerializeField] private Transform player;

    [Header("LOD / Performance")]
    [Tooltip("How many enemies at most can run Full logic per frame.")]
    [SerializeField] private int maxFullLogicEnemies = 300;

    [Tooltip("Within this radius (world units) enemies are eligible for Full logic.")]
    [SerializeField] private float fullLogicRadius = 15f;

    private float fullLogicRadiusSq;

    private readonly List<SREnemyLite> enemies = new();

    public Transform Player => player;

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    public int ActiveEnemyCount => enemies.Count;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        fullLogicRadiusSq = fullLogicRadius * fullLogicRadius;
    }

    public void Register(SREnemyLite enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    public void Unregister(SREnemyLite enemy)
    {
        if (enemy != null)
            enemies.Remove(enemy);
    }


    private int startIndex = 0;

    private void Update()
    {
        // Auto-find player if needed
        if (player == null)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null)
                player = pObj.transform;
        }

        if (player == null)
            return;

        if (enemies.Count == 0)
            return;

        float dt = Time.deltaTime;
        Vector3 playerPos = player.position;

        // Take a snapshot so changes to `enemies` (Register/Unregister)
        // during this frame won't break our indexing.
        SREnemyLite[] snapshot = enemies.ToArray();
        int count = snapshot.Length;
        if (count == 0)
            return;

        int fullUsed = 0;

        // Advance start index based on current count
        startIndex = (startIndex + 1) % count;

        for (int n = 0; n < count; n++)
        {
            int i = (startIndex + n) % count;
            var enemy = snapshot[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            float distSq = (enemy.Position - playerPos).sqrMagnitude;

            EnemyLOD lod;
            if (distSq <= fullLogicRadiusSq && fullUsed < maxFullLogicEnemies)
            {
                lod = EnemyLOD.Full;
                fullUsed++;
            }
            else
            {
                lod = EnemyLOD.Far;
            }

            enemy.Tick(dt, distSq, lod);
        }
    }


}

public enum EnemyLOD
{
    Full,
    Far
}
