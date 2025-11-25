//using System.Collections.Generic;
//using UnityEngine;

//public class SREnemyPool : MonoBehaviour
//{
//    public static SREnemyPool Instance { get; private set; }

//    [SerializeField] private SREnemyBase enemyPrefab;
//    [SerializeField] private int prewarmCount = 300;

//    private readonly Queue<SREnemyBase> pool = new Queue<SREnemyBase>();

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;
//    }

//    private void Start()
//    {
//        Prewarm();
//    }

//    private void Prewarm()
//    {
//        for (int i = 0; i < prewarmCount; ++i)
//        {
//            CreateNewInstance();
//        }
//    }

//    private SREnemyBase CreateNewInstance()
//    {
//        SREnemyBase enemy = Instantiate(enemyPrefab, transform);
//        enemy.gameObject.SetActive(false);
//        pool.Enqueue(enemy);
//        return enemy;
//    }

//    public SREnemyBase GetFromPool()
//    {
//        if (pool.Count == 0)
//        {
//            CreateNewInstance();
//        }

//        return pool.Dequeue();
//    }

//    public void ReturnToPool(SREnemyBase enemy)
//    {
//        if (enemy == null)
//            return;

//        enemy.OnDespawn();
//        enemy.transform.SetParent(transform);
//        pool.Enqueue(enemy);
//    }
//}
