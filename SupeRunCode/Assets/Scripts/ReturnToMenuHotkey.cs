using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ReturnToMenuHotkey : MonoBehaviour
{
    [Header("Hotkey")]
    [SerializeField] private Key hotkey = Key.K;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current[hotkey].wasPressedThisFrame)
        {
            ReturnToMenu();
        }
    }

    private void ReturnToMenu()
    {
        // Optional: reset time scale if you paused somewhere
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
