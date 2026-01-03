using UnityEngine;

public class SRPlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputReader input;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private SRPlayerMotor motor;

    [Header("Jump")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    private float jumpBufferTimer;

    [Header("Forced Steep-Slope Slide")]
    [Tooltip("How long we force slide when standing on a slope steeper than MaxWalkSlopeAngle.")]
    [SerializeField] private float forcedSlideDuration = 1.0f;

    [Tooltip("We re-check slope at (duration - this). Example 0.05 means re-check at 0.95s.")]
    [SerializeField] private float forcedSlideRecheckLead = 0.05f;

    private bool forcedSlideActive;
    private float forcedSlideTimer;

    private void Reset()
    {
        motor = GetComponent<SRPlayerMotor>();
    }

    void Update()
    {
        if (input == null || cameraTransform == null || motor == null)
            return;

        float dt = Time.deltaTime;

        // NEW: make sure GroundAngle / GroundContact are CURRENT this frame
        motor.ControllerPreProbe();
        Debug.Log($"Angle={motor.GroundAngle:F1}, MaxWalk={motor.MaxWalkSlopeAngle:F1}, forced={forcedSlideActive}");

        Vector3 desiredDir = GetCameraRelativeMove(input.Move, cameraTransform);
        bool runHeld = input.RunHeld;
        bool slideHeldInput = input.CrouchHeld;

        // --- Jump buffer ---
        if (input.JumpPressed)
            jumpBufferTimer = jumpBufferTime;

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= dt;

        // ----------------------------------------------------
        // Forced slide on too-steep slopes (above max walk slope)
        // ----------------------------------------------------
        bool hasGroundContact = motor.GroundContact; // stable probe contact
        bool tooSteep = hasGroundContact && (motor.GroundAngle > motor.MaxWalkSlopeAngle);

        if (!hasGroundContact)
        {
            // If we're not actually on ground contact (jumped / airborne), cancel forced slide.
            forcedSlideActive = false;
            forcedSlideTimer = 0f;
        }
        else if (tooSteep)
        {
            if (!forcedSlideActive)
            {
                forcedSlideActive = true;
                forcedSlideTimer = forcedSlideDuration;
            }
            else
            {
                forcedSlideTimer -= dt;

                // Re-check at ~0.95s (i.e., when timer drops below lead window)
                float recheckThreshold = Mathf.Max(0f, forcedSlideRecheckLead);
                if (forcedSlideTimer <= recheckThreshold)
                {
                    // Still too steep? restart the 1s window. Otherwise release.
                    if (motor.GroundAngle > motor.MaxWalkSlopeAngle)
                    {
                        forcedSlideTimer = forcedSlideDuration;
                    }
                    else
                    {
                        forcedSlideActive = false;
                        forcedSlideTimer = 0f;
                    }
                }
            }
        }
        else
        {
            // Normal walkable ground -> release forced slide immediately
            forcedSlideActive = false;
            forcedSlideTimer = 0f;
        }

        bool slideHeldEffective = slideHeldInput || forcedSlideActive;

        motor.Tick(desiredDir, runHeld, slideHeldEffective, dt);

        // --- Consume buffered jump if possible ---
        if (jumpBufferTimer > 0f)
        {
            if (motor.TryJump(slideHeldEffective))
                jumpBufferTimer = 0f;
        }
    }

    private static Vector3 GetCameraRelativeMove(Vector2 move, Transform cam)
    {
        if (move.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 fwd = cam.forward;
        Vector3 right = cam.right;

        //remove y axis input from camera
        fwd.y = 0f; right.y = 0f;

        //normalize so that move direction doesnt affect speed
        fwd.Normalize(); right.Normalize();

        Vector3 desired = (fwd * move.y) + (right * move.x);
        if (desired.sqrMagnitude > 1f)
            desired.Normalize();
        return desired;
    }
}
