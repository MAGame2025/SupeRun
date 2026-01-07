using UnityEngine;

public class FrictionModifier : MonoBehaviour
{
    [Header("Multipliers")]
    [Tooltip("Scales movement force while on this surface.")]
    public float moveForceMultiplier = 1f;

    [Tooltip("Scales counter-movement braking while on this surface.")]
    public float counterMoveMultiplier = 1f;

    [Tooltip("Optional: scales max speed while on this surface.")]
    public float maxSpeedMultiplier = 1f;
}
