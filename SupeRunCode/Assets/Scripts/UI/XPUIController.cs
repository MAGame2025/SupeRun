using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class XPUIController : MonoBehaviour
{
    [SerializeField] private SRXpManager xpManager;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private Slider xpSlider;

    private void Start()
    {
        if (xpManager == null)
            xpManager = SRXpManager.Instance;

        if (xpManager != null)
        {
            xpManager.OnXpChanged += HandleXpChanged;
            // Force initial update
            HandleXpChanged(xpManager.CurrentLevel, xpManager.CurrentXp, xpManager.XpToNextLevel);
        }
    }

    private void OnDestroy()
    {
        if (xpManager != null)
            xpManager.OnXpChanged -= HandleXpChanged;
    }

    private void HandleXpChanged(int level, int currentXp, int xpToNext)
    {
        if (levelText != null)
            levelText.text = $"Level {level}";

        if (xpText != null)
            xpText.text = $"XP: {currentXp} / {xpToNext}";

        if (xpSlider != null)
        {
            xpSlider.maxValue = xpToNext;
            xpSlider.value = currentXp;
        }
    }
}
