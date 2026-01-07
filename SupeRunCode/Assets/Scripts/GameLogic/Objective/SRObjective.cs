using System;
using UnityEngine;

public abstract class SRObjective : ScriptableObject
{
    public event Action Completed;

    public abstract string Title { get; }
    public abstract string GetProgressText();

    // Called when this objective becomes active.
    public virtual void Init() { }

    // Called when objective stops being active (completed/aborted/level end).
    public virtual void Cleanup() { }

    // Optional ticking (survive timer uses it).
    public virtual void Tick(float dt) { }

    protected void MarkCompleted()
    {
        Completed?.Invoke();
    }
}
