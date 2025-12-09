using UnityEngine;

public class SRGrenade : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float upwardAngleDegrees = 15f;

    [Header("Explosion")]
    [SerializeField] private float baseFuseTime = 0.8f;
    [SerializeField] private float baseExplosionRadius = 4f;
    [SerializeField] private float baseDamage = 50f;
    [SerializeField] private LayerMask damageMask; // set to Enemy layer(s)

    [Header("Knockback")]
    [SerializeField] private float baseMaxKnockbackForce = 25f;

    [Header("Collision")]
    [SerializeField] private LayerMask groundMask; // what counts as ground

    private float timer;
    private Vector3 velocity;
    private bool active;

    // runtime stats (modified by weapon upgrades)
    private float fuseTime;
    private float explosionRadius;
    private float damage;
    private float maxKnockbackForce;

    public System.Action<SRGrenade> OnReturnToPool;

    private void OnEnable()
    {
        timer = 0f;
        active = true;

        // default to base values if not configured explicitly
        fuseTime = baseFuseTime;
        explosionRadius = baseExplosionRadius;
        damage = baseDamage;
        maxKnockbackForce = baseMaxKnockbackForce;
    }

    public void ConfigureStats(float damage, float radius, float maxKnockback)
    {
        this.damage = damage;
        this.explosionRadius = radius;
        this.maxKnockbackForce = maxKnockback;
    }

    private void Update()
    {
        if (!active) return;

        float dt = Time.deltaTime;

        Vector3 start = transform.position;
        Vector3 end = start + velocity * dt;

        if (Physics.Linecast(start, end, out RaycastHit hit, groundMask, QueryTriggerInteraction.Collide))
        {
            transform.position = hit.point;
            Detonate();
            return;
        }
        else
        {
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

        forwardDir.Normalize();
        Vector3 up = Vector3.up;
        Vector3 axis = Vector3.Cross(up, forwardDir);
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.right;

        Quaternion tilt = Quaternion.AngleAxis(upwardAngleDegrees, axis);
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
                Vector3 dir = enemyLite.transform.position - transform.position;
                float distance = dir.magnitude;
                if (distance > 0.0001f)
                {
                    dir /= distance;
                    dir.y = 0.3f;

                    float t = Mathf.Clamp01(1f - (distance / explosionRadius));
                    float force = maxKnockbackForce * t;

                    Vector3 knockback = dir * force;
                    enemyLite.ApplyKnockback(knockback);
                }
            }
        }

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
