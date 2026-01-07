using UnityEngine;

public class SRGrenade : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float upwardAngleDegrees = 15f;

    [Tooltip("Gravity used for the arc. Negative means downward.")]
    [SerializeField] private float gravity = -30f;

    [Tooltip("Simulation step size for arc sampling.")]
    [SerializeField] private float simStep = 0.03f;

    [Tooltip("Max number of sim steps for travel path.")]
    [SerializeField] private int maxSimSteps = 120;

    [Tooltip("Fast -> Slow -> Fast amount. Must be < 1 to stay monotonic.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float speedProfileAmount = 0.75f;

    private bool isCrit;

    [Header("Explosion")]
    [SerializeField] private float baseFuseTime = 0.8f;
    [SerializeField] private float baseExplosionRadius = 4f;
    [SerializeField] private float baseDamage = 50f;
    [SerializeField] private LayerMask damageMask;

    [Header("Knockback")]
    [SerializeField] private float baseMaxKnockbackForce = 25f;

    [Header("Collision")]
    [SerializeField] private LayerMask groundMask;

    [Header("FX")]
    [SerializeField] private ParticleSystem explosionVfxPrefab;
    [SerializeField] private AudioClip explosionSfx;
    [SerializeField] private float explosionSfxVolume = 1f;

    // runtime stats (modified by weapon upgrades)
    private float fuseTime;
    private float explosionRadius;
    private float damage;
    private float maxKnockbackForce;

    private float timer;
    private bool active;

    // sampled arc
    private Vector3[] samples;
    private int sampleCount;
    private float travelDuration;
    private float travelT;

    public float Speed => speed;
    public float UpwardAngleDegrees => upwardAngleDegrees;
    public float Gravity => gravity;

    public System.Action<SRGrenade> OnReturnToPool;

    private void OnEnable()
    {
        timer = 0f;
        active = true;

        fuseTime = baseFuseTime;
        explosionRadius = baseExplosionRadius;
        damage = baseDamage;
        maxKnockbackForce = baseMaxKnockbackForce;

        samples = null;
        sampleCount = 0;
        travelDuration = 0f;
        travelT = 0f;
    }

    public void ConfigureStats(float damage, float radius, float maxKnockback, bool isCrit)
    {
        this.damage = damage;
        this.explosionRadius = radius;
        this.maxKnockbackForce = maxKnockback;
        this.isCrit = isCrit;

    }

    public void Launch(Vector3 startPosition, Vector3 forwardDir)
    {
        transform.position = startPosition;

        forwardDir.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, forwardDir);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;

        Quaternion tilt = Quaternion.AngleAxis(upwardAngleDegrees, axis);
        Vector3 launchDir = (tilt * forwardDir).normalized;

        BuildArcSamples(startPosition, launchDir);

        timer = 0f;
        travelT = 0f;
        active = true;
    }

    private void Update()
    {
        if (!active) return;

        float dt = Time.deltaTime;
        timer += dt;

        // If we have an arc path, move along it with fast->slow->fast remap
        if (sampleCount >= 2 && travelDuration > 0.0001f)
        {
            travelT += dt;

            float t01 = Mathf.Clamp01(travelT / travelDuration);

            // Remap time so derivative is high at ends and low in middle:
            // s(t) = t + (a/(2π)) * sin(2πt), with 0<a<1 ensures monotonic.
            float a = Mathf.Clamp(speedProfileAmount, 0f, 0.95f);
            float s = t01 + (a / (2f * Mathf.PI)) * Mathf.Sin(2f * Mathf.PI * t01);
            s = Mathf.Clamp01(s);

            float fIndex = s * (sampleCount - 1);
            int i0 = Mathf.FloorToInt(fIndex);
            int i1 = Mathf.Min(i0 + 1, sampleCount - 1);

            float lerp = fIndex - i0;
            Vector3 pos = Vector3.Lerp(samples[i0], samples[i1], lerp);
            transform.position = pos;

            // If we reached end of samples, detonate immediately (impact)
            if (t01 >= 1f)
            {
                Detonate();
                return;
            }
        }
        else
        {
            // Fallback: if sampling failed for some reason, just fuse-detonate.
            if (timer >= fuseTime)
            {
                Detonate();
                return;
            }
        }

        // Fuse timer still works even if impact didn't happen
        if (timer >= fuseTime)
        {
            Detonate();
        }
    }

    private void BuildArcSamples(Vector3 startPos, Vector3 launchDir)
    {
        Vector3 vel = launchDir * speed;

        // worst-case allocate, then shrink via sampleCount
        samples = new Vector3[maxSimSteps + 1];
        sampleCount = 0;

        Vector3 prev = startPos;
        samples[sampleCount++] = prev;

        float totalTime = 0f;

        for (int i = 0; i < maxSimSteps; i++)
        {
            vel += Vector3.up * (gravity * simStep);
            Vector3 next = prev + vel * simStep;

            // collision check on the segment
            if (Physics.Linecast(prev, next, out RaycastHit hit, groundMask, QueryTriggerInteraction.Ignore))
            {
                samples[sampleCount++] = hit.point;
                totalTime += simStep;
                travelDuration = totalTime;
                return;
            }

            samples[sampleCount++] = next;
            totalTime += simStep;
            prev = next;

            // If fuseTime is shorter than travel, stop sampling at fuse
            if (totalTime >= fuseTime)
                break;
        }

        travelDuration = Mathf.Min(totalTime, fuseTime);
        if (travelDuration <= 0.0001f) travelDuration = fuseTime;
    }

    private void Detonate()
    {
        if (!active) return;
        active = false;

        // FX
        if (explosionVfxPrefab != null)
        {
            ParticleSystem fx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, 5f);
        }

        if (explosionSfx != null)
        {
            AudioSource.PlayClipAtPoint(explosionSfx, transform.position, explosionSfxVolume);
        }

        // Damage / knockback
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
                health.TakeDamage(damage, isCrit);

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

                    enemyLite.ApplyKnockback(dir * force);
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
