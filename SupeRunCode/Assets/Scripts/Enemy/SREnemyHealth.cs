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

    private bool isDead;

    private float currentHealth;
    private SREnemyLite enemy;

    private void Awake()
    {
        enemy = GetComponent<SREnemyLite>();
        currentHealth = maxHealth;
    }

    public void Initialize()
    {
        // Call this when spawning from the pool, to reset HP.
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        currentHealth -= amount;

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Drop XP orbs
        if (SRXpManager.Instance != null)
        {
            int orbCount = Random.Range(xpOrbsMin, xpOrbsMax + 1);
            SRXpManager.Instance.SpawnXpOrbs(transform.position, orbCount, xpPerOrb);
        }
        Debug.Log($"SREnemyHealth.Die: spawning XP orbs at {transform.position}");

        // Let the enemy handle despawn / pooling
        enemy.Kill();
    }
}
