using UnityEngine;

public class SRGrenade : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float upwardAngleDegrees = 15f;

    [Header("Explosion")]
    [SerializeField] private float fuseTime = 0.8f;
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float damage = 50f;
    [SerializeField] private LayerMask damageMask; // set to Enemy layer(s)

    [Header("Knockback")]
    [SerializeField] private float maxKnockbackForce = 25f;

    [Header("Collision")]
    [SerializeField] private LayerMask groundMask; // what counts as ground

    private float timer;
    private Vector3 velocity;
    private bool active;

    // Optional pooling hook
    public System.Action<SRGrenade> OnReturnToPool;

    private void OnEnable()
    {
        timer = 0f;
        active = true;
    }

    private void Update()
    {
        if (!active) return;

        float dt = Time.deltaTime;

        // Predict next position
        Vector3 start = transform.position;
        Vector3 end = start + velocity * dt;

        // Check if we hit the ground along the path
        if (Physics.Linecast(start, end, out RaycastHit hit, groundMask, QueryTriggerInteraction.Collide))
        {
            // Snap to hit point and explode there
            transform.position = hit.point;
            Detonate();
            return;
        }
        else
        {
            // No ground hit, just move
            transform.position = end;
        }

        timer += dt;
        if (timer >= fuseTime)
        {
            Detonate();
        }
    }


    public void Launch(Vector3 startPosition, Vector3 forwardDir)
    {
        transform.position = startPosition;

        // build a slightly upward launch direction
        forwardDir.Normalize();
        Vector3 up = Vector3.up;
        Quaternion tilt = Quaternion.AngleAxis(upwardAngleDegrees, Vector3.Cross(up, forwardDir));
        Vector3 launchDir = tilt * forwardDir;

        velocity = launchDir * speed;
        timer = 0f;
        active = true;
    }

    private void Detonate()
    {
        active = false;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            damageMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform enemyTransform = hits[i].transform;

            var health = enemyTransform.GetComponentInParent<SREnemyHealth>();
            var enemyLite = enemyTransform.GetComponentInParent<SREnemyLite>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }

            if (enemyLite != null)
            {
                // Direction from explosion center to enemy
                Vector3 dir = enemyLite.transform.position - transform.position;
                float distance = dir.magnitude;
                if (distance > 0.0001f)
                {
                    dir /= distance; // normalize

                    // Make it push slightly upward too so it feels punchier
                    dir.y = 0.3f;

                    // Knockback falloff: full in center, less at edge
                    float t = Mathf.Clamp01(1f - (distance / explosionRadius));
                    float force = maxKnockbackForce * t;

                    Vector3 knockback = dir * force;
                    enemyLite.ApplyKnockback(knockback);
                }
            }
        }

        // TODO: VFX / SFX

        if (OnReturnToPool != null)
            OnReturnToPool(this);
        else
            gameObject.SetActive(false);
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
