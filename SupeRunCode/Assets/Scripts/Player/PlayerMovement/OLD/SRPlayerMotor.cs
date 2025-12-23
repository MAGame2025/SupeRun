using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SRPlayerMotor : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isSurfing;

    [Header("Core Physics")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float terminalVelocity = -50f;

    [Tooltip("How far below the controller we look for ground.")]
    [SerializeField] private float groundCheckDist = 0.35f;

    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Angle where we stop being 'walkable'. Above this -> surfing/slide down.")]
    [SerializeField] private float maxWalkSlope = 55f;

    [Header("Grounding / Slope Handling")]
    [Tooltip("Pull the controller down onto ground when grounded (fixes jitter on dunes).")]
    [SerializeField] private float groundSnapSpeed = 25f;

    [Tooltip("Add a small downward force so you don't float off terrain. (CharacterController quirk)")]
    [SerializeField] private float stickToGroundForce = 6f;

    [Tooltip("How long after leaving ground we still count as grounded (helps on bumpy slopes).")]
    [SerializeField] private float coyoteTime = 0.08f;

    [Header("Movement Smoothing")]
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 50f;
    [SerializeField] private float airAcceleration = 20f;
    [SerializeField] private float airFriction = 0f; // keep 0 for bunny hop feel

    [Header("Slide / Surf Physics")]
    [SerializeField] private float slideFriction = 2f;
    [SerializeField] private float slideSlopeAccel = 25f;
    [SerializeField] private float minSlideSlopeAngle = 5f;

    [Tooltip("Extra acceleration down steep slopes (surfing).")]
    [SerializeField] private float steepSlopeAccel = 35f;

    [Header("External Forces")]
    [SerializeField] private float externalPushDamping = 5f;
    [SerializeField] private float pushCooldown = 2f;

    [Header("Steep Slopes")]
    [SerializeField] private bool enableSteepSlopeSurfing = true;

    // VELOCITY STATE
    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public Vector3 HorizontalVelocity => PlanarVelocity;

    private CharacterController controller;

    private float jumpCooldownTimer;
    private float groundedGraceTimer;

    private Vector3 wallNormal;
    private bool isTouchingWall;

    private Vector3 externalPlanarVelocity;
    private float nextAllowedPushTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        currentSpeed = PlanarVelocity.magnitude;
        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= Time.deltaTime;
    }

    public void ApplyExternalPush(Vector3 push)
    {
        if (Time.time < nextAllowedPushTime) return;

        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f) return;

        externalPlanarVelocity += push;
        nextAllowedPushTime = Time.time + pushCooldown;
    }

    public void ProcessMove(Vector3 wishDir, float targetSpeed, bool isAirborne)
    {
        PerformGroundCheck();

        if (isGrounded) groundedGraceTimer = coyoteTime;
        else groundedGraceTimer -= Time.deltaTime;

        // If slope too steep -> surf down
        if (isSurfing)
        {
            HandleSteepSlopeSurf(wishDir, targetSpeed);
        }
        else if (IsEffectivelyGrounded())
        {
            ApplyGroundPhysics(wishDir, targetSpeed);
        }
        else
        {
            ApplyAirPhysics(wishDir, targetSpeed);
        }

        ApplyGravityAndMove();

        isTouchingWall = false;
    }

    public void ApplySlideVelocity(Vector3 velocity)
    {
        PlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
    }

    public void ProcessSlidePhysics()
    {
        PerformGroundCheck();

        bool effectivelyGrounded = IsEffectivelyGrounded();

        Vector3 downSlope = GetDownSlopeDir(GroundNormal);
        float slopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
        bool onSlope = slopeAngle > minSlideSlopeAngle;

        if (effectivelyGrounded && onSlope && !isSurfing)
        {
            Vector3 slopeDirFlat = new Vector3(downSlope.x, 0f, downSlope.z).normalized;
            PlanarVelocity += slopeDirFlat * slideSlopeAccel * Time.deltaTime;
        }
        else if (effectivelyGrounded && isSurfing)
        {
            PlanarVelocity += new Vector3(downSlope.x, 0f, downSlope.z).normalized * steepSlopeAccel * Time.deltaTime;
        }
        else
        {
            float speed = PlanarVelocity.magnitude;
            speed = Mathf.MoveTowards(speed, 0f, slideFriction * Time.deltaTime);
            PlanarVelocity = (PlanarVelocity.sqrMagnitude > 0.0001f) ? PlanarVelocity.normalized * speed : Vector3.zero;
        }

        ApplyGravityAndMove();
    }

    public void ForceJump(float force)
    {
        VerticalVelocity = force;
        isGrounded = false;
        isSurfing = false;

        jumpCooldownTimer = 0.15f;
        groundedGraceTimer = 0f;

        controller.Move(Vector3.up * 0.05f);
    }

    private void ApplyGroundPhysics(Vector3 wishDir, float targetSpeed)
    {
        Vector3 wishOnPlane = Vector3.ProjectOnPlane(wishDir, GroundNormal).normalized;
        if (wishDir.sqrMagnitude < 0.0001f) wishOnPlane = Vector3.zero;

        float currentMag = PlanarVelocity.magnitude;
        float accel = (targetSpeed > currentMag) ? groundAcceleration : groundDeceleration;

        Vector3 targetVel = wishOnPlane * targetSpeed;
        PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, targetVel, accel * Time.deltaTime);
    }

    private void ApplyAirPhysics(Vector3 wishDir, float targetSpeed)
    {
        if (isTouchingWall && Vector3.Dot(wishDir, wallNormal) < 0f)
        {
            wishDir = Vector3.ProjectOnPlane(wishDir, wallNormal).normalized;
        }

        float currentProjSpeed = Vector3.Dot(PlanarVelocity, wishDir);
        float addSpeed = targetSpeed - currentProjSpeed;

        if (addSpeed > 0f)
        {
            float accelSpeed = airAcceleration * targetSpeed * Time.deltaTime;
            accelSpeed = Mathf.Min(accelSpeed, addSpeed);
            PlanarVelocity += wishDir * accelSpeed;
        }

        if (airFriction > 0f)
        {
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, Vector3.zero, airFriction * Time.deltaTime);
        }
    }

    private void HandleSteepSlopeSurf(Vector3 wishDir, float targetSpeed)
    {
        Vector3 downSlope = GetDownSlopeDir(GroundNormal);
        Vector3 downFlat = new Vector3(downSlope.x, 0f, downSlope.z).normalized;

        PlanarVelocity += downFlat * steepSlopeAccel * Time.deltaTime;

        // Limited steering while surfing (keeps control but doesn't kill downhill feel)
        Vector3 steer = Vector3.ProjectOnPlane(wishDir, GroundNormal).normalized;
        if (steer.sqrMagnitude > 0.0001f)
        {
            float steerSpeed = Mathf.Min(targetSpeed, 10f);
            Vector3 desired = steer * steerSpeed + downFlat * PlanarVelocity.magnitude;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, desired, 10f * Time.deltaTime);
        }
    }

    private void ApplyGravityAndMove()
    {
        if (externalPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity,
                Vector3.zero,
                externalPushDamping * Time.deltaTime);
        }

        Vector3 effectivePlanar = PlanarVelocity + externalPlanarVelocity;
        bool effectivelyGrounded = IsEffectivelyGrounded();

        if (effectivelyGrounded && VerticalVelocity < 0f)
        {
            VerticalVelocity = -2f;
        }
        else
        {
            VerticalVelocity += gravity * Time.deltaTime;
            if (VerticalVelocity < terminalVelocity)
                VerticalVelocity = terminalVelocity;
        }

        Vector3 finalPlanar = effectivePlanar;

        if (effectivelyGrounded && !isSurfing && effectivePlanar.sqrMagnitude > 0.0001f)
        {
            Vector3 onPlane = Vector3.ProjectOnPlane(effectivePlanar, GroundNormal);
            if (onPlane.sqrMagnitude > 0.0001f)
                finalPlanar = onPlane;
        }

        float extraDown = 0f;
        if (effectivelyGrounded && !isSurfing)
        {
            extraDown = stickToGroundForce;
        }

        Vector3 motion = (finalPlanar + Vector3.up * (VerticalVelocity - extraDown)) * Time.deltaTime;
        CollisionFlags flags = controller.Move(motion);

        if (effectivelyGrounded && !isSurfing && jumpCooldownTimer <= 0f)
        {
            SnapToGround();
        }

        if ((flags & CollisionFlags.Above) != 0 && VerticalVelocity > 0f)
            VerticalVelocity = 0f;
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float radius = Mathf.Max(0.2f, controller.radius * 0.9f);
        float dist = groundCheckDist + 0.6f;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= maxWalkSlope)
            {
                float desiredY = hit.point.y;
                float currentY = transform.position.y;

                float delta = desiredY - currentY;
                if (delta < 0f && Mathf.Abs(delta) < 0.5f)
                {
                    float snap = Mathf.Clamp(delta, -groundSnapSpeed * Time.deltaTime, 0f);
                    controller.Move(new Vector3(0f, snap, 0f));
                }
            }
        }
    }

    private void PerformGroundCheck()
    {
        if (jumpCooldownTimer > 0f)
        {
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);
        float radius = Mathf.Max(0.2f, controller.radius * 0.9f);
        float dist = (controller.height * 0.5f) + groundCheckDist;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            GroundNormal = hit.normal;

            if (angle > maxWalkSlope)
            {
                if (enableSteepSlopeSurfing)
                {
                    isGrounded = false;   // not walkable
                    isSurfing = true;     // surf/slide mode
                }
                else
                {
                    // Optional behavior: treat steep slope as "walkable" ground
                    // (lets you climb dunes without being forced to slide backwards)
                    isGrounded = true;
                    isSurfing = false;
                }
            }
            else
            {
                isGrounded = true;
                isSurfing = false;
            }
        }
        else
        {
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
        }
    }


    private bool IsEffectivelyGrounded()
    {
        if (isGrounded) return true;
        return groundedGraceTimer > 0f;
    }

    private Vector3 GetDownSlopeDir(Vector3 groundNormal)
    {
        Vector3 down = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (down.sqrMagnitude < 0.0001f) return Vector3.zero;
        return down.normalized;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!controller.isGrounded && hit.normal.y < 0.1f)
        {
            isTouchingWall = true;
            wallNormal = hit.normal;
        }
    }
}
