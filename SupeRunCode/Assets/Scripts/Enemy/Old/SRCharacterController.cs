//using UnityEngine;

//[RequireComponent(typeof(CharacterController))]
//public class SRCharacterController : MonoBehaviour
//{
//    [Header("Movement Speeds")]
//    [SerializeField] private float walkSpeed = 4f;
//    [SerializeField] private float runSpeed = 9f;
//    [SerializeField] private float crouchSpeed = 2f;
    
//    [Header("Jump System")]
//    [SerializeField] private float jumpForce = 7f;
//    [SerializeField] private int maxJumps = 2; // Example: 2 for Double Jump
//    [SerializeField] private float gravity = -25f;

//    [Header("Acceleration")]
//    [Tooltip("How fast you speed up on the ground (units per second^2).")]
//    [SerializeField] private float groundAcceleration = 40f;
//    [Tooltip("How fast you slow down on the ground when changing direction / stopping.")]
//    [SerializeField] private float groundDeceleration = 35f;

//    [Header("Air Control / Bunny Hop")]
//    [Tooltip("How quickly you can steer/change velocity in the air.")]
//    [SerializeField] private float airAcceleration = 10f;
//    [Tooltip("How fast the character model rotates to face movement in the air.")]
//    [SerializeField] private float airRotationSpeed = 15f; // New Field
//    [Tooltip("Maximum horizontal speed you can reach from bunny hopping.")]
//    [SerializeField] private float maxBunnyHopSpeed = 14f;

//    [Header("Sliding (Apex Style)")]
//    [SerializeField] private float baseSlideSpeed = 10f;
//    [SerializeField] private float maxFlatSlideSpeed = 14f;
//    [SerializeField] private float terminalSlideSpeed = 35f;
//    [SerializeField] private float slideFriction = 6f;
//    [SerializeField] private float slideSlopeAccel = 20f;
//    [SerializeField] private float minSlideSlopeAngle = 5f;
//    [SerializeField] private float slideDuration = 3f;

//    [Header("Ground Detection")]
//    [SerializeField] private LayerMask groundMask = ~0;
//    [SerializeField] private float groundCheckDist = 2f;

//    [Header("References")]
//    [SerializeField] private InputReader inputReader;

//    private CharacterController controller;
//    private bool isGrounded;
    
//    // Horizontal (XZ) movement
//    private Vector3 horizontalVelocity = Vector3.zero;
//    // Vertical (Y) movement
//    private float verticalVelocity = 0f;
    
//    // Jump State
//    private int jumpsRemaining;

//    // Sliding state helpers
//    private float slideTimer = 0f;
//    private float currentSlideSpeed = 0f;
//    private Vector3 slideDirection = Vector3.zero;

//    public PlayerStates.PlayerState currentState = PlayerStates.PlayerState.Idle;

//    private void Awake()
//    {
//        controller = GetComponent<CharacterController>();

//        if (inputReader == null)
//        {
//            inputReader = GetComponent<InputReader>();
//            if (inputReader == null)
//                inputReader = FindAnyObjectByType<InputReader>();
//        }
//    }

//    private void Update()
//    {
//        if (inputReader == null) return;
//        HandleStateMachine();
//    }

//    // =====================================================================
//    //  STATE MACHINE
//    // =====================================================================

//    private void HandleStateMachine()
//    {
//        isGrounded = controller.isGrounded;

//        // Ground Snap & Jump Reset
//        // We only reset jumps if grounded AND not currently moving up (jumping)
//        if (isGrounded && verticalVelocity <= 0f)
//        {
//            verticalVelocity = -5f; // Stick to ground
//            jumpsRemaining = maxJumps; // Regain jumps
//        }

//        Vector2 moveInput = inputReader.Move;
//        bool hasMovement = moveInput.sqrMagnitude > 0.01f;
//        bool wantsToRun = inputReader.RunHeld;

//        switch (currentState)
//        {
//            case PlayerStates.PlayerState.Idle:
//                if (!isGrounded) SwitchState(PlayerStates.PlayerState.Falling);
//                else if (inputReader.CrouchHeld) SwitchState(PlayerStates.PlayerState.Crouching);
//                else if (hasMovement && wantsToRun) SwitchState(PlayerStates.PlayerState.Running);
//                else if (hasMovement) SwitchState(PlayerStates.PlayerState.Walking);
//                break;

