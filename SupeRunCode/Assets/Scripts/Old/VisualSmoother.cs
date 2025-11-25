using UnityEngine;

/// Responsibilities: Interpolating the visual mesh to remove FixedUpdate jitter.
/// Usage: Attach to the Child Object containing the Mesh/Camera.
/// 


public class SRVisualSmoother : MonoBehaviour
{
    private Transform physicsTarget; // Assign the Player Parent here
    private float rotationSpeed = 20f;

    private void LateUpdate()
    {
        if (physicsTarget == null) return;

        // 1. Position Interpolation
        // Smoothly move towards the physics ticks. 
        // A factor of 25-30 is usually snappy enough to feel responsive but smooth enough to hide 50Hz jitter
        transform.position = Vector3.Lerp(transform.position, physicsTarget.position, 30f * Time.deltaTime);

        // 2. Rotation Smoothing (Critique Fix: Slerp)
        // We handle rotation here on the visual mesh, not the collider
        Vector3 velocity = physicsTarget.GetComponent<SRPlayerMotor>().PlanarVelocity;

        if (velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}