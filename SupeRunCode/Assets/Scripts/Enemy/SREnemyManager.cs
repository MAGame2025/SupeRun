using System;
using System.Collections.Generic;
using UnityEngine;

public class SREnemyManager : MonoBehaviour
{
    public static SREnemyManager Instance { get; private set; }

    [SerializeField] private Transform player;

    private bool simulationEnabled = true;

    [Header("Full Logic (Closest First)")]
    [Tooltip("Max enemies that can run Full logic per frame. These will be the CLOSEST enemies within Full radius.")]
    [SerializeField] private int maxFullLogicEnemies = 300;

    [Tooltip("Enemies within this radius are eligible for Full logic (then closest N get Full).")]
    [SerializeField] private float fullLogicRadius = 30f;

    [Header("Far Logic")]
    [Tooltip("Enemies beyond Full radius but within this radius are Far (reduced logic).")]
    [SerializeField] private float farLogicRadius = 90f;

    [Tooltip("Far enemies run Tick once every N frames.")]
    [SerializeField] private int farUpdateIntervalFrames = 10;
    [SerializeField, Range(0.1f, 1f)]
    private float farSpeedMultiplier = 0.5f;

    [Header("Despawn")]
    [Tooltip("Enemies beyond this radius despawn silently (no XP, no kill). Must be >= Far radius.")]
    [SerializeField] private float despawnRadius = 180f;

    [Header("Component Disabling")]
    [Tooltip("Disable CharacterController + Animator for Far enemies.")]
    [SerializeField] private bool disableComponentsWhenNotFull = true;

    [Header("Player Auto-Find")]
    [Tooltip("If player is not assigned, attempt FindGameObjectWithTag('Player') once every N frames.")]
    [SerializeField] private int playerFindIntervalFrames = 30;

    private float fullLogicRadiusSq;
    private float farLogicRadiusSq;
    private float despawnRadiusSq;

    private readonly List<SREnemyLite> enemies = new();

    // Reusable snapshot buffer (no enemies.ToArray() allocation)
    private SREnemyLite[] snapshot = new SREnemyLite[256];

    // Per-index data (reused)
    private float[] distSqByIndex = new float[256];
    private bool[] isFullByIndex = new bool[256];

    // Candidate arrays for closest-full selection (reused)
    private int[] candidateIndices = new int[256];
    private float[] candidateDistSq = new float[256];

    public Transform Player => player;
    public int ActiveEnemyCount => enemies.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RecomputeSq();
    }

    private void OnValidate()
    {
        if (maxFullLogicEnemies < 0) maxFullLogicEnemies = 0;

        if (fullLogicRadius < 0f) fullLogicRadius = 0f;
        if (farLogicRadius < fullLogicRadius) farLogicRadius = fullLogicRadius;
        if (despawnRadius < farLogicRadius) despawnRadius = farLogicRadius;

        if (farUpdateIntervalFrames < 1) farUpdateIntervalFrames = 1;
        if (playerFindIntervalFrames < 1) playerFindIntervalFrames = 1;

        RecomputeSq();
    }

    private void RecomputeSq()
    {
        fullLogicRadiusSq = fullLogicRadius * fullLogicRadius;
        farLogicRadiusSq = farLogicRadius * farLogicRadius;
        despawnRadiusSq = despawnRadius * despawnRadius;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
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

    private void EnsureCapacity(int needed)
    {
        if (snapshot.Length < needed)
        {
            int newSize = snapshot.Length;
            while (newSize < needed) newSize *= 2;

            snapshot = new SREnemyLite[newSize];
            distSqByIndex = new float[newSize];
            isFullByIndex = new bool[newSize];
        }

        if (candidateIndices.Length < needed)
        {
            int newSize = candidateIndices.Length;
            while (newSize < needed) newSize *= 2;

            candidateIndices = new int[newSize];
            candidateDistSq = new float[newSize];
        }
    }

    public void SetSimulationEnabled(bool enabled)
    {
        simulationEnabled = enabled;
    }
    private void Update()
    {
        // Auto-find player if not assigned (throttled)
        if (player == null)
        {
            if (Time.frameCount % playerFindIntervalFrames == 0)
            {
                var pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null)
                    player = pObj.transform;
            }
        }

        if (!simulationEnabled)
            return;

        if (player == null)
            return;

        int count = enemies.Count;
        if (count == 0)
            return;

        EnsureCapacity(count);

        // Snapshot copy (no allocation)
        enemies.CopyTo(0, snapshot, 0, count);

        Vector3 playerPos = player.position;
        float dt = Time.deltaTime;
        int frame = Time.frameCount;

        Array.Clear(isFullByIndex, 0, count);

        // 1) Compute distances + despawn out-of-range + build Full candidates
        int candidateCount = 0;

        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null || !e.isActiveAndEnabled)
            {
                distSqByIndex[i] = float.PositiveInfinity;
                continue;
            }

            float distSq = (e.Position - playerPos).sqrMagnitude;
            distSqByIndex[i] = distSq;

            // Hard despawn (silent, no XP)
            if (distSq > despawnRadiusSq)
            {
                e.DespawnWithoutRewards();
                continue;
            }

            if (maxFullLogicEnemies > 0 && distSq <= fullLogicRadiusSq)
            {
                candidateIndices[candidateCount] = i;
                candidateDistSq[candidateCount] = distSq;
                candidateCount++;
            }
        }

        // 2) Closest-first selection for Full
        if (candidateCount > 0 && maxFullLogicEnemies > 0)
        {
            Array.Sort(candidateDistSq, candidateIndices, 0, candidateCount);

            int fullCount = Mathf.Min(maxFullLogicEnemies, candidateCount);
            for (int k = 0; k < fullCount; k++)
            {
                int idx = candidateIndices[k];
                if (idx >= 0 && idx < count)
                    isFullByIndex[idx] = true;
            }
        }

        // 3) Apply LOD + throttle + tick
        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null || !e.isActiveAndEnabled)
                continue;

            float distSq = distSqByIndex[i];
            if (float.IsPositiveInfinity(distSq))
                continue;

            if (!e.isActiveAndEnabled)
                continue;

            EnemyLOD lod = isFullByIndex[i] ? EnemyLOD.Full : EnemyLOD.Far;

            e.ApplyLODState(lod, disableComponentsWhenNotFull);

            float effectiveDt = dt;

            if (lod == EnemyLOD.Far)
            {
                int interval = farUpdateIntervalFrames;

                if (interval > 1)
                {
                    if ((frame + e.FrameOffset) % interval != 0)
                        continue;

                    effectiveDt = dt * interval * farSpeedMultiplier;
                }
            }

            e.Tick(effectiveDt, distSq, lod);
        }

    }
}

public enum EnemyLOD
{
    Full,
    Far
}
