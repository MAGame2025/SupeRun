using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class XPUIController : MonoBehaviour
{
    [SerializeField] private SRXpManager xpManager;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI xpText;

    [Header("XP Bar (Frame + Fill)")]
    [SerializeField] private Image fillImage; // <-- the Fill Image (Type = Filled)

    [Header("Color")]
    [SerializeField] private Gradient fillColorOverProgress;

    private void Start()
    {
        if (xpManager == null)
            xpManager = SRXpManager.Instance;

        if (xpManager != null)
        {
            xpManager.OnXpChanged += HandleXpChanged;
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

        float t = (xpToNext <= 0) ? 0f : (float)currentXp / xpToNext;
        t = Mathf.Clamp01(t);

        if (fillImage != null)
        {
            // IMPORTANT: in Inspector set Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left
            fillImage.fillAmount = t;

            if (fillColorOverProgress != null)
                fillImage.color = fillColorOverProgress.Evaluate(t);
        }
    }
}
