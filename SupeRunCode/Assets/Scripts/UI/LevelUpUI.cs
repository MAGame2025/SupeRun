using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Controls the "You leveled up – pick 1 of 3" UI.
/// Shows up when LevelUpController fires OnLevelUpOptionsGenerated.
public class LevelUpUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelUpController levelUpController;

    [Header("Panel Root")]
    [Tooltip("Root object for the level-up UI panel (will be enabled/disabled).")]
    [SerializeField] private GameObject panelRoot;

    [Header("Option Buttons")]
    [Tooltip("Buttons for each of the up-to-3 options.")]
    [SerializeField] private Button[] optionButtons;

    [Tooltip("Title text for each option (same size as optionButtons).")]
    [SerializeField] private TMP_Text[] optionTitleTexts;

    [Tooltip("Description text for each option (same size as optionButtons).")]
    [SerializeField] private TMP_Text[] optionDescriptionTexts;

    [Header("Skip")]
    [Tooltip("Optional 'Skip' button to skip taking an upgrade.")]
    [SerializeField] private Button skipButton;

    private List<LevelUpOption> currentOptions;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Start()
    {
        if (levelUpController == null)
        {
            levelUpController = FindAnyObjectByType<LevelUpController>();
        }

        if (levelUpController != null)
        {
            levelUpController.OnLevelUpOptionsGenerated += HandleLevelUpOptions;
        }
        else
        {
            Debug.LogWarning("LevelUpUI: No LevelUpController found in scene.");
        }
    }

    private void OnDestroy()
    {
        if (levelUpController != null)
        {
            levelUpController.OnLevelUpOptionsGenerated -= HandleLevelUpOptions;
        }
    }

    private void HandleLevelUpOptions(List<LevelUpOption> options)
    {
        currentOptions = options;

        if (panelRoot == null || optionButtons == null || optionTitleTexts == null || optionDescriptionTexts == null)
        {
            Debug.LogWarning("LevelUpUI: Panel or button/text arrays not assigned.");
            // Fallback: just auto-pick the first option
            if (options != null && options.Count > 0)
            {
                levelUpController.ApplyOption(options[0]);
            }
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f; // pause game while choosing
        panelRoot.SetActive(true);

        // Setup option buttons
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < options.Count)
            {
                var opt = options[i];

                optionButtons[i].gameObject.SetActive(true);

                if (optionTitleTexts.Length > i && optionTitleTexts[i] != null)
                    optionTitleTexts[i].text = opt.Title;

                if (optionDescriptionTexts.Length > i && optionDescriptionTexts[i] != null)
                    optionDescriptionTexts[i].text = opt.Description;

                int index = i; // capture for closure
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => OnClickOption(index));
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }

        // Setup skip button
        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnClickSkip);
        }
    }

    private void OnClickOption(int index)
    {
        if (currentOptions == null || index < 0 || index >= currentOptions.Count)
        {
            ClosePanel();
            return;
        }

        levelUpController.ApplyOption(currentOptions[index]);
        ClosePanel();
    }

    private void OnClickSkip()
    {
        // Right now, skip = no upgrade, just continue.
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        Time.timeScale = previousTimeScale;
        currentOptions = null;
    }
}
