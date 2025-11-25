using UnityEngine;

public static class SpawnPositions
{
    // Use this as "failed" value if needed
    public static readonly Vector3 INVALID_POS = new Vector3(float.NaN, float.NaN, float.NaN);

    // Simple: spawn in a ring around the player
    public static Vector3 GetEnemySpawnPosition(EnemyData enemyData, Transform player)
    {
        if (player == null || enemyData == null)
            return INVALID_POS;

        float radius = 20f; // tweak as you like

        Vector2 circle = Random.insideUnitCircle.normalized;
        Vector3 offset = new Vector3(circle.x, 0f, circle.y) * radius;

        Vector3 position = player.position + offset;
        return position;
    }
}
