using UnityEngine;

[RequireComponent(typeof(UnityEngine.CharacterController))]
public class SRPlayerMotor : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isSurfing;

    [Header("Core Physics")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float terminalVelocity = -50f;
    [SerializeField] private float groundCheckDist = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float maxWalkSlope = 55f;

    [Header("Movement Smoothing")]
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 50f;
    [SerializeField] private float airAcceleration = 20f;
    [SerializeField] private float airRotationSpeed = 15f;
    [SerializeField] private float airFriction = 0f; // Keep 0 for bunny hopping

    [Header("Slide Physics")]
    [SerializeField] private float slideFriction = 2f;
    [SerializeField] private float slideSlopeAccel = 25f;
    [SerializeField] private float minSlideSlopeAngle = 5f;

    [Header("External Forces")]
    [Tooltip("How quickly external pushes (from enemies, explosions, etc.) decay back to zero.")]
    [SerializeField] private float externalPushDamping = 5f;

    [Tooltip("Cooldown between being pushed by enemies (seconds).")]
    [SerializeField] private float pushCooldown = 2f;

    // VELOCITY STATE (logical movement)
    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal { get; private set; }

    public Vector3 HorizontalVelocity => PlanarVelocity;

    private UnityEngine.CharacterController controller;
    private float jumpCooldownTimer;
    private Vector3 wallNormal;
    private bool isTouchingWall;

    // external push that decays over time
    private Vector3 externalPlanarVelocity;
    private float nextAllowedPushTime;

    private void Awake()
    {
        controller = GetComponent<UnityEngine.CharacterController>();
    }

    private void Update()
    {
        currentSpeed = PlanarVelocity.magnitude;
        if (jumpCooldownTimer > 0) jumpCooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Called by enemies to shove the player in XZ.
    /// Can only actually apply a push once every 'pushCooldown' seconds.
    /// </summary>
    public void ApplyExternalPush(Vector3 push)
    {
        if (Time.time < nextAllowedPushTime)
            return;

        // Only planar part
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f)
            return;

        externalPlanarVelocity += push;
        nextAllowedPushTime = Time.time + pushCooldown;
    }

    public void ProcessMove(Vector3 wishDir, float targetSpeed, bool isAirborne)
    {
        PerformGroundCheck();

        if (isSurfing)
        {
            HandleSurfingPhysics();
        }

        if (isGrounded && !isSurfing)
        {
            float currentMag = PlanarVelocity.magnitude;
            float accel = (targetSpeed > currentMag) ? groundAcceleration : groundDeceleration;

            Vector3 targetVel = wishDir * targetSpeed;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, targetVel, accel * Time.deltaTime);

            if (PlanarVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(PlanarVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 15f * Time.deltaTime);
            }
        }
        else
        {
            ApplyAirPhysics(wishDir, targetSpeed);
        }

        ApplyGravityAndMove();
        isTouchingWall = false;
    }

    private void ApplyAirPhysics(Vector3 wishDir, float targetSpeed)
    {
        if (isTouchingWall && Vector3.Dot(wishDir, wallNormal) < 0)
        {
            wishDir = Vector3.ProjectOnPlane(wishDir, wallNormal).normalized;
        }

        float currentProjSpeed = Vector3.Dot(PlanarVelocity, wishDir);
        float addSpeed = targetSpeed - currentProjSpeed;

        if (addSpeed > 0)
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
        Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
        PlanarVelocity += downSlope * slideSlopeAccel * Time.deltaTime;

        if (Vector3.Dot(PlanarVelocity, downSlope) < 0)
        {
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, Vector3.zero, 10f * Time.deltaTime);
        }
    }

    public void ApplySlideVelocity(Vector3 velocity)
    {
        PlanarVelocity = new Vector3(velocity.x, 0, velocity.z);
    }

    public void ProcessSlidePhysics()
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

        if (PlanarVelocity.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(PlanarVelocity.normalized, Vector3.up);

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
        // Decay external push
        if (externalPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity,
                Vector3.zero,
                externalPushDamping * Time.deltaTime);
        }

        // Combine logical and external planar velocities
        Vector3 effectivePlanarVelocity = PlanarVelocity + externalPlanarVelocity;

        // Gravity
        if (isGrounded && VerticalVelocity < 0f)
        {
            VerticalVelocity = -2f;
        }
        else
        {
            VerticalVelocity += gravity * Time.deltaTime;
            if (VerticalVelocity < terminalVelocity)
                VerticalVelocity = terminalVelocity;
        }

        Vector3 finalMove = effectivePlanarVelocity;

        if (isGrounded && !isSurfing && effectivePlanarVelocity.sqrMagnitude > 0.0001f)
        {
            finalMove = Vector3.ProjectOnPlane(effectivePlanarVelocity, GroundNormal).normalized
                        * effectivePlanarVelocity.magnitude;
        }

        Vector3 motion = (finalMove + Vector3.up * VerticalVelocity) * Time.deltaTime;
        controller.Move(motion);
    }

    private void PerformGroundCheck()
    {
        if (jumpCooldownTimer > 0)
        {
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float radius = 0.3f;
        float dist = 0.5f + groundCheckDist;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            if (angle > maxWalkSlope)
            {
                isGrounded = false;
                isSurfing = true;
                GroundNormal = hit.normal;
            }
            else
            {
                isGrounded = true;
                isSurfing = false;
                GroundNormal = hit.normal;
            }
        }
        else
        {
            isGrounded = false;
            isSurfing = false;
            GroundNormal = Vector3.up;
        }
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
