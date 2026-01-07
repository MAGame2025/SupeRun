using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class RBPlayerMovement : MonoBehaviour
{
    /* =========================================================
     * COMPONENTS
     * ========================================================= */
    private Rigidbody rb;
    private CapsuleCollider capsule;

    /* =========================================================
     * INPUT (set externally, e.g. by your InputReader)
     * ========================================================= */
    [Header("Input (set every frame)")]
    public Vector2 moveInput;   // X = strafe, Y = forward
    public bool jumpPressed;
    public bool crouchHeld;

    /* =========================================================
     * MOVEMENT TUNING
     * ========================================================= */
    [Header("Movement Tuning")]
    [SerializeField] private float moveForce = 150f;

    [Header("Speed Caps")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float slideMaxSpeed = 16f;
    [SerializeField] private float airMaxSpeed = 22f;
    [Header("Soft Speed Cap (Momentum Friendly)")]
    [SerializeField] private bool useSoftSpeedCap = true;
    [SerializeField] private float softCapDrag = 18f;             // higher = stronger slowdown above cap
    [SerializeField] private float hardClampMultiplier = 1.35f;   // safety clamp at cap * this


    [SerializeField] private float jumpForce = 7.5f;
    [SerializeField] private float airControl = 0.4f;
    [SerializeField] private float airDeceleration = 0.003f;

    [SerializeField] private float extraGravity = 11f;

    [Header("Counter Movement Accuracy")]
    [SerializeField] private float idleBrakeStrength = 10f;
    [SerializeField] private float opposeBrakeStrength = 8f;
    [SerializeField] private float alignBrakeStrength = 2f;
    [SerializeField] private float bhopGraceTime = 0.08f;
    [SerializeField, Range(0f, 1f)] private float bhopBrakeScale = 0.25f;

    private float lastGroundedTime = -999f;
    private bool wasGrounded;

    [Header("Slope Classification")]
    [SerializeField] private float maxWalkSlopeAngle = 60f;
    [SerializeField] private float maxSlideSlopeAngle = 80f;

    [SerializeField] private float groundMoveMultiplier = 1f;
    [SerializeField] private float groundCounterMultiplier = 1f;
    [SerializeField] private float groundMaxSpeedMultiplier = 1f;

    [Header("Manual Slide")]
    [SerializeField] private float slideStartBoostAdd = 2.0f;     // flat m/s added on slide start
    [SerializeField] private float slideBoostCooldown = 1.0f;     // seconds
    [SerializeField] private float slideBoostMinSpeed = 0.1f;     // "not idle" threshold
    [SerializeField] private float slideStartDownhillPush = 6f;
    [SerializeField] private float slideSpeedMultiplier = 0.15f;
    [SerializeField] private float slideBrakeMultiplier = 0.15f;

    private float lastSlideBoostTime = -999f;

    [Header("Slide Steering")]
    [SerializeField] private float slideForce = 21f;
    [SerializeField] private float minDownhillAngle = 2f;

    [Header("Slide Steering Weights (Less slope control)")]
    [Tooltip("Downhill weight when slope is near maxWalkSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWDownFlat = 0.15f;
    [Tooltip("Downhill weight when slope is near maxSlideSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWDownSteep = 0.35f;

    [Tooltip("Velocity weight when slope is near maxWalkSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWVelFlat = 0.80f;
    [Tooltip("Velocity weight when slope is near maxSlideSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWVelSteep = 0.90f;

    [Tooltip("Input weight when slope is near maxWalkSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWInpFlat = 0.25f;
    [Tooltip("Input weight when slope is near maxSlideSlopeAngle.")]
    [SerializeField, Range(0f, 1f)] private float slideWInpSteep = 0.08f;

    [Header("Forced Slide (Steep Slopes)")]
    [SerializeField] private float slideGravity = 35f;
    [SerializeField] private float slideMinSpeed = 0.25f;

    [Header("Uphill Slowdown (Toggle)")]
    [Tooltip("If OFF: removes extra uphill slowdown (walking/sliding) besides normal friction/counter.\n" +
             "Forced sliding on steep slopes remains.")]
    [SerializeField] private bool enableUphillSlowdown = false;

    [Tooltip("Seconds after landing where slope forces won't steal momentum immediately.")]
    [SerializeField] private float landingSlopeGraceTime = 0.12f;

    [Header("Uphill Slowdown Controls (only if enableUphillSlowdown)")]
    [SerializeField, Range(0.1f, 1f)] private float uphillSlideGravityScale = 0.55f;
    [SerializeField, Range(0f, 1f)] private float uphillWalkDownSlopeGravityScale = 0.5f;
    [SerializeField, Range(0f, 1f)] private float uphillSlideDownhillForceScale = 0.5f;

    [Header("Prevent Backwards Slope Forces")]
    [Tooltip("If ON: slope forces (slide gravity/steer/downforce) will never accelerate you backwards relative to last input direction.")]
    [SerializeField] private bool preventBackwardsSlopeForces = true;

    [Header("Landing Momentum Preserve (Ramp Forgiveness)")]
    [SerializeField] private bool enableLandingMomentumPreserve = true;

    [Tooltip("Minimum fraction of your pre-landing flat speed to keep when hitting an uphill slope.")]
    [SerializeField, Range(0f, 1f)] private float landingPreservePercent = 0.75f;

    [Tooltip("Only apply preserve when slope angle is at least this (degrees).")]
    [SerializeField] private float landingPreserveMinSlopeAngle = 6f;

    private Vector3 lastAirVelocity;

    [Header("Uphill Downforce (Optional)")]
    [SerializeField] private bool enableUphillDownforce = false;
    [SerializeField] private float uphillDownforce = 12f;
    [SerializeField] private float uphillDownforceMinAngle = 2f;

    private bool manualSliding;
    private bool prevCrouchHeld;

    private FrictionModifier currentFriction;
    private float groundAngle;
    private bool onWalkableSlope;
    private bool onSlideableSlope;

    /* =========================================================
     * GROUND CHECK
     * ========================================================= */
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.3f;

    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;

    /* =========================================================
     * STATE
     * ========================================================= */
    [System.Flags]
    public enum MovementState
    {
        Idle = 1 << 0,
        Walking = 1 << 1,
        Crouching = 1 << 2,
        Sliding = 1 << 3,
        Airborne = 1 << 4,
        Wallrun = 1 << 5
    }

    [Header("Visual Alignment")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform yawSource;
    [SerializeField] private float groundAlignSpeed = 14f;
    [SerializeField] private float airUprightSpeed = 10f;
    [SerializeField] private float modelTurnInputDeadzone = 0.15f;


    [Header("Crouch (Visual + Collider)")]
    [SerializeField] private float crouchScaleY = 0.6f;
    [SerializeField] private float crouchLerpSpeed = 12f;

    private Vector3 modelStandScale;
    [SerializeField] private float crouchCapsuleHeightMultiplier = 0.6f;
    [SerializeField] private float crouchCapsuleLerpSpeed = 16f;

    private float standCapsuleHeight;
    private Vector3 standCapsuleCenter;

    [Header("Debug")]
    [SerializeField] private MovementState currentState;
    [SerializeField] private Vector3 velocityXZ;

    [Header("Debug (Read-only numbers)")]
    [SerializeField] private float debugGroundAngle;
    [SerializeField] private float debugSpeed;
    [SerializeField] private float debugFlatSpeed;

    [Header("Debug Forces (Read-only vectors)")]
    [SerializeField] private Vector3 debugMoveForce;
    [SerializeField] private Vector3 debugCounterForce;
    [SerializeField] private Vector3 debugSlideSteerForce;
    [SerializeField] private Vector3 debugSlideGravityForce;
    [SerializeField] private Vector3 debugGravityForce;

    [Header("Debug Slide")]
    [SerializeField] private bool forcedSliding;
    [SerializeField] private Vector3 downhillDir;

    /* =========================================================
     * UNITY LIFECYCLE
     * ========================================================= */
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;
        rb.useGravity = false;
        if (modelRoot != null)
            modelStandScale = modelRoot.localScale;
        standCapsuleHeight = capsule.height;
        standCapsuleCenter = capsule.center;


    }

    private void FixedUpdate()
    {
        debugMoveForce = Vector3.zero;
        debugCounterForce = Vector3.zero;
        debugSlideSteerForce = Vector3.zero;
        debugSlideGravityForce = Vector3.zero;
        debugGravityForce = Vector3.zero;

        UpdateGroundCheck();
        UpdateForcedSlideState();

        bool crouchPressed = crouchHeld && !prevCrouchHeld;
        prevCrouchHeld = crouchHeld;
        UpdateManualSlideState(crouchPressed);

        bool groundedNow = isGrounded;
        velocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Track last airborne velocity so we can "forgive" ramp landings
        if (!groundedNow)
        {
            lastAirVelocity = rb.linearVelocity;
        }

        if (groundedNow && !wasGrounded)
        {
            lastGroundedTime = Time.time;
            ApplyLandingMomentumPreserve();
            TryApplyLandingSlideBoost();

        }

        wasGrounded = groundedNow;

        debugGroundAngle = groundAngle;
        debugSpeed = rb.linearVelocity.magnitude;
        debugFlatSpeed = velocityXZ.magnitude;

        UpdateState();

        ApplyMovement();
        ApplySlideSteering();
        ApplySlideGravity();
        ApplyUphillDownforce();

        bool jumpedThisFrame = HandleJump();
        if (!jumpedThisFrame)
            ApplyCounterMovement();

        ApplyGravity();
        ClampHorizontalSpeed();
        UpdateCrouchVisual(Time.fixedDeltaTime);
        UpdateCrouchCollider(Time.fixedDeltaTime);

        jumpPressed = false;
    }

    private void LateUpdate()
    {
        if (modelRoot == null)
            return;

        // Face input direction (camera-relative input), smoothly.
        Vector3 fwd = new Vector3(moveInput.x, 0f, moveInput.y);

        // Deadzone so tiny stick noise doesn't jitter the model.
        if (fwd.sqrMagnitude < (modelTurnInputDeadzone * modelTurnInputDeadzone))
        {
            // No meaningful input: keep current facing (do not snap to camera or velocity).
            fwd = modelRoot.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = transform.forward;
        }

        if (fwd.sqrMagnitude > 0.0001f)
        {
            fwd.Normalize();

            // If grounded, align the facing direction to the ground plane
            if (isGrounded)
            {
                fwd = Vector3.ProjectOnPlane(fwd, groundNormal);
                if (fwd.sqrMagnitude > 0.0001f)
                    fwd.Normalize();
            }
        }


        Vector3 up = isGrounded ? groundNormal : Vector3.up;

        Quaternion target = Quaternion.LookRotation(fwd, up);
        float spd = isGrounded ? groundAlignSpeed : airUprightSpeed;
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, target, Time.deltaTime * spd);
    }

    /* =========================================================
     * HELPERS
     * ========================================================= */
    private bool InLandingSlopeGrace()
    {
        return isGrounded && (Time.time - lastGroundedTime) <= landingSlopeGraceTime;
    }
    private void TryApplyLandingSlideBoost()
    {
        // Only on landing frame (caller ensures that)
        if (!crouchHeld)
            return;

        // Never boost when forced sliding (your rule)
        if (forcedSliding)
            return;

        // Cooldown (your rule)
        if ((Time.time - lastSlideBoostTime) < slideBoostCooldown)
            return;

        Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = velXZ.magnitude;

        // No boost for idle -> slide (your rule)
        if (speed <= slideBoostMinSpeed)
            return;

        Vector3 dir = velXZ.normalized;

        float cap = slideMaxSpeed * groundMaxSpeedMultiplier; // your choice: clamp to slideMaxSpeed
        float newSpeed = Mathf.Min(speed + slideStartBoostAdd, cap);

        rb.linearVelocity = new Vector3(
            dir.x * newSpeed,
            rb.linearVelocity.y,
            dir.z * newSpeed
        );

        lastSlideBoostTime = Time.time;
    }

    private Vector3 GetLookDirOnPlane()
    {
        Vector3 fwd = yawSource != null ? yawSource.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = transform.forward;
        fwd.Normalize();

        if (isGrounded)
        {
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal);
            if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
        }

        return fwd;
    }

    private Vector3 GetInputDirOnPlane()
    {
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        if (isGrounded)
        {
            dir = Vector3.ProjectOnPlane(dir, groundNormal);
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            else dir = Vector3.zero;
        }

        return dir;
    }


    private void ClampForceNoBackwards(ref Vector3 force)
    {
        if (!preventBackwardsSlopeForces)
            return;

        // Define "backwards" by INPUT direction, not camera/look direction.
        Vector3 refDir = GetInputDirOnPlane();

        // If no input, fall back to current planar velocity direction (still not look-based).
        if (refDir.sqrMagnitude < 0.0001f)
        {
            Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (velXZ.sqrMagnitude > 0.0001f)
                refDir = velXZ.normalized;
        }

        if (refDir.sqrMagnitude < 0.0001f)
            return;

        float backComp = Vector3.Dot(force, -refDir);
        if (backComp > 0f)
        {
            force += refDir * backComp; // cancels the backwards component
        }
    }


    /* =========================================================
     * CORE STEPS
     * ========================================================= */
    private void UpdateGroundCheck()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        float radius = capsule.radius * 0.9f;

        Vector3 bottom = transform.position + capsule.center + Vector3.down * (capsule.height * 0.5f - capsule.radius);
        Vector3 origin = bottom + Vector3.up * 0.05f;

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;

            groundAngle = Vector3.Angle(groundNormal, Vector3.up);
            onWalkableSlope = groundAngle <= maxWalkSlopeAngle;
            onSlideableSlope = groundAngle <= maxSlideSlopeAngle;

            isGrounded = onSlideableSlope;

            currentFriction = hit.collider.GetComponent<FrictionModifier>();
            if (currentFriction != null)
            {
                groundMoveMultiplier = currentFriction.moveForceMultiplier;
                groundCounterMultiplier = currentFriction.counterMoveMultiplier;
                groundMaxSpeedMultiplier = currentFriction.maxSpeedMultiplier;
            }
            else
            {
                groundMoveMultiplier = 1f;
                groundCounterMultiplier = 1f;
                groundMaxSpeedMultiplier = 1f;
            }
        }
        else
        {
            groundAngle = 0f;
            onWalkableSlope = false;
            onSlideableSlope = false;

            currentFriction = null;
            groundMoveMultiplier = 1f;
            groundCounterMultiplier = 1f;
            groundMaxSpeedMultiplier = 1f;
        }
    }

    private void UpdateState()
    {
        currentState = 0;
        bool slidingNow = IsSlidingNow();

        if (!isGrounded)
            currentState |= MovementState.Airborne;

        if (isGrounded && !slidingNow && velocityXZ.magnitude > 0.1f)
            currentState |= MovementState.Walking;

        if (isGrounded && !slidingNow && velocityXZ.magnitude < 0.1f)
            currentState |= MovementState.Idle;

        if (slidingNow)
            currentState |= MovementState.Sliding;
    }

    private void ApplyMovement()
    {
        Vector3 wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (wishDir.sqrMagnitude > 1f)
            wishDir.Normalize();

        float mf = moveForce;
        if (isGrounded) mf *= groundMoveMultiplier;

        Vector3 forceDir = wishDir;

        if (isGrounded)
        {
            forceDir = Vector3.ProjectOnPlane(forceDir, groundNormal);
            if (forceDir.sqrMagnitude > 0.0001f) forceDir.Normalize();
        }

        Vector3 force = forceDir * mf;

        if (!isGrounded)
            force *= airControl;
        else if (IsSlidingNow())
            force *= slideSpeedMultiplier;

        debugMoveForce = force;
        rb.AddForce(force, ForceMode.Force);
    }

    private void ApplyCounterMovement()
    {
        float speed = velocityXZ.magnitude;

        if (!isGrounded)
        {
            Vector3 airDecel = velocityXZ * (airDeceleration * Time.fixedDeltaTime);
            rb.linearVelocity -= airDecel;
            debugCounterForce = Vector3.zero;
            return;
        }

        if (speed <= 0.0001f)
            return;

        float brakeScale = 1f;
        if (Time.time - lastGroundedTime <= bhopGraceTime)
            brakeScale = bhopBrakeScale;

        float cm = groundCounterMultiplier;
        if (IsSlidingNow())
            cm *= slideBrakeMultiplier;

        Vector3 wish = new Vector3(moveInput.x, 0f, moveInput.y);
        bool hasInput = wish.sqrMagnitude > 0.0001f;
        if (hasInput)
            wish.Normalize();

        Vector3 velDir = velocityXZ / speed;

        if (!hasInput)
        {
            Vector3 f = -velDir * (idleBrakeStrength * cm * speed * brakeScale);
            debugCounterForce = f;
            rb.AddForce(f, ForceMode.Force);
            return;
        }

        float dot = Vector3.Dot(velDir, wish);

        if (dot > 0.2f)
            return;

        if (dot < 0f)
        {
            Vector3 f = -velDir * (opposeBrakeStrength * cm * speed * brakeScale);
            debugCounterForce = f;
            rb.AddForce(f, ForceMode.Force);
        }
        else
        {
            Vector3 f = -velDir * (alignBrakeStrength * cm * speed * brakeScale);
            debugCounterForce = f;
            rb.AddForce(f, ForceMode.Force);
        }
    }

    private void ApplyGravity()
    {
        Vector3 g = Physics.gravity + Vector3.down * extraGravity;

        // only apply these uphill slowdowns if toggled on AND not in landing grace
        if (enableUphillSlowdown && !InLandingSlopeGrace())
        {
            // Sliding uphill: lighten gravity a bit
            if (isGrounded && IsSlidingNow() && TryGetDownhill(out Vector3 downhillSlide))
            {
                Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                bool movingUphill =
                    velXZ.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(velXZ, downhillSlide) < 0f;

                if (movingUphill)
                    g *= uphillSlideGravityScale;
            }

            // Walking uphill: reduce only slope-parallel gravity component
            if (isGrounded && !IsSlidingNow() && onWalkableSlope)
            {
                Vector3 wish = new Vector3(moveInput.x, 0f, moveInput.y);
                if (wish.sqrMagnitude > 0.0001f)
                {
                    wish.Normalize();
                    wish = Vector3.ProjectOnPlane(wish, groundNormal);
                    if (wish.sqrMagnitude > 0.0001f) wish.Normalize();

                    if (TryGetDownhill(out Vector3 downhillWalk))
                    {
                        bool pushingUphill = Vector3.Dot(wish, downhillWalk) < 0f;
                        if (pushingUphill)
                        {
                            Vector3 alongNormal = Vector3.Project(g, groundNormal);
                            Vector3 alongPlane = g - alongNormal;
                            alongPlane *= uphillWalkDownSlopeGravityScale;
                            g = alongNormal + alongPlane;
                        }
                    }
                }
            }
        }

        debugGravityForce = g;
        rb.AddForce(g, ForceMode.Acceleration);
    }

    private bool HandleJump()
    {
        if (!jumpPressed || !isGrounded)
            return false;

        Vector3 vel = rb.linearVelocity;
        if (vel.y < 0f)
            vel.y = 0f;

        rb.linearVelocity = vel;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        return true;
    }
    private void ClampHorizontalSpeed()
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);

        float cap;
        if (!isGrounded) cap = airMaxSpeed;
        else if (IsSlidingNow()) cap = slideMaxSpeed * groundMaxSpeedMultiplier;
        else cap = maxSpeed * groundMaxSpeedMultiplier;

        float speed = flat.magnitude;
        if (speed <= cap)
            return;

        if (useSoftSpeedCap)
        {
            // Soft cap: apply extra drag only for the amount above cap (keeps momentum feel)
            float excess = speed - cap;
            Vector3 dir = flat / speed;

            // Acceleration-based drag so it feels consistent across masses
            Vector3 dragAccel = -dir * (excess * softCapDrag);
            rb.AddForce(dragAccel, ForceMode.Acceleration);

            // Safety hard clamp if we get way above cap (prevents runaway edge cases)
            float hardCap = cap * hardClampMultiplier;
            if (speed > hardCap)
            {
                Vector3 limited = dir * hardCap;
                rb.linearVelocity = new Vector3(limited.x, vel.y, limited.z);
            }
        }
        else
        {
            // Old hard clamp behavior
            Vector3 limited = (flat / speed) * cap;
            rb.linearVelocity = new Vector3(limited.x, vel.y, limited.z);
        }
    }


    private void UpdateForcedSlideState()
    {
        forcedSliding = isGrounded && !onWalkableSlope && onSlideableSlope;

        if (!forcedSliding)
        {
            downhillDir = Vector3.zero;
            return;
        }

        downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhillDir.sqrMagnitude > 0.0001f)
            downhillDir.Normalize();
        else
            downhillDir = Vector3.zero;
    }

    private void ApplySlideGravity()
    {
        if (!IsSlidingNow())
            return;

        float angle = Vector3.Angle(groundNormal, Vector3.up);
        if (angle < minDownhillAngle)
            return;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude < 0.0001f)
            return;
        downhill.Normalize();

        float appliedSlideGravity = slideGravity;

        // if uphill slowdown disabled, do NOT reduce/modify slide gravity based on uphill movement
        // (still allow steep-slope forced sliding to work normally)
        if (enableUphillSlowdown && !InLandingSlopeGrace())
        {
            Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (velXZ.sqrMagnitude > 0.0001f)
            {
                bool movingUphill = Vector3.Dot(velXZ, downhill) < 0f;
                if (movingUphill)
                    appliedSlideGravity *= uphillSlideDownhillForceScale;
            }
        }

        Vector3 f = downhill * appliedSlideGravity;

        // IMPORTANT: never allow slope gravity to push player backwards relative to look direction
        ClampForceNoBackwards(ref f);

        debugSlideGravityForce = f;
        rb.AddForce(f, ForceMode.Acceleration);

        Vector3 velXZ2 = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (velXZ2.magnitude < slideMinSpeed)
        {
            Vector3 f2 = downhill * (appliedSlideGravity * 0.25f);
            ClampForceNoBackwards(ref f2);
            debugSlideGravityForce += f2;
            rb.AddForce(f2, ForceMode.Acceleration);
        }
    }

    private void UpdateManualSlideState(bool crouchPressed)
    {
        if (!isGrounded || forcedSliding)
        {
            manualSliding = false;
            return;
        }

        if (!manualSliding && crouchPressed)
        {
            manualSliding = true;

            Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float speed = velXZ.magnitude;

            // Manual slide boost rules:
            // - only if grounded (already guaranteed here)
            // - NOT if forcedSliding (already returned above)
            // - NOT if idle -> sliding
            // - NOT if cooldown not ready
            if (speed > slideBoostMinSpeed && (Time.time - lastSlideBoostTime) >= slideBoostCooldown)
            {
                Vector3 dir = velXZ.normalized;

                float cap = slideMaxSpeed * groundMaxSpeedMultiplier;     // your choice: clamp to slideMaxSpeed
                float newSpeed = Mathf.Min(speed + slideStartBoostAdd, cap);

                rb.linearVelocity = new Vector3(
                    dir.x * newSpeed,
                    rb.linearVelocity.y,
                    dir.z * newSpeed
                );

                lastSlideBoostTime = Time.time;
            }


            if (speed <= slideBoostMinSpeed)
            {
                Vector3 starterDir = Vector3.zero;

                float angle = Vector3.Angle(groundNormal, Vector3.up);
                if (angle >= minDownhillAngle)
                {
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                    if (downhill.sqrMagnitude > 0.0001f)
                    {
                        downhill.Normalize();
                        starterDir = downhill;
                    }
                }

                if (starterDir == Vector3.zero)
                {
                    Vector3 fwd = (yawSource != null) ? yawSource.forward : transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.0001f)
                    {
                        fwd.Normalize();
                        starterDir = fwd;
                    }
                }

                if (starterDir != Vector3.zero)
                    rb.AddForce(starterDir * slideStartDownhillPush, ForceMode.Impulse);
            }
        }

        if (manualSliding && !crouchHeld)
            manualSliding = false;
    }

    private bool IsSlidingNow()
    {
        return forcedSliding || crouchHeld;
    }

    private void ApplySlideSteering()
    {
        if (!isGrounded)
            return;
        if (!IsSlidingNow())
            return;

        // reduce "ground trajectory dictates everything" by heavily biasing velocity,
        // with small input, and small downhill.
        Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velDir = velXZ.sqrMagnitude > 0.0001f ? velXZ.normalized : Vector3.zero;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        float angle = Vector3.Angle(groundNormal, Vector3.up);

        Vector3 downhill = Vector3.zero;
        if (angle >= minDownhillAngle)
        {
            downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            if (downhill.sqrMagnitude > 0.0001f) downhill.Normalize();
            else downhill = Vector3.zero;
        }

        float slopeT = Mathf.InverseLerp(maxWalkSlopeAngle, maxSlideSlopeAngle, angle);
        slopeT = Mathf.Clamp01(slopeT);

        float wDown = Mathf.Lerp(slideWDownFlat, slideWDownSteep, slopeT);
        float wVel = Mathf.Lerp(slideWVelFlat, slideWVelSteep, slopeT);
        float wInp = Mathf.Lerp(slideWInpFlat, slideWInpSteep, slopeT);

        if (inputDir.sqrMagnitude < 0.0001f) wInp = 0f;
        if (downhill == Vector3.zero) wDown = 0f;

        float sum = wDown + wVel + wInp;
        if (sum <= 0.0001f) return;

        wDown /= sum; wVel /= sum; wInp /= sum;

        Vector3 blended =
            downhill * wDown +
            velDir * wVel +
            inputDir * wInp;

        if (blended.sqrMagnitude < 0.0001f)
            return;

        blended.Normalize();

        Vector3 f = blended * slideForce;

        // Don't let slide steering ever push you backwards relative to look
        ClampForceNoBackwards(ref f);

        debugSlideSteerForce = f;
        rb.AddForce(f, ForceMode.Force);
    }

    private bool TryGetDownhill(out Vector3 downhill)
    {
        downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude < 0.0001f) return false;
        downhill.Normalize();
        return true;
    }

    private void ApplyUphillDownforce()
    {
        if (!enableUphillDownforce)
            return;

        if (!isGrounded)
            return;

        // Don't interfere with forced sliding on too-steep-to-walk slopes
        if (forcedSliding)
            return;

        // If you want to test "no extra uphill slowdown", this should effectively be off
        // (so keep it separate from enableUphillSlowdown).
        float angle = Vector3.Angle(groundNormal, Vector3.up);
        if (angle < uphillDownforceMinAngle)
            return;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude < 0.0001f)
            return;
        downhill.Normalize();

        Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (velXZ.sqrMagnitude < 0.0001f)
            return;

        bool movingUphill = Vector3.Dot(velXZ, downhill) < 0f;
        if (!movingUphill)
            return;

        Vector3 f = downhill * uphillDownforce;
        ClampForceNoBackwards(ref f);
        rb.AddForce(f, ForceMode.Acceleration);
    }

    private void ApplyLandingMomentumPreserve()
    {
        if (!enableLandingMomentumPreserve)
            return;

        //
        //float angle = Vector3.Angle(groundNormal, Vector3.up);
        //if (angle < landingPreserveMinSlopeAngle)
        //    return;

        Vector3 airVel = lastAirVelocity;

        Vector3 airFlat = new Vector3(airVel.x, 0f, airVel.z);
        float airSpeed = airFlat.magnitude;
        if (airSpeed < 0.1f)
            return;

        Vector3 curVel = rb.linearVelocity;
        Vector3 curFlat = new Vector3(curVel.x, 0f, curVel.z);
        float curSpeed = curFlat.magnitude;

        float minWanted = airSpeed * landingPreservePercent;

        //// Only “give back” speed if the landing collision stole too much.
        //if (curSpeed >= minWanted)
        //    return;

        // Keep current direction if we still have one; otherwise use projected air direction along the slope.
        Vector3 dir = curFlat;
        if (dir.sqrMagnitude < 0.0001f)
        {
            Vector3 projected = Vector3.ProjectOnPlane(airVel, groundNormal);
            dir = new Vector3(projected.x, 0f, projected.z);
        }

        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        rb.linearVelocity = new Vector3(
            dir.x * minWanted,
            curVel.y,
            dir.z * minWanted
        );
    }
    private void UpdateCrouchVisual(float dt)
    {
        if (modelRoot == null)
            return;

        float targetY = IsSlidingNow() ? crouchScaleY : 1f;


        Vector3 target = new Vector3(
            modelStandScale.x,
            modelStandScale.y * targetY,
            modelStandScale.z
        );

        modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, target, dt * crouchLerpSpeed);
    }

    private void UpdateCrouchCollider(float dt)
    {
        float targetHeight = IsSlidingNow() ? standCapsuleHeight * crouchCapsuleHeightMultiplier : standCapsuleHeight;

        // Keep bottom roughly in place by shifting center down/up with height
        float heightDelta = targetHeight - capsule.height;
        Vector3 targetCenter = capsule.center;
        targetCenter.y += heightDelta * 0.5f;

        capsule.height = Mathf.Lerp(capsule.height, targetHeight, dt * crouchCapsuleLerpSpeed);
        capsule.center = Vector3.Lerp(capsule.center, targetCenter, dt * crouchCapsuleLerpSpeed);
    }

}
