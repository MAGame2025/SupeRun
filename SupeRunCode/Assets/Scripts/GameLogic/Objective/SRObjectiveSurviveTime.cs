using UnityEngine;

[CreateAssetMenu(menuName = "SupeRun/Objectives/Survive Time", order = 1)]
public class SRObjectiveSurviveTime : SRObjective
{
    [SerializeField] private float secondsToSurvive = 60f;

    private float elapsed;

    public override string Title => "Survive";
    public override string GetProgressText()
    {
        float remaining = Mathf.Max(0f, secondsToSurvive - elapsed);
        return $"{remaining:0}s";
    }

    public override void Init()
    {
        elapsed = 0f;
    }

    public override void Tick(float dt)
    {
        elapsed += dt;
        if (elapsed >= secondsToSurvive)
            MarkCompleted();
    }
}
