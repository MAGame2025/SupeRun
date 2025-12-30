using UnityEngine;
using UnityEngine.Serialization;

// Handles player input and state machine, delegating actual movement to PlayerMotor.
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 8f;
    [SerializeField] private float runSpeed = 14f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private int maxJumps = 3;

    [Header("Slide Settings")]
    [Tooltip("Slide speed boost (multiplies runSpeed) applied ONLY when starting slide while grounded.")]
    [FormerlySerializedAs("slideSpeedMultiplier")]
    [SerializeField] private float slideSpeedBoost = 1.5f; // "Slide speed boost"

    [Header("Facing / Rotation")]
    [Tooltip("How fast the model rotates to face movement direction.")]
    [SerializeField] private float rotateSpeed = 18f;
    [Tooltip("If true: when NOT moving, face camera forward. If false: keep last facing.")]
    [SerializeField] private bool faceCameraWhenIdle = false;

    [Header("References")]
    [SerializeField] private InputReader inputReader;

    private PlayerMotor motor;
    private int jumpsRemaining;

    private enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Sliding,
        Jumping,
        Falling
    }

    [Header("Debug")]
    [SerializeField] private PlayerState currentState = PlayerState.Idle;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        if (inputReader == null)
            inputReader = FindAnyObjectByType<InputReader>();
    }

    private void Update()
    {
        // Read inputs
        Vector2 moveInput = inputReader.Move;
        bool runHeld = inputReader.RunHeld;
        bool crouchPressed = inputReader.CrouchPressed; // Ctrl down this frame
        bool crouchHeld = inputReader.CrouchHeld; // Ctrl currently held
        bool jumpPressed = inputReader.JumpPressed;

        // Reset jump count when touching ground
        if (motor.IsGrounded && motor.VerticalVelocity <= 0f)
            jumpsRemaining = maxJumps;

        Vector3 wishDir = GetCameraRelativeDir(moveInput);

        // --- ENTER SLIDE: pressing Ctrl always puts you in Sliding (ground or air) ---
        if (crouchPressed && currentState != PlayerState.Sliding)
        {
            bool groundedStart = motor.IsGrounded;
            StartSlide(groundedStart);
        }

        // --- EXIT SLIDE: releasing Ctrl always exits Sliding ---
        if (!crouchHeld && currentState == PlayerState.Sliding)
        {
            ExitSlide(moveInput, runHeld);
            // Continue executing this frame as non-sliding movement
        }

        // --- FSM transitions (excluding Sliding which is driven by crouchHeld) ---
        switch (currentState)
        {
            case PlayerState.Idle:
                if (crouchHeld) currentState = PlayerState.Sliding; // safety: if held at start
                else if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (moveInput.sqrMagnitude > 0f) currentState = runHeld ? PlayerState.Running : PlayerState.Walking;
                break;

            case PlayerState.Walking:
                if (crouchHeld) currentState = PlayerState.Sliding;
                else if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (runHeld && moveInput.sqrMagnitude > 0f) currentState = PlayerState.Running;
                else if (moveInput.sqrMagnitude == 0f) currentState = PlayerState.Idle;
                break;

            case PlayerState.Running:
                if (crouchHeld) currentState = PlayerState.Sliding;
                else if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (moveInput.sqrMagnitude == 0f) currentState = PlayerState.Idle;
                else if (!runHeld) currentState = PlayerState.Walking;
                break;

            case PlayerState.Jumping:
                if (crouchHeld) currentState = PlayerState.Sliding; // allow enter slide in air
                else if (motor.VerticalVelocity < 0f) currentState = PlayerState.Falling;
                break;

            case PlayerState.Falling:
                if (crouchHeld) currentState = PlayerState.Sliding; // Falling -> Sliding when holding Ctrl
                else if (motor.IsGrounded && motor.VerticalVelocity <= 0f)
                    currentState = moveInput.sqrMagnitude > 0f ? (runHeld ? PlayerState.Running : PlayerState.Walking) : PlayerState.Idle;
                break;

            case PlayerState.Sliding:
                // handled below
                break;
        }

        // --- Jump ---
        if (currentState != PlayerState.Sliding && jumpPressed && jumpsRemaining > 0)
            PerformJump();

        // --- Sliding behavior (ground + air) ---
        if (currentState == PlayerState.Sliding)
        {
            // If player let go of Ctrl, ExitSlide() already handled it above.
            if (!crouchHeld)
            {
                // just in case, bail out
                return;
            }

            // Slide cancel on jump
            if (jumpPressed && jumpsRemaining > 0)
            {
                PerformJump();
                return;
            }

            // Keep sliding as long as Ctrl is held (even in air)
            motor.ProcessSlidePhysics(wishDir);

            // Face slide velocity (feels right + works for air slide)
            RotateTowardsVelocity(motor.HorizontalVelocity);
            return;
        }

        // --- Normal movement (non-sliding) ---
        float targetSpeed = 0f;
        if (moveInput.sqrMagnitude > 0.001f)
            targetSpeed = runHeld ? runSpeed : walkSpeed;

        bool isAirborne = !motor.IsGrounded;
        motor.ProcessMove(wishDir, targetSpeed, isAirborne);

        // Strafe-style: when moving, face camera forward (mouse direction)
        if (moveInput.sqrMagnitude > 0.001f)
        {
            RotateTowardsDirection(GetCameraForwardFlat());
        }
        else if (faceCameraWhenIdle)
        {
            RotateTowardsDirection(GetCameraForwardFlat());
        }
    }

    private void StartSlide(bool groundedStart)
    {
        currentState = PlayerState.Sliding;

        // If we started the slide while airborne, DO NOT boost velocity.
        if (!groundedStart)
            return;

        float currentSpeed = motor.HorizontalVelocity.magnitude;

        // If we have momentum, keep it and optionally add a bonus.
        if (currentSpeed > 0.1f)
        {
            Vector3 dir = motor.HorizontalVelocity.normalized;
            if (dir == Vector3.zero) dir = transform.forward;

            // slideSpeedBoost now acts as an ADDITIVE bonus in units of (runSpeed * boost).
            // With boost = 0 -> no bonus, speed stays the same.
            // With boost = 1 -> bonus = runSpeed (so you gain +14 by default).
            // If you want a smaller effect, set boost to 0.1..0.4.
            float bonus = runSpeed * slideSpeedBoost;

            float newSpeed = currentSpeed + bonus;

            motor.ApplySlideVelocity(dir * newSpeed);
            return;
        }

        // If nearly standing still, push off downhill a bit (or forward if flat)
        Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, motor.GroundNormal);
        Vector3 flatDir = new Vector3(downSlope.x, 0f, downSlope.z);

        if (flatDir.sqrMagnitude > 0.0001f)
            flatDir.Normalize();
        else
            flatDir = transform.forward;

        motor.ApplySlideVelocity(flatDir * 2f);
    }

    private void ExitSlide(Vector2 moveInput, bool runHeld)
    {
        if (!motor.IsGrounded)
        {
            currentState = PlayerState.Falling;
            return;
        }

        if (moveInput.sqrMagnitude > 0.001f)
            currentState = runHeld ? PlayerState.Running : PlayerState.Walking;
        else
            currentState = PlayerState.Idle;
    }

    private void PerformJump()
    {
        motor.ForceJump(jumpForce);
        jumpsRemaining--;
        currentState = PlayerState.Jumping;
    }

    private void RotateTowardsDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotateSpeed * Time.deltaTime);
    }

    private void RotateTowardsVelocity(Vector3 vel)
    {
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(vel.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotateSpeed * Time.deltaTime);
    }

    private Vector3 GetCameraRelativeDir(Vector2 input)
    {
        if (Camera.main == null)
            return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 camFwd = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        camFwd.y = 0f;
        camRight.y = 0f;

        Vector3 dir = (camFwd.normalized * input.y + camRight.normalized * input.x);
        if (dir.sqrMagnitude < 0.0001f) return Vector3.zero;
        return dir.normalized;
    }

    private Vector3 GetCameraForwardFlat()
    {
        if (Camera.main == null) return transform.forward;

        Vector3 fwd = Camera.main.transform.forward;
        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f) return transform.forward;
        return fwd.normalized;
    }

}
