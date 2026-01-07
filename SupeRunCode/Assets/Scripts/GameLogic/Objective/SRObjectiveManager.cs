using System.Collections.Generic;
using UnityEngine;

public class SRObjectiveManager : MonoBehaviour
{
    [Header("Objective List (in order)")]
    [SerializeField] private List<SRObjective> objectives = new List<SRObjective>();

    [Header("Optional UI Hook")]
    [SerializeField] private SRObjectiveUI ui;

    [SerializeField] private SRWinUI winUI;

    private bool levelCompleted;
    private int index = -1;
    private SRObjective current;

    private void Start()
    {
        StartNextObjective();
    }

    private void Update()
    {
        if (current == null)
            return;

        current.Tick(Time.deltaTime);

        if (ui != null)
            ui.Set(current.Title, current.GetProgressText());
    }

    private void StartNextObjective()
    {
        CleanupCurrent();

        index++;
        if (index >= objectives.Count)
        {
            OnAllObjectivesCompleted();
            return;
        }

        current = objectives[index];
        if (current == null)
        {
            StartNextObjective();
            return;
        }

        current.Completed += HandleObjectiveCompleted;
        current.Init();

        if (ui != null)
            ui.Set(current.Title, current.GetProgressText());
    }

    private void HandleObjectiveCompleted()
    {
        StartNextObjective();
    }

    private void CleanupCurrent()
    {
        if (current == null)
            return;

        current.Completed -= HandleObjectiveCompleted;
        current.Cleanup();
        current = null;
    }
    public void RegisterUI(SRObjectiveUI newUI)
    {
        ui = newUI;

        // Immediately push current state so UI shows something right away
        if (ui != null && current != null)
            ui.Set(current.Title, current.GetProgressText());
    }

    private void OnDestroy()
    {
        CleanupCurrent();
    }
    private void OnAllObjectivesCompleted()
    {
        Debug.Log("SRObjectiveManager: All objectives completed (level clear).");

        levelCompleted = true;
        SRRunState.MarkLevelComplete();

        // Disable enemy AI (your plan)
        if (SREnemyManager.Instance != null)
            SREnemyManager.Instance.SetSimulationEnabled(false);

        if (winUI != null)
            winUI.Show();
    }


    public void RegisterWinUI(SRWinUI newWinUI)
    {
        winUI = newWinUI;

        // If objectives already finished before UI existed, show immediately.
        if (levelCompleted && winUI != null)
            winUI.Show();
    }


}
