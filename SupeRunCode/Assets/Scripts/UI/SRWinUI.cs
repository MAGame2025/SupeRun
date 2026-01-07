using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SRWinUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;

    private bool shown;

    private void Awake()
    {
        if (panel == null) panel = gameObject;
        panel.SetActive(false);
    }

    public void Show()
    {
        if (shown) return;
        shown = true;

        panel.SetActive(true);

        if (titleText != null)
            titleText.text = "YOU WIN";

        int kills = SRRunStats.Instance != null ? SRRunStats.Instance.EnemiesKilled : 0;

        // If you later add time to SRRunStats, use it here.
        float time = SRRunStats.Instance != null ? SRRunStats.Instance.ElapsedTime : 0f;

        if (statsText != null)
            statsText.text = $"Kills: {kills}\nTime: {time:0.0}s";

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void OnQuitToMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
    public void OnQuitButton()
    {
        Time.timeScale = 1f;

        // If you have a main menu scene, load it here:
        // SceneManager.LoadScene("MainMenu");

        // For now, just quit play mode / app
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    public void OnRestartButton()
    {
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
