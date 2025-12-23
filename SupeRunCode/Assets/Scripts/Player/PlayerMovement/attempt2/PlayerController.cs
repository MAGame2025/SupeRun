// PlayerController.cs
using UnityEngine;

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
    [SerializeField] private float slideDuration = 3f;
    [SerializeField] private float slideSpeedMultiplier = 1.5f;

    [Header("Facing / Rotation")]
    [Tooltip("How fast the model rotates to face movement direction.")]
    [SerializeField] private float rotateSpeed = 18f;

    [Tooltip("If true: when NOT moving, face camera forward. If false: keep last facing.")]
    [SerializeField] private bool faceCameraWhenIdle = false;

    [Header("References")]
    [SerializeField] private InputReader inputReader;

    private PlayerMotor motor;
    private int jumpsRemaining;
    private float slideTimer;

    private enum PlayerState { Idle, Walking, Running, Sliding, Jumping, Falling }
    private PlayerState currentState = PlayerState.Idle;

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
        bool slidePressed = inputReader.CrouchPressed;
        bool slideHeld = inputReader.CrouchHeld;
        bool jumpPressed = inputReader.JumpPressed;

        // Reset jump count when touching ground
        if (motor.IsGrounded && motor.VerticalVelocity <= 0f)
            jumpsRemaining = maxJumps;

        Vector3 wishDir = GetCameraRelativeDir(moveInput);

        // --- FSM transitions ---
        switch (currentState)
        {
            case PlayerState.Idle:
                if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (moveInput.sqrMagnitude > 0f) currentState = runHeld ? PlayerState.Running : PlayerState.Walking;
                break;

            case PlayerState.Walking:
                if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (runHeld && moveInput.sqrMagnitude > 0f) currentState = PlayerState.Running;
                else if (slidePressed && motor.HorizontalVelocity.magnitude > walkSpeed * 1.1f) StartSlide();
                else if (moveInput.sqrMagnitude == 0f) currentState = PlayerState.Idle;
                break;

            case PlayerState.Running:
                if (!motor.IsGrounded) currentState = PlayerState.Falling;
                else if (slidePressed) StartSlide();
                else if (moveInput.sqrMagnitude == 0f) currentState = PlayerState.Idle;
                else if (!runHeld) currentState = PlayerState.Walking;
                break;

            case PlayerState.Jumping:
                if (motor.VerticalVelocity < 0f) currentState = PlayerState.Falling;
                break;

            case PlayerState.Falling:
                if (motor.IsGrounded && motor.VerticalVelocity <= 0f) currentState = PlayerState.Idle;
                break;

            case PlayerState.Sliding:
                // handled below
                break;
        }

        // --- Jump ---
        if (currentState != PlayerState.Sliding && jumpPressed && jumpsRemaining > 0)
            PerformJump();

        // --- Movement + Facing (Update) ---
        if (currentState == PlayerState.Sliding)
        {
            // Slide cancel on jump
            if (jumpPressed && jumpsRemaining > 0)
            {
                PerformJump();
                return;
            }

            slideTimer -= Time.deltaTime;

            if (slideTimer <= 0f || !slideHeld)
            {
                currentState = PlayerState.Idle;
            }
            else
            {
                motor.ProcessSlidePhysics(wishDir);

                // While sliding, face the actual slide velocity (feels right)
                RotateTowardsVelocity(motor.HorizontalVelocity);
            }
        }
        else
        {
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
    }

    private void StartSlide()
    {
        currentState = PlayerState.Sliding;
        slideTimer = slideDuration;

        bool hasMomentum = motor.HorizontalVelocity.magnitude > walkSpeed;
        if (hasMomentum)
        {
            Vector3 boostDir = motor.HorizontalVelocity.normalized;
            if (boostDir == Vector3.zero) boostDir = transform.forward;

            float slideSpeed = runSpeed * slideSpeedMultiplier;
            motor.ApplySlideVelocity(boostDir * slideSpeed);
        }
        else
        {
            // If nearly standing, push off downhill
            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, motor.GroundNormal).normalized;
            Vector3 flatDir = new Vector3(downSlope.x, 0, downSlope.z).normalized;
            motor.ApplySlideVelocity(flatDir * 2f);
        }
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

    // Optional: keep FixedUpdate empty so we don't double-drive movement.
    private void FixedUpdate() { }
}
