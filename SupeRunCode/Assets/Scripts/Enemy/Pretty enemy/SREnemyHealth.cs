using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SREnemyLite))]
public class SREnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("XP Drop")]
    [SerializeField] private int xpOrbsMin = 2;
    [SerializeField] private int xpOrbsMax = 5;
    [SerializeField] private int xpPerOrb = 2;

    [Header("Death")]
    [SerializeField] private float despawnDelay = 1.0f; // match your death clip length

    private Coroutine deathRoutine;

    private bool isDead;
    private float currentHealth;
    private SREnemyLite enemy;

    private void Awake()
    {
        enemy = GetComponent<SREnemyLite>();
        Initialize(); // ensures both health + isDead are correct at start
    }

    public void Initialize()
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        isDead = false;
        currentHealth = maxHealth;
    }


    public void TakeDamage(float amount, bool isCrit)
    {
        if (isDead) return;
        if (amount <= 0f) return;
        if (SRDamageNumberManager.Instance != null)
        {
            SRDamageNumberManager.Instance.Spawn(
                GetDamageNumberWorldPos(),
                Mathf.RoundToInt(amount),
                isCrit
            );
        }

        currentHealth -= amount;

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private Vector3 GetDamageNumberWorldPos()
    {
        Vector3 p = transform.position + Vector3.up * 1.2f; // base height tweak

        // If you have a collider, use its bounds center (more accurate)
        Collider c = GetComponent<Collider>();
        if (c != null) p = c.bounds.center;

        // Random offset so it’s not always same spot
        Vector3 rand = new Vector3(
            Random.Range(-0.35f, 0.35f),
            Random.Range(0.1f, 0.35f),
            Random.Range(-0.35f, 0.35f)
        );

        return p + rand;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Count kill (only real deaths)
        SRRunStats.Instance?.AddKill(1);

        // Drop XP orbs
        if (SRXpManager.Instance != null)
        {
            int orbCount = Random.Range(xpOrbsMin, xpOrbsMax + 1);
            SRXpManager.Instance.SpawnXpOrbs(transform.position, orbCount, xpPerOrb);
        }
        Debug.Log($"SREnemyHealth.Die: spawning XP orbs at {transform.position}");

        // Tell visuals to play death (Animator or future baked system)
        var visual = GetComponent<ISREnemyVisual>();
        if (visual != null)
            visual.PlayDeath();

        // Despawn after delay (or immediately if delay <= 0)
        if (despawnDelay <= 0f)
        {
            enemy.Kill();
            return;
        }

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DespawnAfterDelay());
    }

    private void OnDisable()
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }


    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelay);
        enemy.Kill();
        deathRoutine = null;
    }

}
