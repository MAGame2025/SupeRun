using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private float currentSpeed;
    public float CurrentSpeed => currentSpeed;

    [SerializeField] private bool isGrounded;

    [Header("Core Physics")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float terminalVelocity = -50f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float maxWalkSlope = 55f;

    [Header("Movement Smoothing")]
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 60f;
    [SerializeField] private float airAcceleration = 25f;

    [Header("Ground Vs Camera strength")]
    [SerializeField, Range(0f, 1f)]
    private float groundProjectionStrength = 0.35f; // 0 = never project, 1 = full project

    [Header("Bunnyhop")]
    [SerializeField] private bool preserveBunnyhopSpeedOnLanding = true;
    [SerializeField] private float bunnyhopLandingGrace = 0.12f;
    [SerializeField] private float bunnyhopBrakeDecel = 80f;

    [Header("Slide Physics")]
    [SerializeField] private float slideFriction = 2f;
    [SerializeField] private float slideSlopeAccel = 25f;
    [SerializeField] private float minSlideSlopeAngle = 5f;

    [Header("Slope Friction Scaling")]
    [SerializeField] private float slopeFrictionExtraAtMaxAngle = 8f;
    [SerializeField] private float slopeFrictionMaxAngle = 60f;
    [SerializeField] private AnimationCurve slopeFrictionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float downhillFrictionMultiplier = 0.35f; // less friction downhill

    [Header("Slide Air Handling")]
    [SerializeField] private bool preserveSlideMomentumInAir = true;
    [SerializeField] private float slideAirSteer = 0.35f;

    [Header("Grounded Slide Steering")]
    [SerializeField] private float slideGroundSteer = 0.5f;

    [Header("Ramp Launch")]
    [SerializeField] private bool enableRampLaunch = true;
    [SerializeField] private float rampLaunchMinSpeed = 14f;
    [SerializeField] private float rampLaunchMinSlopeAngle = 8f;
    [SerializeField] private float rampLaunchVerticalMultiplier = 0.95f;
    [SerializeField] private float rampLaunchMaxVertical = 14f;

    [Header("Uphill Mode = NEVER GROUNDED")]
    [SerializeField] private bool slideUphillNeverGrounded = true;
    [SerializeField] private float uphillNeverGroundedMinSpeed = 6f;
    [SerializeField] private float uphillNeverGroundedMinSlope = 3f;
    [Tooltip("While forced-airborne uphill, clamp downward velocity so you don't get sucked into the slope.")]
    [SerializeField] private float uphillStickDownClamp = -3f;

    [Header("Detaching / Ground Stickiness")]
    [SerializeField] private float groundedStickVelocity = -2f; // was hardcoded -5
    [SerializeField] private float detachMinSlopeAngle = 1.5f;  // allow detach on smaller slopes
    [SerializeField] private float detachCrestDot = 0.15f;      // near-crest = detach sooner

    [Header("Grounding Policy (Downhill-only while sliding)")]
    [Tooltip("When sliding, stay grounded ONLY if your planar motion goes downhill (decreasing Y). Flat/uphill forces airborne.")]
    [SerializeField] private bool downhillOnlyGroundedWhileSliding = true;
    [Tooltip("Deadzone for deciding downhill vs flat (dot with down-slope). Lower = more sensitive.")]
    [SerializeField] private float downhillDotEpsilon = 0.05f;
    [Tooltip("Ignore micro-bumps; below this angle we treat it as flat for the policy.")]
    [SerializeField] private float downhillMinSlopeAngle = 0.75f;

    [Header("Uphill/Downhill State Noise Filtering")]
    [Tooltip("Dot threshold (m/s) to decide uphill vs downhill. Higher = less state flicker near flat.")]
    [SerializeField] private float uphillDownhillDotThreshold = 0.75f;

    [Header("External Forces")]
    [SerializeField] private float externalPushDamping = 5f;
    [SerializeField] private float pushCooldown = 2f;

    [Header("Debug – Speed Clamp")]
    [SerializeField] private bool debugClampMaxSpeed = false;
    [SerializeField] private float debugMaxPlanarSpeed = 20f;

    // =========================
    // SLOPE STATES
    // =========================
    private enum SlideSlopeState { Flat, Downhill, Uphill }

    [Header("Debug – Slide State")]
    [SerializeField] private bool debugIsSlidingFrame;
    [SerializeField] private SlideSlopeState debugSlopeState;
    [SerializeField] private float debugSlopeAngle;
    [SerializeField] private float debugAlongDown;

    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }
    public bool IsGrounded => isGrounded;

    private float slideAlongDown;

    public Vector3 GroundNormal { get; private set; }
    public Vector3 HorizontalVelocity => PlanarVelocity;

    private CharacterController controller;

    private float jumpCooldownTimer;
    private float bunnyhopGraceTimer;

    private Vector3 wallNormal;
    private bool isTouchingWall;

    private Vector3 externalPlanarVelocity;
    private float nextAllowedPushTime;

    // Ground probe result (separate from grounded state)
    private bool hasGroundHit;
    private RaycastHit groundHit;

    // "support" probe (only what's directly under the player)
    private bool hasSupportHit;
    private RaycastHit supportHit;
    private Vector3 supportNormal = Vector3.up;

    // Transition tracking
    private bool prevGrounded;

    // Sliding context
    private bool slideFrameActive;
    private float slideSlopeAngle;
    private Vector3 slideDownSlope;
    private SlideSlopeState slideSlopeState = SlideSlopeState.Flat;

    // Ramp launch caching (from last “groundish” frame)
    private float lastGroundSlopeAngle;
    private Vector3 lastDownSlope;

    // One-shot ramp launch latch for uphill "never grounded" mode
    private bool wasForceAirborneUphill = false;
    private bool rampLaunchUsedThisUphill = false;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GroundNormal = Vector3.up;
    }

    private void Update()
    {
        currentSpeed = PlanarVelocity.magnitude;

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        if (bunnyhopGraceTimer > 0f)
            bunnyhopGraceTimer -= Time.deltaTime;

        if (externalPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity, Vector3.zero, externalPushDamping * Time.deltaTime);
        }
    }

    public void ProcessMove(Vector3 wishDir, float targetSpeed, bool isAirborne)
    {
        GroundProbe();

        slideFrameActive = false;
        debugIsSlidingFrame = false;

        if (isGrounded)
        {
            if (preserveBunnyhopSpeedOnLanding && bunnyhopGraceTimer > 0f && wishDir.sqrMagnitude > 0.0001f)
            {
                float speed = PlanarVelocity.magnitude;

                if (speed > targetSpeed + 0.01f)
                {
                    Vector3 velDir = PlanarVelocity.normalized;
                    float dot = Vector3.Dot(velDir, wishDir.normalized);

                    if (dot > 0f)
                    {
                        Vector3 desired = wishDir.normalized * speed;
                        PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, desired, groundAcceleration * Time.deltaTime);
                    }
                    else
                    {
                        Vector3 desired = wishDir.normalized * targetSpeed;
                        PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, desired, bunnyhopBrakeDecel * Time.deltaTime);
                    }
                }
                else
                {
                    float accel = targetSpeed > speed ? groundAcceleration : groundDeceleration;
                    PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, wishDir * targetSpeed, accel * Time.deltaTime);
                }
            }
            else
            {
                float speed = PlanarVelocity.magnitude;
                float accel = targetSpeed > speed ? groundAcceleration : groundDeceleration;
                PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, wishDir * targetSpeed, accel * Time.deltaTime);
            }
        }
        else
        {
            ApplyAirPhysics(wishDir, targetSpeed);
        }

        ApplyGravityAndMove();
        isTouchingWall = false;
    }

    public void ProcessSlidePhysics(Vector3 wishDir)
    {
        GroundProbe();

        slideFrameActive = true;
        debugIsSlidingFrame = true;

        slideDownSlope = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
        slideSlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);

        debugSlopeAngle = slideSlopeAngle;

        // Determine uphill/downhill based on velocity vs down-slope direction
        float alongDown = (slideDownSlope.sqrMagnitude > 0.0001f) ? Vector3.Dot(PlanarVelocity, slideDownSlope) : 0f;
        debugAlongDown = alongDown;
        slideAlongDown = alongDown;

        // Noise filter near 0
        if (slideSlopeAngle < minSlideSlopeAngle || slideDownSlope.sqrMagnitude < 0.0001f)
        {
            slideSlopeState = SlideSlopeState.Flat;
        }
        else if (alongDown >= uphillDownhillDotThreshold)
        {
            slideSlopeState = SlideSlopeState.Downhill;
        }
        else if (alongDown <= -uphillDownhillDotThreshold)
        {
            slideSlopeState = SlideSlopeState.Uphill;
        }
        else
        {
            slideSlopeState = SlideSlopeState.Flat;
        }

        debugSlopeState = slideSlopeState;

        // Cache surface for ramp launch (even though we might force-airborne)
        lastGroundSlopeAngle = slideSlopeAngle;
        lastDownSlope = slideDownSlope;

        // If we are not grounded, treat like air-slide
        if (!isGrounded)
        {
            if (preserveSlideMomentumInAir && wishDir.sqrMagnitude > 0.1f)
            {
                Vector3 target = wishDir.normalized * PlanarVelocity.magnitude;
                PlanarVelocity = Vector3.MoveTowards(
                    PlanarVelocity, target, slideAirSteer * airAcceleration * Time.deltaTime);
            }

            ApplyGravityAndMove();
            return;
        }

        // Apply friction ALWAYS while grounded sliding
        float friction = GetEffectiveSlideFriction(slideSlopeAngle);
        float frictionMult = (slideSlopeState == SlideSlopeState.Downhill) ? downhillFrictionMultiplier : 1f;
        float spd = PlanarVelocity.magnitude;
        spd = Mathf.MoveTowards(spd, 0f, friction * frictionMult * Time.deltaTime);
        PlanarVelocity = spd > 0.01f ? PlanarVelocity.normalized * spd : Vector3.zero;

        // Then add downhill acceleration if downhill
        if (slideSlopeState == SlideSlopeState.Downhill)
        {
            Vector3 accelDir = new Vector3(slideDownSlope.x, 0f, slideDownSlope.z).normalized;
            PlanarVelocity += accelDir * slideSlopeAccel * Time.deltaTime;
        }

        // Steering
        if (wishDir.sqrMagnitude > 0.1f)
        {
            Vector3 targetVel = wishDir.normalized * PlanarVelocity.magnitude;
            PlanarVelocity = Vector3.MoveTowards(
                PlanarVelocity, targetVel, slideGroundSteer * airAcceleration * Time.deltaTime);
        }

        ApplyGravityAndMove();
    }

    private void ApplyAirPhysics(Vector3 wishDir, float targetSpeed)
    {
        if (isTouchingWall && Vector3.Dot(wishDir, wallNormal) < 0f)
            wishDir = Vector3.ProjectOnPlane(wishDir, wallNormal).normalized;

        float projSpeed = Vector3.Dot(PlanarVelocity, wishDir);
        float addSpeed = targetSpeed - projSpeed;

        if (addSpeed > 0f)
        {
            float accelSpeed = airAcceleration * targetSpeed * Time.deltaTime;
            accelSpeed = Mathf.Min(accelSpeed, addSpeed);
            PlanarVelocity += wishDir * accelSpeed;
        }
    }

    public void ForceJump(float force)
    {
        VerticalVelocity = force;
        isGrounded = false;
        jumpCooldownTimer = 0.2f;

        // Reset uphill one-shot so jump doesn't inherit it weirdly
        wasForceAirborneUphill = false;
        rampLaunchUsedThisUphill = false;

        controller.Move(Vector3.up * 0.05f);
    }

    private void ApplyGravityAndMove()
    {
        ApplyDebugSpeedClamp();

        Vector3 planar = PlanarVelocity + externalPlanarVelocity;

        // =====================================================
        // Uphill = NEVER GROUNDED (forced airborne)
        // =====================================================
        bool forceAirborneUphill =
            slideFrameActive &&
            slideUphillNeverGrounded &&
            slideSlopeAngle >= detachMinSlopeAngle &&
            planar.magnitude >= uphillNeverGroundedMinSpeed &&
            (
                slideSlopeState == SlideSlopeState.Uphill ||
                (slideSlopeAngle >= uphillNeverGroundedMinSlope && slideAlongDown <= detachCrestDot)
            );

        // =====================================================
        // Downhill-only grounding policy while sliding:
        // grounded ONLY if our planar motion goes downhill (decreasing Y).
        // flat/uphill => force airborne.
        // =====================================================
        bool forceUngroundedByPolicy = false;
        if (downhillOnlyGroundedWhileSliding && slideFrameActive)
        {
            forceUngroundedByPolicy = !IsMovingDownhill(planar);
        }

        bool forceAirborne = forceAirborneUphill || forceUngroundedByPolicy;

        // One-shot ramp launch: only when we ENTER uphill-airborne mode (not for flat-policy airborne)
        if (forceAirborneUphill)
        {
            if (!wasForceAirborneUphill)
            {
                // entering this mode now
                rampLaunchUsedThisUphill = false;
            }

            if (!rampLaunchUsedThisUphill)
            {
                // only if we were actually grounded before forcing airborne
                if (isGrounded)
                {
                    TryApplyRampLaunch(planar);
                    rampLaunchUsedThisUphill = true;
                }
            }
        }
        else
        {
            // leaving the mode allows a new one-shot next time
            rampLaunchUsedThisUphill = false;
        }

        // Gravity / glue
        if (!forceAirborne && isGrounded)
        {
            VerticalVelocity = groundedStickVelocity;
        }
        else
        {
            VerticalVelocity = Mathf.Max(VerticalVelocity + gravity * Time.deltaTime, terminalVelocity);

            // Prevent “sucked into ramp” feel (only for uphill airborne)
            if (forceAirborneUphill)
                VerticalVelocity = Mathf.Max(VerticalVelocity, uphillStickDownClamp);
        }

        // Only project when truly grounded and NOT forced airborne
        if (isGrounded && !forceAirborne)
        {
            Vector3 projected = Vector3.ProjectOnPlane(planar, GroundNormal);
            planar = Vector3.Lerp(planar, projected, groundProjectionStrength);
        }

        // Move
        controller.Move((planar + Vector3.up * VerticalVelocity) * Time.deltaTime);

        // Resolve grounded for next frame
        ResolveGroundAfterMove(forceAirborne);

        // update latch AFTER we compute this frame
        wasForceAirborneUphill = forceAirborneUphill;

        slideFrameActive = false;
    }

    private void ResolveGroundAfterMove(bool forceAirborne)
    {
        prevGrounded = isGrounded;

        if (jumpCooldownTimer > 0f)
        {
            isGrounded = false;
            return;
        }

        if (forceAirborne)
        {
            isGrounded = false;
            return;
        }

        bool ccGrounded = controller != null && controller.isGrounded;
        bool probeGrounded = (hasSupportHit || hasGroundHit) && VerticalVelocity <= 0f;

        isGrounded = ccGrounded || probeGrounded;

        if (!prevGrounded && isGrounded)
            bunnyhopGraceTimer = bunnyhopLandingGrace;
    }

    private void TryApplyRampLaunch(Vector3 planarAtTakeoff)
    {
        if (!enableRampLaunch)
            return;

        float speed = planarAtTakeoff.magnitude;
        if (speed < rampLaunchMinSpeed)
            return;

        if (lastGroundSlopeAngle < rampLaunchMinSlopeAngle)
            return;

        if (lastDownSlope.sqrMagnitude < 0.0001f)
            return;

        float alongDown = Vector3.Dot(planarAtTakeoff, lastDownSlope);
        bool movingUphill = (alongDown < 0f);
        if (!movingUphill)
            return;

        float vertical =
            speed *
            Mathf.Sin(lastGroundSlopeAngle * Mathf.Deg2Rad) *
            rampLaunchVerticalMultiplier;

        vertical = Mathf.Clamp(vertical, 0f, rampLaunchMaxVertical);
        if (vertical <= 0.01f)
            return;

        VerticalVelocity = Mathf.Max(VerticalVelocity, vertical);

        // Tiny detach nudge so CC doesn't immediately re-ground
        controller.Move(Vector3.up * 0.02f);
    }

    private void GroundProbe()
    {
        hasGroundHit = false;
        hasSupportHit = false;

        GroundNormal = Vector3.up;
        supportNormal = Vector3.up;

        if (controller == null)
            return;

        // CharacterController center in world space (more correct than transform.position)
        Vector3 ccCenter = transform.TransformPoint(controller.center);

        // ---- Support probe (small): ONLY detects ground directly under the player ----
        float supportRadius = Mathf.Max(0.02f, controller.radius * 0.2f);
        float supportDist = (controller.height * 0.5f) + groundCheckDistance + 0.2f;

        Vector3 supportOrigin = ccCenter + Vector3.up * 0.05f;

        if (Physics.SphereCast(
            supportOrigin,
            supportRadius,
            Vector3.down,
            out supportHit,
            supportDist,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            hasSupportHit = true;
            supportNormal = supportHit.normal;
        }

        // ---- Wide probe (big): used ONLY for grounding detection ----
        float wideRadius = controller.radius * 0.9f;
        float wideDist = groundCheckDistance + 0.3f;

        // Start just above the bottom hemisphere so we still catch ground reliably
        Vector3 wideOrigin = ccCenter + Vector3.up * (controller.radius + 0.05f);

        if (Physics.SphereCast(
            wideOrigin,
            wideRadius,
            Vector3.down,
            out groundHit,
            wideDist,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            hasGroundHit = true;
        }

        // Use support normal for movement projection whenever possible
        GroundNormal = hasSupportHit ? supportNormal : Vector3.up;
    }

    private bool IsMovingDownhill(Vector3 planar)
    {
        if (!hasSupportHit)
            return false;

        float angle = Vector3.Angle(supportNormal, Vector3.up);
        if (angle < downhillMinSlopeAngle)
            return false;

        Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, supportNormal);
        if (downSlope.sqrMagnitude < 0.0001f)
            return false;

        downSlope.Normalize();

        float alongDown = Vector3.Dot(planar, downSlope);
        return alongDown > downhillDotEpsilon;
    }

    private float GetEffectiveSlideFriction(float slopeAngle)
    {
        float t = 0f;
        if (slopeFrictionMaxAngle > 0.01f)
            t = Mathf.Clamp01(slopeAngle / slopeFrictionMaxAngle);

        float shaped = t;
        if (slopeFrictionCurve != null)
            shaped = Mathf.Clamp01(slopeFrictionCurve.Evaluate(t));

        return slideFriction + slopeFrictionExtraAtMaxAngle * shaped;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isGrounded && hit.normal.y < 0.1f)
        {
            isTouchingWall = true;
            wallNormal = hit.normal;
        }
    }

    public void ApplySlideVelocity(Vector3 velocity)
    {
        PlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
    }

    public void ApplyExternalPush(Vector3 push)
    {
        if (Time.time < nextAllowedPushTime) return;

        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f) return;

        externalPlanarVelocity += push;
        nextAllowedPushTime = Time.time + pushCooldown;
    }

    private void ApplyDebugSpeedClamp()
    {
        if (!debugClampMaxSpeed)
            return;

        float speed = PlanarVelocity.magnitude;
        if (speed > debugMaxPlanarSpeed)
            PlanarVelocity = PlanarVelocity.normalized * debugMaxPlanarSpeed;
    }
}



