using UnityEngine;

// Handles low-level physics via CharacterController: movement, sliding, jumping, etc.
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private float currentSpeed;
    public float CurrentSpeed => currentSpeed;

    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isSurfing;

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
    [SerializeField] private float airRotationSpeed = 15f;

    [Header("Slide Physics")]
    [SerializeField] private float slideFriction = 2f;
    [SerializeField] private float slideSlopeAccel = 25f;
    [SerializeField] private float minSlideSlopeAngle = 5f;

    [Header("External Forces")]
    [SerializeField] private float externalPushDamping = 5f;
    [SerializeField] private float pushCooldown = 2f;

    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }
    public bool IsGrounded => isGrounded;
    public bool IsSurfing => isSurfing;
    public Vector3 GroundNormal { get; private set; }
    public Vector3 HorizontalVelocity => PlanarVelocity;

    private CharacterController controller;
    private float jumpCooldownTimer;
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
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        if (externalPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity, Vector3.zero, externalPushDamping * Time.deltaTime);
        }
    }

    public void ProcessMove(Vector3 wishDir, float targetSpeed, bool isAirborne)
    {
        PerformGroundCheck();

        if (isSurfing)
        {
            // Add acceleration down steep slope
            HandleSurfingPhysics();
        }

        // Walkable ground vs air/surfing
        if (isGrounded && !isSurfing)
        {
            float currentMag = PlanarVelocity.magnitude;
            float accel = (targetSpeed > currentMag) ? groundAcceleration : groundDeceleration;
            Vector3 targetVel = wishDir * targetSpeed;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, targetVel, accel * Time.deltaTime);

            if (PlanarVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(PlanarVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, airRotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Air or surfing control (limited but still there)
            ApplyAirPhysics(wishDir, targetSpeed);
        }

        ApplyGravityAndMove();
        isTouchingWall = false;
    }

    private void ApplyAirPhysics(Vector3 wishDir, float targetSpeed)
    {
        // If sliding along a wall/slope, project input so we don't push INTO the wall
        if (isTouchingWall && Vector3.Dot(wishDir, wallNormal) < 0f)
        {
            wishDir = Vector3.ProjectOnPlane(wishDir, wallNormal).normalized;
        }

        float projSpeed = Vector3.Dot(PlanarVelocity, wishDir);
        float addSpeed = targetSpeed - projSpeed;
        if (addSpeed > 0f)
        {
            float accelSpeed = airAcceleration * targetSpeed * Time.deltaTime;
            accelSpeed = Mathf.Min(accelSpeed, addSpeed);
            PlanarVelocity += wishDir * accelSpeed;
        }

        if (wishDir.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, airRotationSpeed * Time.deltaTime);
        }
    }

    private void HandleSurfingPhysics()
    {
        // TRUE downhill vector based on surface normal
        Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;

        // Add gravity-like acceleration down the slope
        PlanarVelocity += downSlope * slideSlopeAccel * Time.deltaTime;

        // Friction if velocity is against the slope
        if (Vector3.Dot(PlanarVelocity, downSlope) < 0f)
        {
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, Vector3.zero, slideFriction * Time.deltaTime);
        }
    }

    public void ApplySlideVelocity(Vector3 velocity)
    {
        PlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
    }

    public void ProcessSlidePhysics(Vector3 wishDir)
    {
        PerformGroundCheck();

        Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
        float slopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
        bool onSlope = slopeAngle > minSlideSlopeAngle;

        if (isGrounded && onSlope && !isSurfing)
        {
            Vector3 slopeDirFlat = new Vector3(downSlope.x, 0, downSlope.z).normalized;
            PlanarVelocity += slopeDirFlat * slideSlopeAccel * Time.deltaTime;
        }
        else
        {
            float speed = PlanarVelocity.magnitude;
            speed = Mathf.MoveTowards(speed, 0f, slideFriction * Time.deltaTime);
            PlanarVelocity = PlanarVelocity.normalized * speed;
        }

        // Slide steering
        if (wishDir.sqrMagnitude > 0.1f)
        {
            float controlFactor = 0.5f; // tweak this (0.3–0.8) for feel
            Vector3 targetVel = wishDir.normalized * PlanarVelocity.magnitude;
            PlanarVelocity = Vector3.MoveTowards(
                PlanarVelocity, targetVel, controlFactor * airAcceleration * Time.deltaTime);
        }

        if (PlanarVelocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(PlanarVelocity.normalized, Vector3.up);
        }

        ApplyGravityAndMove();
    }

    public void ForceJump(float force)
    {
        VerticalVelocity = force;
        isGrounded = false;
        isSurfing = false;
        jumpCooldownTimer = 0.2f;
        controller.Move(Vector3.up * 0.05f);
    }

    private void ApplyGravityAndMove()
    {
        Vector3 effectivePlanar = PlanarVelocity + externalPlanarVelocity;

        // --- GRAVITY LOGIC ---
        if (isGrounded)
        {
            // Grounded (walkable or surfing): small constant downward to stay "glued"
            VerticalVelocity = -5f;
        }
        else
        {
            // In Air
            VerticalVelocity += gravity * Time.deltaTime;
            VerticalVelocity = Mathf.Max(VerticalVelocity, terminalVelocity);
        }

        // --- PROJECTION LOGIC (jitter-safe) ---
        Vector3 finalPlanar = effectivePlanar;

        if (isGrounded)
        {
            // Whether surfing or on gentle ground, we only project the PLANAR part
            // This avoids fighting CC by projecting away gravity.
            finalPlanar = Vector3.ProjectOnPlane(effectivePlanar, GroundNormal);
        }

        Vector3 finalVelocity = finalPlanar + Vector3.up * VerticalVelocity;
        Vector3 motion = finalVelocity * Time.deltaTime;
        controller.Move(motion);
    }

    private void PerformGroundCheck()
    {
        if (jumpCooldownTimer > 0f)
        {
            // Short grace period after jump so we don't instantly re-ground mid-jump
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
            return;
        }

        // Cast from just above the feet
        float radius = controller != null ? controller.radius * 0.9f : 0.3f;
        Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);

        // How far below we look for ground
        float dist = groundCheckDistance + 0.2f;

        if (Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out RaycastHit hit,
                dist,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            GroundNormal = hit.normal;
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            // We hit something below us → we're grounded
            isGrounded = true;

            // Mark steep slopes as "surfing"
            isSurfing = angle > maxWalkSlope;
        }
        else
        {
            // Nothing under us → we're in the air
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
        }
    }


    public void ApplyExternalPush(Vector3 push)
    {
        if (Time.time < nextAllowedPushTime) return;
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f) return;
        externalPlanarVelocity += push;
        nextAllowedPushTime = Time.time + pushCooldown;
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