//            case PlayerStates.PlayerState.Walking:
//                if (!isGrounded) SwitchState(PlayerStates.PlayerState.Falling);
//                else if (inputReader.CrouchHeld) SwitchState(PlayerStates.PlayerState.Crouching);
//                else if (!hasMovement) SwitchState(PlayerStates.PlayerState.Idle);
//                else if (wantsToRun) SwitchState(PlayerStates.PlayerState.Running);
//                break;

//            case PlayerStates.PlayerState.Running:
//                if (!isGrounded) SwitchState(PlayerStates.PlayerState.Falling);
//                else
//                {
//                    if (!hasMovement) SwitchState(PlayerStates.PlayerState.Idle);
//                    if (inputReader.CrouchPressed) TryStartSlide();
//                }
//                break;

//            case PlayerStates.PlayerState.Jumping:
//                if (verticalVelocity < 0f) SwitchState(PlayerStates.PlayerState.Falling);
//                break;

//            case PlayerStates.PlayerState.Falling:
//                if (isGrounded && verticalVelocity <= 0f)
//                {
//                    if (hasMovement && wantsToRun) SwitchState(PlayerStates.PlayerState.Running);
//                    else if (hasMovement) SwitchState(PlayerStates.PlayerState.Walking);
//                    else SwitchState(PlayerStates.PlayerState.Idle);
//                }
//                break;

//            case PlayerStates.PlayerState.Crouching:
//                if (!isGrounded) SwitchState(PlayerStates.PlayerState.Falling);
//                else if (!inputReader.CrouchHeld)
//                {
//                    if (hasMovement && wantsToRun) SwitchState(PlayerStates.PlayerState.Running);
//                    else if (hasMovement) SwitchState(PlayerStates.PlayerState.Walking);
//                    else SwitchState(PlayerStates.PlayerState.Idle);
//                }
//                break;

//            case PlayerStates.PlayerState.Sliding:
//                break;
//        }

//        ExecuteMovement(moveInput);
//    }

//    private void SwitchState(PlayerStates.PlayerState newState)
//    {
//        currentState = newState;
//    }

//    // =====================================================================
//    //  SLIDE CONTROL
//    // =====================================================================

//    private void TryStartSlide()
//    {
//        float currentFlatSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
//        if (!isGrounded || currentFlatSpeed < walkSpeed * 0.5f) return;

//        currentState = PlayerStates.PlayerState.Sliding;
//        slideTimer = slideDuration;

//        Vector2 moveInput = inputReader.Move;
//        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

//        if (inputDir.sqrMagnitude > 0.01f)
//            slideDirection = horizontalVelocity.normalized;
//        else
//            slideDirection = transform.forward;

//        currentSlideSpeed = Mathf.Max(currentFlatSpeed, baseSlideSpeed);
//    }

//    private void EndSlide()
//    {
//        Vector2 moveInput = inputReader.Move;
//        bool hasMovement = moveInput.sqrMagnitude > 0.01f;
//        bool wantsToRun = inputReader.RunHeld;
//        bool wantsToCrouch = inputReader.CrouchHeld;

//        if (!isGrounded) SwitchState(PlayerStates.PlayerState.Falling);
//        else if (hasMovement && wantsToRun) SwitchState(PlayerStates.PlayerState.Running);
//        else if (hasMovement && wantsToCrouch) SwitchState(PlayerStates.PlayerState.Crouching);
//        else if (hasMovement) SwitchState(PlayerStates.PlayerState.Walking);
//        else SwitchState(PlayerStates.PlayerState.Idle);
//    }

//    // =====================================================================
//    //  MOVEMENT EXECUTION
//    // =====================================================================

//    private void ExecuteMovement(Vector2 moveInput)
//    {
//        if (currentState == PlayerStates.PlayerState.Sliding)
//        {
//            ExecuteSlide();
//            return;
//        }

