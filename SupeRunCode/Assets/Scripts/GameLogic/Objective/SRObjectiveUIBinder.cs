using UnityEngine;

public class SRObjectiveUIBinder : MonoBehaviour
{
    [SerializeField] private SRObjectiveUI objectiveUI;
    [SerializeField] private SRWinUI winUI;

    private void Awake()
    {
        if (objectiveUI == null)
            objectiveUI = GetComponentInChildren<SRObjectiveUI>(true);

        if (winUI == null)
            winUI = GetComponentInChildren<SRWinUI>(true);
    }

    private void Start()
    {
        SRObjectiveManager mgr = FindFirstObjectByType<SRObjectiveManager>();
        if (mgr == null)
        {
            Debug.LogWarning("SRObjectiveUIBinder: No SRObjectiveManager found in scene.");
            return;
        }

        if (objectiveUI != null)
            mgr.RegisterUI(objectiveUI);

        if (winUI != null)
            mgr.RegisterWinUI(winUI);
    }
}
