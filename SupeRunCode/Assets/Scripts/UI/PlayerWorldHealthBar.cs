using UnityEngine;
using UnityEngine.UI;

public class PlayerWorldHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage;

    [Header("Behavior")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float visibleHealthThreshold = 0.999f;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (visualRoot == null)
            visualRoot = gameObject; // fallback
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (fillImage == null) return;

        float t = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        fillImage.fillAmount = t;

        if (hideWhenFull && visualRoot != null)
            visualRoot.SetActive(t < visibleHealthThreshold);
    }
}
