// PlayerController.cs
using UnityEngine;

// Handles player input and state machine, delegating actual movement to PlayerMotor.
// Crouch state removed; crouch input now triggers slide when moving.
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 8f;
    [SerializeField] private float runSpeed = 14f;
    //[SerializeField] private float crouchSpeed = 5f; // (Removed – no crouch state)
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private int maxJumps = 3;

    [Header("Slide Settings")]
    [SerializeField] private float slideDuration = 3f;
    [SerializeField] private float slideSpeedMultiplier = 1.5f;
    [Tooltip("Slope angle above which standing still can auto-start a slide (optional)")]
    [SerializeField] private float minSlideStartAngle = 10f;

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

        // FSM: state transitions
        switch (currentState)
        {
            case PlayerState.Idle:
                if (!motor.IsGrounded)
                {
                    currentState = PlayerState.Falling;
                }
                else if (moveInput.sqrMagnitude > 0f)
                {
                    currentState = runHeld ? PlayerState.Running : PlayerState.Walking;
                }
                break;

            case PlayerState.Walking:
                if (!motor.IsGrounded)
                {
                    currentState = PlayerState.Falling;
                }
                else if (runHeld && moveInput.sqrMagnitude > 0f)
                {
                    currentState = PlayerState.Running;
                }
                else if (slidePressed && motor.HorizontalVelocity.magnitude > walkSpeed * 1.1f)
                {
                    // Start slide if moving fast enough
                    StartSlide();
                }
                else if (moveInput.sqrMagnitude == 0f)
                {
                    currentState = PlayerState.Idle;
                }
                break;

            case PlayerState.Running:
                if (!motor.IsGrounded)
                {
                    currentState = PlayerState.Falling;
                }
                else if (slidePressed)
                {
                    StartSlide();
                }
                else if (moveInput.sqrMagnitude == 0f)
                {
                    currentState = PlayerState.Idle;
                }
                else if (!runHeld)
                {
                    currentState = PlayerState.Walking;
                }
                break;

            case PlayerState.Sliding:
                // Handled below
                break;

            case PlayerState.Jumping:
                if (motor.VerticalVelocity < 0f)
                    currentState = PlayerState.Falling;
                break;

            case PlayerState.Falling:
                if (motor.IsGrounded && motor.VerticalVelocity <= 0f)
                    currentState = PlayerState.Idle;
                break;
        }

        // State execution
        if (currentState == PlayerState.Sliding)
        {
            // Slide-cancel on jump
            if (jumpPressed && jumpsRemaining > 0)
            {
                PerformJump();
                return;
            }
            slideTimer -= Time.deltaTime;
            // End slide if timer runs out or slide key released
            if (slideTimer <= 0f || !slideHeld)
            {
                currentState = PlayerState.Idle;
                return;
            }
            // Note: actual sliding physics applied in FixedUpdate
        }
        else
        {
            // Jumping
            if (jumpPressed && jumpsRemaining > 0)
            {
                PerformJump();
            }
            // Normal movement is applied in FixedUpdate
        }
    }

    private void FixedUpdate()
    {
        Vector2 moveInput = inputReader.Move;
        Vector3 wishDir = GetCameraRelativeDir(moveInput);

        // Decide desired speed purely from input (and run key),
        // NOT from Jumping/Falling state.
        float targetSpeed = 0f;

        if (moveInput.sqrMagnitude > 0.001f)
        {
            bool runHeld = inputReader.RunHeld;
            targetSpeed = runHeld ? runSpeed : walkSpeed;
        }

        if (currentState == PlayerState.Sliding)
        {
            // Sliding uses its own physics path
            motor.ProcessSlidePhysics(wishDir);
        }
        else
        {
            bool isAirborne = !motor.IsGrounded;
            motor.ProcessMove(wishDir, targetSpeed, isAirborne);
        }
    }

    private void StartSlide()
    {
        currentState = PlayerState.Sliding;
        slideTimer = slideDuration;

        bool hasMomentum = motor.HorizontalVelocity.magnitude > walkSpeed;
        if (hasMomentum)
        {
            // Boost velocity in current direction
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

    private Vector3 GetCameraRelativeDir(Vector2 input)
    {
        if (Camera.main == null) return new Vector3(input.x, 0, input.y);
        Vector3 camFwd = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camFwd.y = 0f; camRight.y = 0f;
        return (camFwd.normalized * input.y + camRight.normalized * input.x).normalized;
    }
}
