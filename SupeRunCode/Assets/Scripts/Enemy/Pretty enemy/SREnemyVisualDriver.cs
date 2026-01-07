using UnityEngine;

public class EnemyVisualDrive : MonoBehaviour
{
    [SerializeField] private float attackVisualRadius = 1.2f;
    [SerializeField] private float attackVisualInterval = 0.8f;

    private Transform player;
    private Vector3 lastPos;
    private float attackTimer;

    private ISREnemyVisual visual;

    private void Awake()
    {
        visual = GetComponent<ISREnemyVisual>();
        lastPos = transform.position;
    }

    private void OnEnable()
    {
        lastPos = transform.position;
        attackTimer = 0f;
        visual?.OnSpawn();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (player == null && SREnemyManager.Instance != null && SREnemyManager.Instance.Player != null)
            player = SREnemyManager.Instance.Player;

        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;

        float speed = delta.magnitude / dt;
        lastPos = transform.position;

        visual?.SetMoveSpeed(speed);

        if (player != null)
        {
            attackTimer -= dt;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude <= attackVisualRadius * attackVisualRadius && attackTimer <= 0f)
            {
                visual?.PlayHit();
                attackTimer = attackVisualInterval;
            }
        }
    }
}
