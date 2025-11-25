using UnityEngine;

[RequireComponent(typeof(SREnemyLite))]
public class SREnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

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
        // Let the enemy handle despawn / pooling
        enemy.Kill();
    }
}