//        // 1. Input Direction
//        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
//        bool hasMovement = inputDir.sqrMagnitude > 0.0001f;
//        bool wantsToRun = inputReader.RunHeld;

//        Vector3 worldMoveDir = Vector3.zero;
//        if (hasMovement)
//        {
//            Transform cam = Camera.main != null ? Camera.main.transform : null;
//            if (cam != null)
//            {
//                Vector3 camForward = cam.forward;
//                Vector3 camRight = cam.right;
//                camForward.y = 0f;
//                camRight.y = 0f;
//                camForward.Normalize();
//                camRight.Normalize();
//                worldMoveDir = camForward * inputDir.z + camRight * inputDir.x;
//                worldMoveDir.Normalize();
//            }
//            else
//            {
//                worldMoveDir = transform.TransformDirection(inputDir.normalized);
//            }
//        }

//        // 2. Target Speed
//        float targetSpeed = 0f;
//        if (hasMovement)
//        {
//            switch (currentState)
//            {
//                case PlayerStates.PlayerState.Crouching: targetSpeed = crouchSpeed; break;
//                case PlayerStates.PlayerState.Running: targetSpeed = runSpeed; break;
//                default: targetSpeed = wantsToRun ? runSpeed : walkSpeed; break;
//            }
//        }

//        Vector3 desiredHorizontal = hasMovement ? worldMoveDir * targetSpeed : Vector3.zero;

//        // 3. Horizontal Movement Calculation
//        if (isGrounded)
//        {
//            // Slope Projection
//            Vector3 groundNormal = GetGroundNormal();
//            if (groundNormal != Vector3.up)
//            {
//                Vector3 slopeMove = Vector3.ProjectOnPlane(desiredHorizontal, groundNormal).normalized;
//                desiredHorizontal = slopeMove * desiredHorizontal.magnitude;
//            }

//            float currentSpeed = horizontalVelocity.magnitude;
//            float desiredSpeed = desiredHorizontal.magnitude;
//            float accel = desiredSpeed > currentSpeed ? groundAcceleration : groundDeceleration;

//            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredHorizontal, accel * Time.deltaTime);
            
//            // Snap Rotation on Ground
//            if (horizontalVelocity.sqrMagnitude > 0.001f)
//            {
//                transform.rotation = Quaternion.LookRotation(horizontalVelocity.normalized);
//            }
//        }
//        else
//        {
//            // --- AIR CONTROL ---
//            // "Fun" Air Movement: We allow steering towards the input, but we don't apply friction.
            
//            Vector3 airTarget = horizontalVelocity;
//            if (hasMovement)
//            {
//                Vector3 desired = worldMoveDir * targetSpeed;
                
//                // Bunny Hop Logic:
//                // If the desired direction adds speed, or we are slower than desired, accelerate.
//                // If we are already faster than standard run speed, preserve that momentum (don't slow down).
//                if (desired.magnitude > horizontalVelocity.magnitude)
//                    airTarget = desired;
//                else
//                    airTarget = horizontalVelocity; 
//            }
            
//            // Move velocity towards target using airAcceleration
//            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, airTarget, airAcceleration * Time.deltaTime);

//            // Speed Cap for Bunny Hopping
//            float flatSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
//            if (flatSpeed > maxBunnyHopSpeed)
//            {
//                horizontalVelocity = horizontalVelocity.normalized * maxBunnyHopSpeed;
//            }

//            // Smooth Air Rotation (User Requested)
//            if (hasMovement)
//            {
//                Quaternion targetRot = Quaternion.LookRotation(worldMoveDir);
//                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, airRotationSpeed * Time.deltaTime);
//            }
//        }

//        // 4. Jump Logic (Multi-Jump)
//        if (inputReader.JumpPressed && jumpsRemaining > 0)
//        {
//            verticalVelocity = jumpForce;
//            jumpsRemaining--; // Consume a jump
//            SwitchState(PlayerStates.PlayerState.Jumping);
//        }

//        // 5. Gravity
//        verticalVelocity += gravity * Time.deltaTime;

//        // 6. Apply Move
//        controller.Move(horizontalVelocity * Time.deltaTime);
//        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
//    }

//    private void ExecuteSlide()
//    {
//        slideTimer
