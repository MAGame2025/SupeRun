using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerMotor : MonoBehaviour
{
    [Header("Speed / Acceleration")]
    public float maxSpeed = 8f;
    public float groundAccel = 25f;
    public float groundDecel = 18f;
    public float airAccel = 10f;

    [Header("Gravity / Jump")]
    public float gravity = -24f;
    public float terminalVelocity = -55f;
    public float jumpForce = 9f;

    [Header("Ground Probing")]
    public LayerMask groundMask = ~0;
    public float groundCheckExtra = 0.15f;

    [Header("Mild Grounding (Speed fades stickiness)")]
    [Tooltip("Downward velocity used to keep contact at low speed.")]
    public float stickVelLowSpeed = -2.0f;

    [Tooltip("Downward velocity used to keep contact at high speed (usually near 0).")]
    public float stickVelHighSpeed = -0.15f;

    [Tooltip("Speed at which we start reducing ground stickiness.")]
    public float stickFadeStartSpeed = 6f;

    [Tooltip("Speed at which we reach stickVelHighSpeed.")]
    public float stickFadeEndSpeed = 18f;

    [Header("Crest Detach (Hilltop Pop)")]
    [Tooltip("If ground normal changes fast (crest) and speed is high, we detach.")]
    public float crestMinSpeed = 10f;

    [Tooltip("Minimum degrees change in ground normal to consider it a crest.")]
    public float crestNormalChangeDeg = 7f;

    [Tooltip("How much vertical pop we add when crest detaching.")]
    public float crestPopMultiplier = 0.08f;

    [Tooltip("Maximum extra vertical pop.")]
    public float crestPopMax = 7f;

    [Header("Slope Energy (Gain/Lose Speed By Slope)")]
    [Tooltip("Scale for gravity component applied along the slope (1 = realistic-ish).")]
    public float slopeGravityMultiplier = 1.0f;

    [Tooltip("Minimum slope angle before slope-gravity affects speed.")]
    public float minSlopeGravityAngle = 1.0f;

    [Header("Sliding")]
    public float slideFriction = 6f;
    public float slideAccelMultiplier = 1.25f;
    public float slideMaxSpeedMultiplier = 1.35f;

    [Header("Slide Start")]
    [SerializeField] private float slideStartMinSlopeAngle = 2f;   // allow slide-start on small slopes
    [SerializeField] private float slideStartAccel = 12f;          // shove down-slope when crouch pressed
    [SerializeField] private float slideStartForwardNudge = 1.5f;  // if flat, nudge camera-forward a bit
    [SerializeField] private float slideStartForwardTime = 0.08f;  // short time window feels like a “kick”

    [Header("Ground Normal Influence (gentle)")]
    [Range(0f, 1f)]
    public float groundVelocityProjectionStrength = 0.25f; // 0 = none, 1 = fully project onto plane

    public float groundProjectionMinSpeed = 1.0f;          // don't mess with tiny velocities

    [Header("Uphill Effort (Walk/Run)")]
    [Tooltip("Extra drag only when moving uphill (walking/running).")]
    public float uphillDrag = 3.0f;

    [Tooltip("Slope angle where uphill drag reaches full strength.")]
    public float uphillDragFullAngle = 35f;

    [Header("Sliding Control (Terrain-led)")]
    [Tooltip("How much the player can steer while sliding on flat ground (0..1).")]
    [Range(0f, 1f)] public float slideSteerFlat = 0.55f;

    [Tooltip("How much the player can steer while sliding on steep slopes (0..1).")]
    [Range(0f, 1f)] public float slideSteerSteep = 0.08f;

    [Tooltip("Slope angle considered 'steep' for slide steering falloff.")]
    public float slideSteerSteepAngle = 45f;


    [Header("Debug (read-only)")]
    public float debugSpeed;
    public float debugEnergy;
    public float debugAlongDown;
    public float debugSlopeAngle;
    public bool debugGrounded;
    public bool debugSliding;
    public bool debugCrestDetach;

    private CharacterController controller;

    private Vector3 planarVelocity;
    private float verticalVelocity;

    private bool grounded;
    private Vector3 groundNormal = Vector3.up;

    // Support probe (small, directly under player)
    private bool hasSupport;
    private RaycastHit supportHit;

    // Previous frame surface (for crest detection)
    private Vector3 prevGroundNormal = Vector3.up;
    private bool prevGrounded;

    private float slideStartTimer;
    private bool prevSlideHeld;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void ProcessMove(Vector3 wishDir, float targetSpeed, bool jumpPressed, bool slideHeld)
    {
        ProbeGround();

        // Use our computed grounded state (not just controller.isGrounded)
        grounded = ComputeGrounded();

        // Jump
        if (grounded && jumpPressed)
        {
            verticalVelocity = jumpForce;
            grounded = false;
        }

        // Sliding mode (your "CrouchHeld")
        bool sliding = slideHeld && grounded;

        bool slidePressed = slideHeld && !prevSlideHeld;
        prevSlideHeld = slideHeld;

        if (slidePressed && grounded)
        {
            slideStartTimer = slideStartForwardTime;
        }

        // Desired speed + accel rules
        float speedCap = targetSpeed * (sliding ? slideMaxSpeedMultiplier : 1f);
        speedCap = Mathf.Max(0f, speedCap);

        Vector3 desiredDir = wishDir;
        if (desiredDir.sqrMagnitude > 1f)
            desiredDir.Normalize();

        // If grounded, align input direction to the ground plane to avoid jitter / uphill sticking
        if (grounded && hasSupport)
        {
            Vector3 onPlane = Vector3.ProjectOnPlane(desiredDir, groundNormal);
            if (onPlane.sqrMagnitude > 0.0001f)
                desiredDir = onPlane.normalized;
        }

        Vector3 desiredVel = desiredDir * speedCap;

        // Acceleration (inertia-based)
        if (grounded)
        {
            if (!sliding)
            {
                float accel = (desiredVel.magnitude > planarVelocity.magnitude) ? groundAccel : groundDecel;
                planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVel, accel * Time.deltaTime);
            }
            else
            {
                // Sliding: don't chase desired velocity.
                // Keep momentum, allow mild steering that gets weaker on steeper slopes.

                float speed = planarVelocity.magnitude;

                // If basically stopped, allow desired to start direction (your slide-start also helps)
                Vector3 currentDir = (speed > 0.001f) ? (planarVelocity / speed) : desiredDir;

                // Compute steering strength based on slope angle (flat -> stronger steer, steep -> weaker steer)
                float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                float tSteep = Mathf.Clamp01(slopeAngle / Mathf.Max(0.001f, slideSteerSteepAngle));
                float steerStrength = Mathf.Lerp(slideSteerFlat, slideSteerSteep, tSteep);

                // Also reduce steering when there's no input
                if (desiredDir.sqrMagnitude < 0.001f)
                    steerStrength = 0f;

                Vector3 blendedDir = Vector3.Slerp(currentDir, desiredDir, steerStrength);
                if (blendedDir.sqrMagnitude > 0.0001f)
                    blendedDir.Normalize();

                planarVelocity = blendedDir * speed;
            }
        }
        else
        {
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVel, airAccel * Time.deltaTime);
        }


        // Always apply some friction when grounded (including during slide)
        // BUT: don't constantly brake while the player is actively driving movement (causes uphill jitter/fighting)
        if (grounded)
        {
            bool hasInput = desiredDir.sqrMagnitude >= 0.001f;

            // Apply friction only when there's no input, or when sliding (sliding is friction-driven)
            if (!hasInput || sliding)
            {
                float fric = sliding ? slideFriction : groundDecel;

                // friction is stronger when no input
                if (!hasInput)
                    fric *= 1.25f;

                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, fric * Time.deltaTime);
            }
        }

        // Slope gravity: adds speed downhill, removes speed uphill (based on slope angle)
        if (grounded && hasSupport)
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            debugSlopeAngle = slopeAngle;

            if (slopeAngle >= minSlopeGravityAngle)
            {
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                if (downSlope.sqrMagnitude > 0.0001f)
                {
                    downSlope.Normalize();

                    // component of gravity along slope:
                    float g = Mathf.Abs(gravity);
                    float slopeAccel = g * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slopeGravityMultiplier;

                    if (sliding)
                        slopeAccel *= slideAccelMultiplier;

                    planarVelocity += downSlope * slopeAccel * Time.deltaTime;

                    debugAlongDown = Vector3.Dot(planarVelocity, downSlope);
                }
            }
        }

        // Uphill effort: add mild drag when walking/running uphill (not sliding)
        if (grounded && hasSupport && !sliding)
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            if (slopeAngle > 0.5f && planarVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                if (downSlope.sqrMagnitude > 0.0001f)
                {
                    downSlope.Normalize();

                    // uphillness: 1 when moving straight uphill, 0 when flat/sideways/downhill
                    float uphillness = Mathf.Clamp01(Vector3.Dot(planarVelocity.normalized, -downSlope));

                    // scale by slope angle (0..1)
                    float angleFactor = Mathf.Clamp01(slopeAngle / Mathf.Max(0.001f, uphillDragFullAngle));

                    float drag = uphillDrag * uphillness * angleFactor;

                    // apply as a gentle speed reduction along movement direction
                    planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, drag * Time.deltaTime);
                }
            }
        }


        // Gentle ground-normal influence on velocity direction (camera still leads)
        // Scales stronger downhill, weaker uphill, algorithmically.
        if (grounded && hasSupport && planarVelocity.magnitude >= groundProjectionMinSpeed)
        {
            Vector3 projected = Vector3.ProjectOnPlane(planarVelocity, groundNormal);

            if (projected.sqrMagnitude > 0.0001f)
            {
                float speed = planarVelocity.magnitude;

                Vector3 projDir = projected.normalized;

                // Optional: stronger downhill, weaker uphill (no hard speed if-checks)
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                float downhillness = 0f;

                if (downSlope.sqrMagnitude > 0.001f)
                {
                    downSlope.Normalize();
                    downhillness = Mathf.Clamp01(Vector3.Dot(planarVelocity.normalized, downSlope));
                }

                float strength = Mathf.Lerp(
                    groundVelocityProjectionStrength * 0.7f,
                    groundVelocityProjectionStrength * 1.3f,
                    downhillness);

                Vector3 blendedDir = Vector3.Slerp(planarVelocity.normalized, projDir, strength);
                planarVelocity = blendedDir * speed;
            }
        }

        // Slide start: when crouch is pressed, start moving even from standstill.
        // Prefer down-slope direction if there is any slope, otherwise a small camera-forward nudge.
        if (sliding && hasSupport && (slidePressed || planarVelocity.magnitude < 0.5f))
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            // Down-slope push if on a slope
            if (slopeAngle >= slideStartMinSlopeAngle)
            {
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                if (downSlope.sqrMagnitude > 0.0001f)
                {
                    downSlope.Normalize();
                    planarVelocity += downSlope * slideStartAccel * Time.deltaTime;
                }
            }
            else if (slideStartTimer > 0f)
            {
                // Flat-ish: give a tiny forward nudge so slide begins immediately.
                // Uses desiredDir (camera-relative) if any, otherwise just keep current dir if exists.
                Vector3 forward = (desiredDir.sqrMagnitude > 0.001f) ? desiredDir.normalized :
                                  (planarVelocity.sqrMagnitude > 0.001f ? planarVelocity.normalized : Vector3.zero);

                if (forward.sqrMagnitude > 0.0001f)
                    planarVelocity += forward * slideStartForwardNudge;
            }

            if (slideStartTimer > 0f)
                slideStartTimer -= Time.deltaTime;
        }
        else
        {
            slideStartTimer = 0f;
        }

        // Crest detach: if ground normal changes sharply and we’re fast, unground + pop
        debugCrestDetach = false;
        if (grounded && prevGrounded)
        {
            float speed = planarVelocity.magnitude;
            float normalChange = Vector3.Angle(prevGroundNormal, groundNormal);

            // Only detach if we're not driving INTO the ground
            float intoGround = Vector3.Dot(planarVelocity, groundNormal);

            if (speed >= crestMinSpeed && normalChange >= crestNormalChangeDeg && intoGround > -0.05f)
            {
                float pop = Mathf.Clamp(speed * crestPopMultiplier, 0f, crestPopMax);
                verticalVelocity = Mathf.Max(verticalVelocity, pop);

                grounded = false;
                debugCrestDetach = true;

                // tiny upward nudge so CC doesn't instantly re-ground
                controller.Move(Vector3.up * 0.02f);
            }
        }

        // Gravity / mild grounding
        if (grounded)
        {
            // “Mild stick”: fade downward glue as speed rises
            float spd = planarVelocity.magnitude;
            float t = 0f;
            if (stickFadeEndSpeed > stickFadeStartSpeed)
                t = Mathf.Clamp01((spd - stickFadeStartSpeed) / (stickFadeEndSpeed - stickFadeStartSpeed));

            float stick = Mathf.Lerp(stickVelLowSpeed, stickVelHighSpeed, t);
            verticalVelocity = stick;
        }
        else
        {
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, terminalVelocity);
        }

        // Move
        Vector3 motion = (planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(motion);

        // Update previous surface info AFTER move
        prevGrounded = grounded;
        prevGroundNormal = groundNormal;

        // Debug
        debugGrounded = grounded;
        debugSliding = sliding;
        debugSpeed = planarVelocity.magnitude;
        debugEnergy = 0.5f * debugSpeed * debugSpeed;
    }

    private void ProbeGround()
    {
        hasSupport = false;
        groundNormal = Vector3.up;

        // Center in world space
        Vector3 ccCenter = transform.TransformPoint(controller.center);

        // Support probe: detects ground directly under the player
        float supportRadius = Mathf.Max(0.05f, controller.radius * 0.6f);
        float supportDist = (controller.height * 0.5f) + groundCheckExtra + 0.15f;

        Vector3 origin = ccCenter + Vector3.up * 0.05f;

        if (Physics.SphereCast(
                origin,
                supportRadius,
                Vector3.down,
                out supportHit,
                supportDist,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            hasSupport = true;
            groundNormal = supportHit.normal;
        }
    }

    private bool ComputeGrounded()
    {
        if (!hasSupport)
            return false;

        // Dont become grounded if we are moving upward
        if (verticalVelocity > 0.2f)
            return false;

        // Require the support hit to be close (prevents re-grounding while effectively airborne)
        // Tune this if needed, but keep it small to avoid "magnet feet"
        float closeEnough = 0.25f;
        if (supportHit.distance > closeEnough)
            return false;

        // Respect the CharacterController's slope limit (prevents "sticky" behavior on too-steep surfaces)
        float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
        if (slopeAngle > controller.slopeLimit)
            return false;

        return true;
    }
}
