using UnityEngine;

public class SRProgressManager : MonoBehaviour
{
    public static SRProgressManager Instance { get; private set; }

    [Header("Debug (runtime)")]
    [SerializeField] private PlayerProgress progress = new PlayerProgress();
    public PlayerProgress Progress => progress;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async void LoadFromCloud()
    {
        progress = await SRCloudSaveManager.LoadProgress();

        // defensive: make sure bestLevel is sane
        if (progress.bestLevelReached < 1) progress.bestLevelReached = 1;
        if (progress.lastRunLevelReached < 1) progress.lastRunLevelReached = 1;
    }

    public async void SaveToCloud()
    {
        await SRCloudSaveManager.SaveProgress(progress);
    }

    // Call this at end of run (death/win)
    public void RecordRunEnded(bool died)
    {
        progress.totalRuns++;

        if (died) progress.totalDeaths++;

        int levelReached = 1;
        if (SRXpManager.Instance != null)
            levelReached = SRXpManager.Instance.CurrentLevel;

        progress.lastRunLevelReached = levelReached;
        if (levelReached > progress.bestLevelReached)
            progress.bestLevelReached = levelReached;
    }
}
