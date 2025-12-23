using UnityEngine;
using System;


public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public event Action<float, float> OnHealthChanged; // (current, max)


    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private SRPlayerWeapon playerWeapon;

    [Header("Game Over")]
    [Tooltip("Name of the object that has the GameOverUI component. Leave default if unsure.")]
    [SerializeField] private string gameOverCanvasName = "CanvasGameOver";

    private GameOverUI gameOverUI;
    private bool isDead = false;

    private void Awake()
    {
        // Grab local components if not assigned
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerMotor == null)
            playerMotor = GetComponent<PlayerMotor>();

        if (playerWeapon == null)
            playerWeapon = GetComponent<SRPlayerWeapon>();

        // 🔎 Find the GameOverUI in the scene by name (no Inspector linking needed)
        GameObject go = GameObject.Find(gameOverCanvasName);
        if (go != null)
        {
            gameOverUI = go.GetComponent<GameOverUI>();
            if (gameOverUI == null)
                Debug.LogWarning($"Found '{gameOverCanvasName}' but it has no GameOverUI component.");
        }
        else
        {
            Debug.LogWarning($"Could not find object named '{gameOverCanvasName}' for GameOverUI.");
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        currentHealth -= amount;
        NotifyHealthChanged();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        NotifyHealthChanged();
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }


    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Disable movement & input-driven stuff
        if (playerController != null)
            playerController.enabled = false;

        if (playerMotor != null)
            playerMotor.enabled = false;

        if (playerWeapon != null)
            playerWeapon.enabled = false;

        // Unlock cursor for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show game over screen
        if (gameOverUI != null)
        {
            gameOverUI.ShowGameOver();
        }
        else
        {
            Debug.LogWarning("Player died but no GameOverUI found at runtime.");
        }
    }
}
