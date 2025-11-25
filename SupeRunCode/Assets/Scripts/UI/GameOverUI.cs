using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;

    private bool isShown = false;

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (isShown) return;
        isShown = true;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Pause the game
        Time.timeScale = 0f;
    }

    // Called by the Restart button OnClick
    public void OnRestartButton()
    {
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    // Optional: called by a Quit button
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
}
