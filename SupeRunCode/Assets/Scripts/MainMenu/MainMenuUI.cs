using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string levelSelectSceneName = "LevelSelect";

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }

    public void OnOptionsClicked()
    {
        Debug.Log("[MainMenu] Options clicked (TODO).");
        // later: open options panel
    }

    public void OnQuitClicked()
    {
        Debug.Log("[MainMenu] Quit clicked.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
