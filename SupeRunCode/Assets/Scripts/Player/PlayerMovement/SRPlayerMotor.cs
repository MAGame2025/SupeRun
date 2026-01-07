using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SRPlayerMotor : MonoBehaviour
{
    [Header("Debug (read-only)")]
    [SerializeField] private bool ccGrounded;
    [SerializeField] private bool stableGroundedDebug;
    [SerializeField] private bool groundContact;
    [SerializeField] private float groundHitDistance;
    [SerializeField] private float groundAngle;
    [SerializeField] private bool stickActive;

    [SerializeField] private Vector3 groundNormal = Vector3.up;
    [SerializeField] private Vector3 planarVelocity;
    [SerializeField] private float verticalVelocity;
    [SerializeField] private float planarSpeed;
    public float MaxWalkSlopeAngle => maxWalkSlopeAngle;

    public bool CCGrounded => ccGrounded;
    public bool GroundContact => groundContact;
    public float GroundHitDistance => groundHitDistance;
    public float GroundAngle => groundAngle;
    public bool StickActive => stickActive;

    public Vector3 GroundNormal => groundNormal;
    public Vector3 PlanarVelocity => planarVelocity;
    public float VerticalVelocity => verticalVelocity;
    public float PlanarSpeed => planarSpeed;
    [Header("Acceleration")]
    [SerializeField] private float walkAccel = 55f;
    [SerializeField] private float runAccelMultiplier = 1.4f;
    [SerializeField] private float slideAccelMultiplier = 0.15f;    // locked by you
    [SerializeField] private float airAccel = 28f;

    [Header("Soft Speed Caps")]
    [SerializeField] private float walkSpeedCap = 8.5f;
    [SerializeField] private float runSpeedCap = 13.5f;
    [SerializeField] private float airSpeedCap = 14.0f;
    [SerializeField] private float capDamping = 10f;

    [Header("Friction / Drag")]
    [SerializeField] private float groundFriction = 10f; // always applied on ground
    [SerializeField] private float airDrag = 0.6f;       // low

    [Header("Walk Slope Effects (NOT sliding)")]
    [SerializeField] private float uphillSlowStrength = 6f;
    [SerializeField] private float downhillAssistStrength = 2f;

    [Header("Slide / Crouch")]
    [SerializeField] private float crouchHeightMultiplier = 0.5f;   // locked by you
    [SerializeField] private float slideFriction = 3.5f;
    [SerializeField] private float slideSpeedCap = 22f;             // high cap; tune
    [SerializeField] private float slideCapDamping = 12f;           // soft cap strength

    [Header("Slide Slope Gravity Split")]
    [SerializeField] private float slideSlopeGravityDownhill = 18f; // keep your current feel
    [SerializeField] private float slideSlopeGravityUphill = 10f;   // weaker (tune)


    [SerializeField] private float headroomCheckExtra = 0.05f;      // small padding

    [Header("Slide Entry Seed")]
    [SerializeField] private float slideEntrySeedSpeed = 0.5f;     // your choice
    [SerializeField] private float slideEntrySpeedThreshold = 0.25f; // "standing still" threshold
    [SerializeField] private float slideEntryMinSlopeAngle = 6f;     // must be at least this steep to auto-slide

    [Header("Wall Scrape (Sliding)")]
    [SerializeField, Range(0.5f, 1f)] private float wallScrapeDamping = 0.88f; // lose a little speed on wall scrape


    [Header("Slide Direction Weights (must sum to 1)")]
    [SerializeField, Range(0f, 1f)] private float slideWeightVelocity = 0.75f;
    [SerializeField, Range(0f, 1f)] private float slideWeightDownhill = 0.15f;
    [SerializeField, Range(0f, 1f)] private float slideWeightInput = 0.10f;

    [Header("Slide Stability")]
    [SerializeField] private float minDownhillAngle = 6f; // below this, ignore downhill to prevent jitter

    [Header("Slide Ground Influence Toggle")]
    [SerializeField] private bool useGroundDownhillInSlide = true;

    [Header("Slide Pit Stability")]
    [SerializeField] private float slideNormalLerpSpeed = 20f;      // smooth ground normal just for slide
    [SerializeField] private float slideDirLerpSpeed = 14f;         // smooth final slide direction
    [SerializeField] private float slideMinStableSpeed = 1.25f;     // below this, prevent flip jitter
    [SerializeField, Range(0f, 1f)] private float slideFlipResistance = 0.85f; // 0=no lock, 1=strong lock

    private bool isCrouched;     // “capsule is short” (what we actually do)
    private float standHeight;
    private Vector3 standCenter;

    private float crouchHeight;
    private Vector3 crouchCenter;
    private Vector3 slideGroundNormalSmoothed = Vector3.up;
    private Vector3 lastSlideDir = Vector3.forward;


    [Header("Ground Probe")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeDistance = 0.35f;
    [SerializeField] private float probeRadius = 0.22f;
    [SerializeField] private float maxWalkSlopeAngle = 60f;
    [SerializeField] private float maxSlideSlopeAngle = 80f; // NEW: sliding can treat steeper slopes as "ground"


    [Header("Gravity / Grounding")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float terminalVelocity = -60f;

    [Tooltip("Toggleable stick-to-ground bias (for testing). This is NOT the ground check.")]
    [SerializeField] private bool stickToGroundEnabled = true;
    [SerializeField] private float stickToGroundVelocity = -2.0f;
    [SerializeField] private float groundedSnapExtra = 0.05f;


    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 10f;

    [Tooltip("How many EXTRA jumps while airborne (0=single jump, 1=double jump, etc.)")]
    [SerializeField] private int extraJumps = 1;

    [Tooltip("If falling faster than this, clamp before jump so jump isn't eaten.")]
    [SerializeField] private float fastFallThreshold = -12f;

    [Tooltip("When fast-fall triggers, set vertical velocity up to at least this before jump.")]
    [SerializeField] private float fastFallClamp = -2f;

    [Header("Visual Proximity Probe (for model tilt only)")]
    [SerializeField] private float visualProbeDistance = 2.5f;
    [SerializeField] private float visualProbeRadius = 0.35f;

    [SerializeField] private bool visualGroundContact;
    [SerializeField] private float visualGroundDistance = float.PositiveInfinity;
    [SerializeField] private Vector3 visualGroundNormal = Vector3.up;

    [Header("Slide / Crouch Visuals")]
    [SerializeField] private Transform visualModelRoot;   // drag Player/Model here
    [SerializeField] private float crouchVisualScaleY = 0.7f;
    [SerializeField] private float crouchVisualYOffset = -0.35f;
    [SerializeField] private float visualCrouchLerpSpeed = 18f;

    [Header("Grounded Stabilization")]
    [SerializeField] private float groundedSnapDistance = 0.08f; // how close probe must be
    [SerializeField] private float ungroundDelay = 0.08f;        // seconds of grace before ungrounding

    [Header("Slide Landing Boost")]
    [SerializeField] private bool enableSlideLandingBoost = true;
    [SerializeField] private float slideLandingMinSlopeAngle = 10f;
    [SerializeField] private float slideLandingMinPlanarSpeed = 6f;
    [SerializeField] private float slideLandingMinFallSpeed = 1.5f; // needs to be falling at least this fast (abs)
    [SerializeField] private float slideLandingBoostCooldown = 0.15f;

    [SerializeField, Range(0f, 1f)] private float slideLandingAlignMin = 0.55f; // minimum dot to get ANY reward
    [SerializeField, Range(0f, 1f)] private float slideLandingAlignPerfect = 0.95f; // dot considered "perfect"

    [SerializeField] private float slideLandingBoostMin = 0.5f;  // reward at min alignment
    [SerializeField] private float slideLandingBoostMax = 2.5f;  // reward at perfect alignment
    [SerializeField]
    private AnimationCurve slideLandingRewardCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // shape the reward (optional but nice)

    private float slideLandingBoostCooldownTimer;

    private float ungroundTimer;
    private bool stableGrounded;
    public bool StableGrounded => stableGrounded;

    private Vector3 visualDefaultLocalPos;
    private Vector3 visualDefaultLocalScale;

    private bool slideHeldThisFrame;


    public bool VisualGroundContact => visualGroundContact;
    public float VisualGroundDistance => visualGroundDistance;
    public Vector3 VisualGroundNormal => visualGroundNormal;


    private int jumpsRemaining;
    private bool wasGrounded;

    private bool prevSlideHeld;

    private bool wallHitThisFrame;
    private Vector3 wallNormalThisFrame = Vector3.up;
    private float wallAngleThisFrame;

    bool followPlane;

    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        jumpsRemaining = Mathf.Max(0, extraJumps);
        wasGrounded = cc.isGrounded;
        standHeight = cc.height;
        standCenter = cc.center;

        crouchHeight = standHeight * crouchHeightMultiplier;

        // keep feet planted: move center down by half the height difference
        float heightDelta = standHeight - crouchHeight;
        crouchCenter = standCenter;
        crouchCenter.y -= heightDelta * 0.5f;

        if (visualModelRoot != null)
        {
            visualDefaultLocalPos = visualModelRoot.localPosition;
            visualDefaultLocalScale = visualModelRoot.localScale;
        }

    }

    public void Tick(Vector3 desiredDir, bool runHeld, bool slideHeld, float dt)
    {
        if (dt <= 0f)
            return;
        slideHeldThisFrame = slideHeld;

        if (slideLandingBoostCooldownTimer > 0f)
            slideLandingBoostCooldownTimer -= dt;


        wallHitThisFrame = false;
        wallNormalThisFrame = Vector3.up;
        wallAngleThisFrame = 0f;

        // 1) Probe ground normal
        ProbeGround();

        bool ccG = cc.isGrounded;

        // Probe says "we're basically on ground" if hit is close enough
        bool probeG = groundContact && groundHitDistance <= (groundedSnapDistance + groundedSnapExtra);

        // Only allow probe to “fake” grounded if we’re not moving strongly upward
        bool canSnap = verticalVelocity <= 0.5f;

        // Raw candidate grounded
        bool candidate = ccG || (probeG && canSnap);

        // Hysteresis / grace time: don’t drop grounded instantly
        if (candidate)
        {
            stableGrounded = true;
            ungroundTimer = ungroundDelay;
        }
        else
        {
            ungroundTimer -= dt;
            if (ungroundTimer <= 0f)
                stableGrounded = false;
        }


        bool groundedNow = stableGrounded;


        // --- Crouch/Slide state rules ---
        // If slide is held: crouch always (ground or air)
        if (slideHeld)
        {
            SetCrouch(true);
        }
        else
        {
            // If not held:
            // - in air: stand immediately (your choice A)
            // - on ground: attempt stand, but if blocked by ceiling, remain crouched
                SetCrouch(false);

        }

        // --- Slide entry seed: standing on slope + press slide + no input -> start moving downhill ---
        bool enteringSlide = (!prevSlideHeld && slideHeld);
        float slopeAngleNow = Vector3.Angle(groundNormal, Vector3.up);

        ApplySlideLandingBoostIfQualified(groundedNow, slopeAngleNow, dt);

        if (enteringSlide)
        {
            if (planarVelocity.sqrMagnitude > 0.0001f)
                lastSlideDir = planarVelocity.normalized;
        }

        if (enteringSlide && groundedNow)
        {
            bool noInput = desiredDir.sqrMagnitude <= 0.0001f;
            bool nearlyStopped = planarVelocity.magnitude <= slideEntrySpeedThreshold;


            if (noInput && nearlyStopped && slopeAngleNow >= slideEntryMinSlopeAngle && slopeAngleNow <= maxSlideSlopeAngle)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                if (downhill.sqrMagnitude > 0.0001f)
                {
                    downhill.Normalize();
                    planarVelocity += downhill * slideEntrySeedSpeed; // one-time seed
                }
            }
        }


        bool onWalkableSlope = groundedNow && slopeAngleNow <= maxWalkSlopeAngle;

        // Sliding can still treat steeper slopes as slideable ground
        bool onSlideableSlope = groundedNow && slopeAngleNow <= maxSlideSlopeAngle;


        // Reset jump stock when we land
        if (groundedNow && !wasGrounded)
            jumpsRemaining = Mathf.Max(0, extraJumps);

        wasGrounded = groundedNow;

        // 2) Acceleration (velocity-friendly / drifty)
        ApplyAcceleration(desiredDir, runHeld, isCrouched ? onSlideableSlope : onWalkableSlope, dt);


        // 3) Friction / drag
        ApplyFriction(isCrouched ? onSlideableSlope : onWalkableSlope, dt);


        // 4) Walk slope forces (uphill slow + tiny downhill assist)
        ApplySlopeForces(desiredDir, isCrouched ? onSlideableSlope : onWalkableSlope, dt);

        if (isCrouched && onSlideableSlope)
        {
            float tN = 1f - Mathf.Exp(-slideNormalLerpSpeed * dt);
            slideGroundNormalSmoothed = Vector3.Slerp(slideGroundNormalSmoothed, groundNormal, tN);
        }
        else
        {
            slideGroundNormalSmoothed = groundNormal;
        }


        //11)when sliding, follow ground path
        ApplySlideSteering(desiredDir, onSlideableSlope, dt);


        // 5) Gravity
        ApplyGravity(isCrouched ? onSlideableSlope : onWalkableSlope, dt);


        // 6) Soft caps
        ApplySpeedCaps(runHeld, isCrouched ? onSlideableSlope : onWalkableSlope, dt);


        UpdateCrouchVisual(dt);

        // 7) Move
        followPlane = isCrouched ? onSlideableSlope : onWalkableSlope;
        MoveCharacter(dt, followPlane);



        // Debug refresh
        ccGrounded = cc.isGrounded;
        planarSpeed = planarVelocity.magnitude;
        groundAngle = Vector3.Angle(groundNormal, Vector3.up);

        stickActive =
            stickToGroundEnabled &&
            cc.isGrounded &&
            verticalVelocity <= stickToGroundVelocity;

        prevSlideHeld = slideHeld;
        stableGroundedDebug = stableGrounded;

    }
    private void ProbeGround()
    {
        groundNormal = Vector3.up;
        groundContact = false;
        groundHitDistance = float.PositiveInfinity;

        // Use TransformPoint (cc.center is local space)
        Vector3 centerWS = transform.TransformPoint(cc.center);

        // Capsule geometry
        float radius = cc.radius;
        float halfHeight = Mathf.Max(cc.height * 0.5f, radius + 0.001f);
        float cylinder = halfHeight - radius;

        // Bottom sphere center in world space
        Vector3 bottomWS = centerWS - Vector3.up * cylinder;

        // Start slightly above bottom so we don't start inside ground
        Vector3 castOrigin = bottomWS + Vector3.up * 0.05f;

        // Cast down far enough to reach below the feet + probeDistance
        float castDist = probeDistance + 0.25f;


        if (Physics.SphereCast(castOrigin, probeRadius, Vector3.down, out RaycastHit hit, castDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundContact = true;
            groundHitDistance = hit.distance;
            groundNormal = hit.normal;
        }

        Debug.DrawRay(castOrigin, Vector3.down * castDist, groundContact ? Color.green : Color.red, 0f);
        if (groundContact)
            Debug.DrawRay(hit.point, hit.normal, Color.cyan, 0f);


        // ---------------- Visual probe (tilt only) ----------------
        visualGroundContact = false;
        visualGroundDistance = float.PositiveInfinity;
        visualGroundNormal = Vector3.up;

        float vDist = cylinder + visualProbeDistance + 0.05f;

        if (Physics.SphereCast(castOrigin, visualProbeRadius, Vector3.down, out RaycastHit vHit, vDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            visualGroundContact = true;
            visualGroundDistance = vHit.distance;
            visualGroundNormal = vHit.normal;
        }
    }

    public void ControllerPreProbe()
    {
        // Update probe-based fields BEFORE SRPlayerController reads them.
        ProbeGround();

        // Keep debug angle in sync for the controller + inspector
        groundAngle = Vector3.Angle(groundNormal, Vector3.up);

        // Keep debug grounded in sync too (optional but nice)
        ccGrounded = cc.isGrounded;
    }

    private void ApplyAcceleration(Vector3 desiredDir, bool runHeld, bool grounded, float dt)
    {
        if (desiredDir.sqrMagnitude <= 0.0001f)
            return;

        float accel;

        if (grounded)
        {
            if (isCrouched)
            {
                // Sliding input force is ALWAYS based on walk accel
                accel = walkAccel * slideAccelMultiplier;
            }
            else
            {
                // Run = scaled walk
                accel = walkAccel * (runHeld ? runAccelMultiplier : 1f);
            }
        }
        else
        {
            accel = airAccel;
        }

        planarVelocity += desiredDir * (accel * dt);
        Debug.DrawRay(
            transform.position + Vector3.up,
            desiredDir * accel * 0.02f,
            isCrouched ? Color.cyan : Color.white,
            0f
        );

    }




    private void ApplyFriction(bool grounded, float dt)
    {
        if (grounded)
        {
            float fric = isCrouched ? slideFriction : groundFriction;
            float decay = Mathf.Exp(-fric * dt);
            planarVelocity *= decay;
        }
        else
        {
            float decay = Mathf.Exp(-airDrag * dt);
            planarVelocity *= decay;
        }
    }


    private void ApplySlopeForces(Vector3 desiredDir, bool grounded, float dt)
    {
        if (!grounded)
            return;

        // ---- Walking slope feel (uses current groundNormal) ----
        Vector3 walkDownhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (walkDownhill.sqrMagnitude <= 0.0001f)
            return;

        walkDownhill.Normalize();

        Vector3 moveDir = desiredDir.sqrMagnitude > 0.0001f
            ? desiredDir
            : (planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity.normalized : Vector3.zero);

        if (moveDir == Vector3.zero)
            return;

        float walkAlongDownhill = Vector3.Dot(moveDir, walkDownhill);

        if (!isCrouched)
        {
            if (walkAlongDownhill < 0f)
            {
                float strength = (-walkAlongDownhill) * uphillSlowStrength;
                planarVelocity += walkDownhill * (strength * dt);
            }
            else
            {
                float strength = walkAlongDownhill * downhillAssistStrength;
                planarVelocity += walkDownhill * (strength * dt);
            }
            return;
        }

        // ---- Sliding slope gravity (uses smoothed slide normal) ----
        Vector3 n = slideGroundNormalSmoothed;
        float angle = Vector3.Angle(n, Vector3.up);

        if (angle < minDownhillAngle)
            return;

        Vector3 slideDownhill = Vector3.ProjectOnPlane(Vector3.down, n);
        if (slideDownhill.sqrMagnitude <= 0.0001f)
            return;

        slideDownhill.Normalize();

        float steepness01 = Mathf.Clamp01(angle / maxSlideSlopeAngle);

        float minPull = 0.15f;

        Vector3 driveDir =
            planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity.normalized :
            (desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : Vector3.zero);

        float slideAlongDownhill = (driveDir == Vector3.zero) ? 0f : Vector3.Dot(driveDir, slideDownhill);

        float slopeStrength = (slideAlongDownhill < 0f) ? slideSlopeGravityUphill : slideSlopeGravityDownhill;

        if (slideAlongDownhill < 0f)
        {
            float uphill01 = Mathf.Clamp01(-slideAlongDownhill);
            float uphillScale = Mathf.Lerp(1f, 0.5f, uphill01);
            slopeStrength *= uphillScale;

            minPull = 0.05f;
        }

        steepness01 = Mathf.Max(steepness01, minPull);

        planarVelocity += slideDownhill * (slopeStrength * steepness01 * dt);
    }



    private void ApplyGravity(bool grounded, float dt)
    {

        // --- STEP 1: Apply gravity acceleration ---
        // Gravity continuously accelerates the player downward over time.
        // This affects both falling in air and slight downward pressure while grounded.
        verticalVelocity += gravity * dt;


        // --- STEP 2: Clamp to terminal velocity ---
        // Prevents vertical speed from growing infinitely while falling.
        // This keeps physics stable and avoids extreme values.
        if (verticalVelocity < terminalVelocity)
            verticalVelocity = terminalVelocity;

        // --- STEP 3: Stick-to-ground logic (optional) ---
        // When grounded, we may want to gently bias vertical velocity downward
        // to maintain contact with the ground over small bumps and downhill slopes.
        if (grounded && stickToGroundEnabled)
        {
            // Only apply stick-to-ground if gravity has not already pushed us down enough.
            // This avoids overpowering natural downward motion.
            if (verticalVelocity < stickToGroundVelocity)
                verticalVelocity = stickToGroundVelocity;
        }
    }
    private void ApplySpeedCaps(bool runHeld, bool grounded, float dt)
    {
        float cap;
        float damp;

        if (grounded && isCrouched)
        {
            cap = slideSpeedCap;
            damp = slideCapDamping;
        }
        else
        {
            cap = grounded
                ? (runHeld ? runSpeedCap : walkSpeedCap)
                : airSpeedCap;

            damp = capDamping;
        }

        float speed = planarVelocity.magnitude;
        if (speed <= cap)
            return;

        Vector3 capped = planarVelocity.normalized * cap;
        planarVelocity = Vector3.Lerp(planarVelocity, capped, 1f - Mathf.Exp(-damp * dt));
    }


    private void MoveCharacter(float dt, bool followGroundPlane)
    {

        Vector3 planar = planarVelocity;

        if (followGroundPlane)
        {
            planar = Vector3.ProjectOnPlane(planar, groundNormal);
        }


        // --- STEP 1: Combine horizontal and vertical motion ---
        // Planar velocity controls movement along the ground.
        // Vertical velocity controls gravity, jumping, and falling.
        Vector3 motion = (planar + Vector3.up * verticalVelocity) * dt;

        // --- STEP 2: Move the CharacterController ---
        // CharacterController.Move performs collision resolution internally.
        // It updates cc.isGrounded based on the resulting movement.
        cc.Move(motion);

        // --- Wall scrape: keep tangent momentum along steep walls while sliding ---
        if (isCrouched && wallHitThisFrame && wallAngleThisFrame > maxSlideSlopeAngle)
        {
            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, wallNormalThisFrame) * wallScrapeDamping;
        }

        // --- STEP 3: Post-move grounding correction ---
        // After movement, if the controller reports grounded and we're moving downward,
        // we optionally apply a small downward bias to maintain ground contact.
        // This prevents tiny hops on slopes and uneven surfaces.
        if (stableGrounded && verticalVelocity < 0f && stickToGroundEnabled)
            verticalVelocity = stickToGroundVelocity;

    }

    public bool TryJump(bool slideHeld)
    {
        // if stableGrounded hasn't been updated yet this frame, fall back
        bool groundedNow = stableGrounded || cc.isGrounded;



        // Allowed if grounded OR we have air jumps left
        if (!groundedNow && jumpsRemaining <= 0)
            return false;

        // Consume an air jump only if we're not grounded
        if (!groundedNow)
            jumpsRemaining--;

        // Fast-fall cancel (only when falling fast)
        if (verticalVelocity < fastFallThreshold)
            verticalVelocity = fastFallClamp;

        // Prevent stick-to-ground from eating the jump
        if (groundedNow && verticalVelocity < 0f)
            verticalVelocity = 0f;

        // ADD impulse (your choice)
        verticalVelocity += jumpImpulse;

        // Consider ourselves airborne right after jumping
        wasGrounded = false;

        // Slide/jump interaction:
        // - If slide is held, remain crouched in air (animation-friendly)
        // - If slide is not held, stand immediately in air (your choice A)
        if (slideHeld)
        {
            SetCrouch(true);
        }
        else
        {
            SetCrouch(false);
        }

        return true;
    }
    private bool CanStandUp()
    {
        // If we're already at (or above) standing height, we can stand.
        float heightDelta = standHeight - cc.height;
        if (heightDelta <= 0.001f)
            return true;

        // Build the CURRENT capsule (crouched or whatever we are now) in world space.
        Vector3 worldCenter = transform.position + cc.center;

        float radius = cc.radius;
        float halfHeight = Mathf.Max(cc.height * 0.5f, radius + 0.001f);
        float cylinder = halfHeight - radius;

        Vector3 p1 = worldCenter + Vector3.up * cylinder; // top sphere center
        Vector3 p2 = worldCenter - Vector3.up * cylinder; // bottom sphere center

        // Cast upward by the amount we'd grow, plus a tiny padding.
        float castDist = heightDelta + headroomCheckExtra;

        // If something blocks us above, we cannot stand.
        return !Physics.CapsuleCast(
            p1, p2,
            radius,
            Vector3.up,
            castDist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }


    private void SetCrouch(bool crouch)
    {
        if (crouch == isCrouched)
            return;

        if (!crouch)
        {
            // Standing up: only if headroom is clear
            if (!CanStandUp())
                return;
        }

        isCrouched = crouch;

        if (isCrouched)
        {
            cc.height = crouchHeight;
            cc.center = crouchCenter;
        }
        else
        {
            cc.height = standHeight;
            cc.center = standCenter;
        }
    }

    private void UpdateCrouchVisual(float dt)
    {
        if (visualModelRoot == null)
            return;

        Vector3 targetScale = visualDefaultLocalScale;
        Vector3 targetPos = visualDefaultLocalPos;

        if (isCrouched)
        {
            targetScale = new Vector3(visualDefaultLocalScale.x, visualDefaultLocalScale.y * crouchVisualScaleY, visualDefaultLocalScale.z);
            targetPos = visualDefaultLocalPos + new Vector3(0f, crouchVisualYOffset, 0f);
        }

        float t = 1f - Mathf.Exp(-visualCrouchLerpSpeed * dt);
        visualModelRoot.localScale = Vector3.Lerp(visualModelRoot.localScale, targetScale, t);
        visualModelRoot.localPosition = Vector3.Lerp(visualModelRoot.localPosition, targetPos, t);
    }
    private void ApplySlideSteering(Vector3 desiredDir, bool grounded, float dt)
    {
        if (!grounded || !isCrouched)
            return;

        // --- Build the 3 candidate directions ---
        Vector3 n = slideGroundNormalSmoothed;
        float angle = Vector3.Angle(n, Vector3.up);


        Vector3 downhill = Vector3.zero;

        if (useGroundDownhillInSlide && angle >= minDownhillAngle)
        {
            downhill = Vector3.ProjectOnPlane(Vector3.down, n);
            if (downhill.sqrMagnitude > 0.0001f) downhill.Normalize();
            else downhill = Vector3.zero;
        }



        Vector3 velDir = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity.normalized : Vector3.zero;
        Vector3 inputDir = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : Vector3.zero;

        // If we're basically not moving yet, let downhill (or input) start the slide.
        if (velDir == Vector3.zero)
            velDir = downhill != Vector3.zero ? downhill : inputDir;

        // --- Normalize weights so they always sum to 1 ---
        float wV = Mathf.Clamp01(slideWeightVelocity);
        float wD = Mathf.Clamp01(slideWeightDownhill);
        float wI = Mathf.Clamp01(slideWeightInput);
        wI *= Mathf.Clamp01(slideAccelMultiplier); // 0 -> no steering, 0.1 -> 10% steering, etc.

        float sum = wV + wD + wI;
        if (sum <= 0.0001f)
            return;

        wV /= sum;
        wD /= sum;
        wI /= sum;

        // If we don't have a valid dir for one component, redistribute its weight.
        // (Prevents "zero vectors" from weakening the blend.)
        if (downhill == Vector3.zero) { wV += wD; wI += 0f; wD = 0f; }
        if (inputDir == Vector3.zero) { wV += wI; wD += 0f; wI = 0f; }

        // Renormalize after redistribution (in case we zeroed something)
        sum = wV + wD + wI;
        if (sum <= 0.0001f)
            return;

        wV /= sum;
        wD /= sum;
        wI /= sum;

        // --- Blend directions (linear blend then normalize) ---
        Vector3 blendedDir =
            velDir * wV +
            downhill * wD +
            inputDir * wI;

        // Ensure slide stays on the ground plane
        blendedDir = Vector3.ProjectOnPlane(blendedDir, n);


        if (blendedDir.sqrMagnitude <= 0.0001f)
            return;

        blendedDir.Normalize();

        // ---- Pit stability: smooth direction + resist flip-flops at low speed ----
        float speed = planarVelocity.magnitude;

        // Initialize lastSlideDir if needed
        if (lastSlideDir.sqrMagnitude <= 0.0001f)
            lastSlideDir = blendedDir;

        // If we're slow, and the new dir wants to reverse, resist it hard
        float dot = Vector3.Dot(lastSlideDir, blendedDir);
        if (speed < slideMinStableSpeed && dot < 0f)
        {
            // blend back toward lastSlideDir (prevents spinning in pits)
            blendedDir = Vector3.Slerp(blendedDir, lastSlideDir, slideFlipResistance);
            blendedDir = Vector3.ProjectOnPlane(blendedDir, n);

            if (blendedDir.sqrMagnitude > 0.0001f)
                blendedDir.Normalize();
        }

        // Smooth final direction always (removes jitter from triangle normals)
        float tD = 1f - Mathf.Exp(-slideDirLerpSpeed * dt);
        blendedDir = Vector3.Slerp(lastSlideDir, blendedDir, tD);
        blendedDir = Vector3.ProjectOnPlane(blendedDir, n);

        if (blendedDir.sqrMagnitude > 0.0001f)
            blendedDir.Normalize();

        lastSlideDir = blendedDir;

        // Preserve speed, only redirect direction
        planarVelocity = blendedDir * speed;

    }


    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // We only care about steep "wall-like" hits this frame.
        float angle = Vector3.Angle(hit.normal, Vector3.up);

        if (!wallHitThisFrame || angle > wallAngleThisFrame)
        {
            wallHitThisFrame = true;
            wallNormalThisFrame = hit.normal;
            wallAngleThisFrame = angle;
        }
    }

    private void ApplySlideLandingBoostIfQualified(bool groundedNow, float slopeAngleNow, float dt)
    {
        if (!enableSlideLandingBoost)
            return;

        // Only when we actually "land" this frame.
        // NOTE: wasGrounded is updated later in Tick, so here it still represents LAST frame.
        bool landedThisFrame = groundedNow && !wasGrounded;
        if (!landedThisFrame)
            return;

        // Only reward if we're crouched (sliding state)
        if (!isCrouched || !slideHeldThisFrame)
            return;


        // Cooldown prevents multiple triggers due to contact jitter
        if (slideLandingBoostCooldownTimer > 0f)
            return;

        // Must be a proper slope landing
        if (slopeAngleNow < slideLandingMinSlopeAngle || slopeAngleNow > maxSlideSlopeAngle)
            return;

        // Need enough incoming planar speed
        float speed = planarVelocity.magnitude;
        if (speed < slideLandingMinPlanarSpeed)
            return;

        // Must be falling down into the slope (verticalVelocity is negative when falling)
        if (-verticalVelocity < slideLandingMinFallSpeed)
            return;

        // Compute downhill on this slope
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude <= 0.0001f)
            return;
        downhill.Normalize();

        Vector3 planarDir = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity.normalized : Vector3.zero;
        if (planarDir == Vector3.zero)
            return;

        // Must be aligned with downhill (skill check)
        float align = Vector3.Dot(planarDir, downhill); // 1=downhill, 0=sideways, -1=uphill

        // No reward below the minimum alignment
        if (align < slideLandingAlignMin)
            return;

        // Map align -> 0..1 where 0 = min, 1 = perfect
        float t = Mathf.InverseLerp(slideLandingAlignMin, slideLandingAlignPerfect, align);
        t = Mathf.Clamp01(t);

        // Shape the reward curve (more "skill pop" near perfect)
        float shaped = slideLandingRewardCurve != null ? slideLandingRewardCurve.Evaluate(t) : t;

        // Boost amount scales between min and max
        float boost = Mathf.Lerp(slideLandingBoostMin, slideLandingBoostMax, shaped);

        // Reward: add speed in your current travel direction
        planarVelocity += planarDir * boost;


        slideLandingBoostCooldownTimer = slideLandingBoostCooldown;
    }

}