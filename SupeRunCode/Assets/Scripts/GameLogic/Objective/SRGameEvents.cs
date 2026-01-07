using System;

public static class SRGameEvents
{
    public static event Action EnemyKilled;
    public static event Action PlayerDied;

    public static void RaiseEnemyKilled() => EnemyKilled?.Invoke();
    public static void RaisePlayerDied() => PlayerDied?.Invoke();
}
