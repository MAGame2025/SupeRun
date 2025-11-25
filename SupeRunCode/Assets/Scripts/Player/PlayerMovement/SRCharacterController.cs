using UnityEngine;

//asd

[RequireComponent(typeof(SRPlayerMotor))]

public class SRCharacterController : MonoBehaviour

{

    [Header("Settings")]

    [SerializeField] private float walkSpeed = 8f;

    [SerializeField] private float runSpeed = 14f;

    [SerializeField] private float crouchSpeed = 5f;

    [SerializeField] private float jumpForce = 7f;

    [SerializeField] private int maxJumps = 3;



    [Header("Slide Settings")]

    [SerializeField] private float slideDuration = 3f;

    [SerializeField] private float slideSpeedMultiplier = 1.5f;

    [Tooltip("Minimum slope angle to allow sliding from a standstill.")]

    [SerializeField] private float minSlideStartAngle = 10f;



    [Header("References")]

    [SerializeField] private InputReader inputReader;



    private SRPlayerMotor motor;

    private int jumpsRemaining;

    private float slideTimer;



    public enum PlayerState { Idle, Walking, Running, Crouching, Jumping, Falling, Sliding }

    public PlayerState currentState = PlayerState.Idle;



    private void Awake()

    {

        motor = GetComponent<SRPlayerMotor>();

        if (inputReader == null)

            inputReader = FindAnyObjectByType<InputReader>();

    }



    private void Update()

    {

        HandleStateLogic();

    }



    private void HandleStateLogic()

    {

        // Reset Jumps

        if (motor.IsGrounded && motor.VerticalVelocity <= 0)

            jumpsRemaining = maxJumps;



        // Input

        Vector2 input = inputReader.Move;

        Vector3 wishDir = GetCameraRelativeDir(input);

        bool wantsRun = inputReader.RunHeld;



        // Pure FSM

        switch (currentState)

        {

            case PlayerState.Idle:

                if (!motor.IsGrounded) SwitchState(PlayerState.Falling);

                else if (inputReader.CrouchHeld) SwitchState(PlayerState.Crouching);

                else if (input.sqrMagnitude > 0) SwitchState(wantsRun ? PlayerState.Running : PlayerState.Walking);

                break;



            case PlayerState.Walking:

                if (!motor.IsGrounded)

                    SwitchState(PlayerState.Falling);

                else if (wantsRun && input.sqrMagnitude > 0)

                    SwitchState(PlayerState.Running); // Priority: Run

                else if (inputReader.CrouchPressed) // Priority: Crouch/Slide

                {

                    if (motor.HorizontalVelocity.magnitude > walkSpeed + 1f) StartSlide();

                    else SwitchState(PlayerState.Crouching);

                }

                else if (input.sqrMagnitude == 0)

                    SwitchState(PlayerState.Idle);

                break;



            case PlayerState.Running:

                if (!motor.IsGrounded)

                    SwitchState(PlayerState.Falling);

                else if (inputReader.CrouchPressed)

                    StartSlide();

                else if (input.sqrMagnitude == 0)

                    SwitchState(PlayerState.Idle);

                else if (!wantsRun)

                    SwitchState(PlayerState.Walking);

                break;



            case PlayerState.Crouching:

                if (!motor.IsGrounded)

                {

                    SwitchState(PlayerState.Falling);

                }

                else if (IsOnSteepSlope())

                {

                    StartSlide();

                }

                else if (!inputReader.CrouchHeld)

                {

                    SwitchState(PlayerState.Idle);

                }

                break;



            case PlayerState.Sliding:

                HandleSlidingLogic();

                break;



            case PlayerState.Jumping:

                if (motor.VerticalVelocity < 0f) SwitchState(PlayerState.Falling);

                break;



            case PlayerState.Falling:

                if (motor.IsGrounded && motor.VerticalVelocity <= 0) SwitchState(PlayerState.Idle);

                break;

        }



        // Execution

        if (currentState != PlayerState.Sliding)

        {

            float targetSpeed = walkSpeed;

            if (currentState == PlayerState.Running) targetSpeed = runSpeed;

            if (currentState == PlayerState.Crouching) targetSpeed = crouchSpeed;



            if (inputReader.JumpPressed && jumpsRemaining > 0)

            {

                PerformJump();

            }



            motor.ProcessMove(wishDir, targetSpeed, !motor.IsGrounded);

        }

    }

    private void StartSlide()

    {

        SwitchState(PlayerState.Sliding);

        slideTimer = slideDuration;



        // Check momentum to decide boost

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

            // Slope Start: Just ensure we have non-zero Planar velocity in the slope direction

            // The Motor will handle the gravity acceleration

            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, motor.GroundNormal).normalized;

            Vector3 flatDir = new Vector3(downSlope.x, 0, downSlope.z).normalized;

            motor.ApplySlideVelocity(flatDir * 2f); // Tiny push

        }

    }



    private void HandleSlidingLogic()

    {

        slideTimer -= Time.deltaTime;



        if (inputReader.JumpPressed && jumpsRemaining > 0)

        {

            PerformJump();

            return;

        }



        if (slideTimer <= 0 || !inputReader.CrouchHeld)

        {

            SwitchState(PlayerState.Idle);

            return;

        }



        motor.ProcessSlidePhysics();

    }



    private void PerformJump()

    {

        motor.ForceJump(jumpForce);

        jumpsRemaining--;

        SwitchState(PlayerState.Jumping);

    }



    private bool IsOnSteepSlope()

    {

        return Vector3.Angle(motor.GroundNormal, Vector3.up) > minSlideStartAngle;

    }



    private void SwitchState(PlayerState newState) => currentState = newState;



    private Vector3 GetCameraRelativeDir(Vector2 input)

    {

        if (Camera.main == null) return new Vector3(input.x, 0, input.y);

        Vector3 camFwd = Camera.main.transform.forward;

        Vector3 camRight = Camera.main.transform.right;

        camFwd.y = 0; camRight.y = 0;

        return (camFwd.normalized * input.y + camRight.normalized * input.x).normalized;

    }

}

