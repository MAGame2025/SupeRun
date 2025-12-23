using UnityEngine;
using UnityEngine.InputSystem;


/// Central place that reads input from the new Input System.

public class InputReader : MonoBehaviour
{
    private PlayerControls controls;
    private bool isInitialized = false;

    // Exposed properties
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }

    /// True only on the frame the button is first pressed.
    public bool JumpPressed { get; private set; }

    /// True while the button is held down.
    public bool JumpHeld { get; private set; }

    public bool RunHeld { get; private set; }

    public bool CrouchPressed { get; private set; }

    public bool CrouchHeld { get; private set; }

    public bool ShootPressed { get; private set; }
    public bool ShootHeld { get; private set; }

    public bool InteractPressed { get; private set; }
    public bool InteractHeld { get; private set; }


    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized)
            return;

        controls = new PlayerControls();

        // --- Move ---
        controls.Gameplay.Move.performed += ctx =>
        {
            Move = ctx.ReadValue<Vector2>();
        };
        controls.Gameplay.Move.canceled += ctx =>
        {
            Move = Vector2.zero;
        };

        // --- Look ---
        controls.Gameplay.Look.performed += ctx =>
        {
            Look = ctx.ReadValue<Vector2>();
        };
        controls.Gameplay.Look.canceled += ctx =>
        {
            Look = Vector2.zero;
        };

        // --- Jump ---
        controls.Gameplay.Jump.started += ctx =>
        {
            JumpPressed = true;  // one-frame flag
            JumpHeld = true;
        };
        controls.Gameplay.Jump.canceled += ctx =>
        {
            JumpHeld = false;
        };

        // --- Run ---
        controls.Gameplay.Run.started += ctx =>
        {
            RunHeld = true;
        };
        controls.Gameplay.Run.canceled += ctx =>
        {
            RunHeld = false;
        };

        // --- Crouch (for sliding etc.) ---
        controls.Gameplay.Crouch.started += ctx =>
        {
            CrouchPressed = true;
            CrouchHeld = true;
        };
        controls.Gameplay.Crouch.canceled += ctx =>
        {
            CrouchHeld = false;
        };

        // --- Shoot ---
        controls.Gameplay.Shoot.started += ctx =>
        {
            ShootPressed = true;   // one-frame flag
            ShootHeld = true;
        };
        controls.Gameplay.Shoot.canceled += ctx =>
        {
            ShootHeld = false;
        };

        // --- Interact ---
        controls.Gameplay.Interact.started += ctx =>
        {
            InteractPressed = true;   // one-frame flag
            InteractHeld = true;
        };
        controls.Gameplay.Interact.canceled += ctx =>
        {
            InteractHeld = false;
        };


        isInitialized = true;
    }

    private void OnEnable()
    {
        InitializeIfNeeded();     // make sure controls exists
        controls.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)     // guard against domain reload etc
            controls.Disable();
    }

    private void LateUpdate()
    {
        // Reset one-frame flags at the end of the frame
        JumpPressed = false;
        CrouchPressed = false;
        ShootPressed = false;
        InteractPressed = false;

    }

}

