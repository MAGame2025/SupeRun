using UnityEngine;

[CreateAssetMenu(menuName = "SupeRun/Objectives/Kill Count", order = 2)]
public class SRObjectiveKillCount : SRObjective
{
    [SerializeField] private int killsRequired = 50;

    private int kills;

    public override string Title => "Eliminate";
    public override string GetProgressText()
    {
        int clamped = Mathf.Clamp(kills, 0, killsRequired);
        return $"{clamped}/{killsRequired}";
    }

    public override void Init()
    {
        kills = 0;
        SRGameEvents.EnemyKilled += OnEnemyKilled;
    }

    public override void Cleanup()
    {
        SRGameEvents.EnemyKilled -= OnEnemyKilled;
    }

    private void OnEnemyKilled()
    {
        kills++;
        if (kills >= killsRequired)
            MarkCompleted();
    }
}
