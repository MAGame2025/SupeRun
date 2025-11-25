using UnityEngine;

[CreateAssetMenu(menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Prefab")]
    public Enemy prefab;

    [Header("Stats")]
    public float moveSpeed = 5f;

    // You can add HP, damage, etc later if you want.
}
