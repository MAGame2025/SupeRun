using UnityEngine;

public class SRModelGroundAligner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SRPlayerMotor motor;
    [SerializeField] private Transform model;
    [Tooltip("Usually the player root. We rotate this for yaw (movement-based).")]
    [SerializeField] private Transform yawSource;

    [Header("Near-Ground Window (for takeoff/landing tilt)")]
    [SerializeField] private float tiltStartDistance = 0.9f;
    [SerializeField] private float tiltStopDistance = 1.4f;

    [Header("Smoothing")]
    [SerializeField] private float tiltLerpSpeed = 18f;
    [SerializeField] private float uprightLerpSpeed = 14f;

    [Header("Limits")]
    [SerializeField] private float maxTiltAngle = 55f;

    [Header("Yaw (movement-based)")]
    [SerializeField] private bool faceMovementDirection = true;
    [SerializeField] private float yawLerpSpeed = 16f;
    [SerializeField] private float minSpeedToTurn = 0.15f;

    // Internal hysteresis state for "near ground" tilt window.
    private bool nearGroundWindowActive;

    private void Reset()
    {
        yawSource = transform;
    }

    private void LateUpdate()
    {
        if (motor == null || model == null)
            return;

        if (yawSource == null)
            yawSource = transform;

        // -------------------------
        // 1) Movement-based yaw
        // -------------------------
        Vector3 yawForward = yawSource.forward;

        if (faceMovementDirection)
        {
            Vector3 v = motor.PlanarVelocity;
            v.y = 0f;

            if (v.magnitude >= minSpeedToTurn)
            {
                Quaternion targetYaw = Quaternion.LookRotation(v.normalized, Vector3.up);
                yawSource.rotation = Quaternion.Slerp(
                    yawSource.rotation,
                    targetYaw,
                    1f - Mathf.Exp(-yawLerpSpeed * Time.deltaTime)
                );
            }

            yawForward = yawSource.forward;
        }

        // -------------------------------------------------
        // 2) Near-ground window (VISUAL probe, hysteresis)
        // -------------------------------------------------
        bool nearGroundNow = motor.VisualGroundContact && motor.VisualGroundDistance <= tiltStartDistance;

        if (nearGroundWindowActive)
        {
            bool farNow = !motor.VisualGroundContact || motor.VisualGroundDistance >= tiltStopDistance;
            if (farNow)
                nearGroundWindowActive = false;
        }
        else
        {
            if (nearGroundNow)
                nearGroundWindowActive = true;
        }

        // ---------------------------------------------------------
        // 3) TiltActive rule (NEVER upright while grounded)
        // ---------------------------------------------------------
        bool tiltActive = motor.CCGrounded || nearGroundWindowActive;

        // Only go upright when truly airborne AND not in the near-ground window.
        if (!tiltActive)
        {
            Quaternion upright = Quaternion.Euler(0f, yawSource.eulerAngles.y, 0f);
            model.rotation = Quaternion.Slerp(
                model.rotation,
                upright,
                1f - Mathf.Exp(-uprightLerpSpeed * Time.deltaTime)
            );
            return;
        }

        // ---------------------------------------------------------
        // 4) Choose the normal for tilting (tilt probe only)
        // ---------------------------------------------------------
        // For visuals, always use the visual probe normal (stable for takeoff/landing + slopes).
        Vector3 n = motor.VisualGroundNormal;

        // Safety: if something went wrong and the normal is near-zero, default to up.
        if (n.sqrMagnitude < 0.0001f)
            n = Vector3.up;

        // Clamp tilt angle to avoid extreme leaning on weird normals.
        float angle = Vector3.Angle(n, Vector3.up);
        if (angle > maxTiltAngle)
        {
            float t = maxTiltAngle / angle;
            n = Vector3.Slerp(Vector3.up, n, t);
        }

        // ---------------------------------------------------------
        // 5) Build a rotation that preserves yaw but tilts to ground
        // ---------------------------------------------------------
        Vector3 forward = Vector3.ProjectOnPlane(yawForward, n);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(yawSource.right, n);

        forward.Normalize();

        Quaternion target = Quaternion.LookRotation(forward, n);

        model.rotation = Quaternion.Slerp(
            model.rotation,
            target,
            1f - Mathf.Exp(-tiltLerpSpeed * Time.deltaTime)
        );
    }
}
