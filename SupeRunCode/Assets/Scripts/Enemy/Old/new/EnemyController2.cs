//using System.Collections.Generic;
//using UnityEngine;

//public class EnemyController2 : MonoBehaviour
//{
//    public static EnemyController Instance { get; private set; }

//    [Header("Global refs")]
//    [SerializeField] private Transform player;

//    // All active enemies register here
//    private readonly List<EnemyBrain> enemies = new List<EnemyBrain>();

//    public Transform Player => player;

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }

//        Instance = this;

//        // Fallback: try to find the player by tag if not assigned
//        if (player == null)
//        {
//            GameObject p = GameObject.FindGameObjectWithTag("Player");
//            if (p != null)
//                player = p.transform;
//        }
//    }

//    public void Register(EnemyBrain brain)
//    {
//        if (!enemies.Contains(brain))
//            enemies.Add(brain);
//    }

//    public void Unregister(EnemyBrain brain)
//    {
//        enemies.Remove(brain);
//    }

//    private void FixedUpdate()
//    {
//        if (enemies.Count == 0 || player == null)
//            return;

//        float dt = Time.fixedDeltaTime;

//        // Update ALL enemy physics in one place
//        for (int i = 0; i < enemies.Count; i++)
//        {
//            EnemyBrain e = enemies[i];
//            if (e != null && e.isActiveAndEnabled)
//            {
//                e.PhysicsTick(dt);
//            }
//        }
//    }
//}
