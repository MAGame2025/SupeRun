using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectUI : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [SerializeField] private string level1SceneName = "Level1_Tutorial";
    [SerializeField] private string level2SceneName = "Level2_FlatProcedural";
    [SerializeField] private string level3SceneName = "Level3_DesertSlopes";
    [SerializeField] private string level4SceneName = "Level4_Secrets";

    public void OnBackClicked()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnLevel1Clicked() => SceneManager.LoadScene(level1SceneName);
    public void OnLevel2Clicked() => SceneManager.LoadScene(level2SceneName);
    public void OnLevel3Clicked() => SceneManager.LoadScene(level3SceneName);
    public void OnLevel4Clicked() => SceneManager.LoadScene(level4SceneName);
}
