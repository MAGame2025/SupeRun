//using System.Collections.Generic;
//using UnityEngine;

//public class SREnemyManager : MonoBehaviour
//{
//    public static SREnemyManager Instance { get; private set; }

//    [Header("References")]
//    [SerializeField] private Transform player;

//    [Header("Update Budget")]
//    [Tooltip("How many enemies are allowed to run TickEnemy() per frame.")]
//    [SerializeField] private int maxTicksPerFrame = 200;

//    private readonly List<SREnemyBase> activeEnemies = new List<SREnemyBase>();
//    private int rollingIndex = 0;

//    public int ActiveCount => activeEnemies.Count;

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;
//    }

//    public void RegisterEnemy(SREnemyBase enemy)
//    {
//        if (enemy == null)
//            return;

//        if (!activeEnemies.Contains(enemy))
//        {
//            activeEnemies.Add(enemy);
//        }
//    }

//    public void UnregisterEnemy(SREnemyBase enemy)
//    {
//        if (enemy == null)
//            return;

//        int index = activeEnemies.IndexOf(enemy);
//        if (index >= 0)
//        {
//            // Keep rollingIndex stableish
//            if (index <= rollingIndex && rollingIndex > 0)
//                rollingIndex--;

//            activeEnemies.RemoveAt(index);
//        }
//    }

//    private void Update()
//    {
//        if (activeEnemies.Count == 0)
//            return;

//        float dt = Time.deltaTime;
//        int count = activeEnemies.Count;

//        int ticksThisFrame = Mathf.Min(maxTicksPerFrame, count);

//        for (int i = 0; i < ticksThisFrame; ++i)
//        {
//            if (count == 0)
//                break;

//            // Wrap the rolling index
//            if (rollingIndex >= count)
//                rollingIndex = 0;

//            SREnemyBase enemy = activeEnemies[rollingIndex];

//            // Advance index first to be safe if enemy modifies list
//            rollingIndex++;

//            if (enemy != null && enemy.isActiveAndEnabled)
//            {
//                enemy.TickEnemy(dt);
//            }

//            // activeEnemies.Count might change if someone died
//            count = activeEnemies.Count;
//            if (count == 0)
//                break;
//        }

//        // Wrap rolling index in case we overshoot
//        if (rollingIndex >= activeEnemies.Count)
//            rollingIndex = 0;
//    }
//}
