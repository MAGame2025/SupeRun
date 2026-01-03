using UnityEngine;

[RequireComponent(typeof(AdvancedPlayerMotor))]
public class AdvancedCharacterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera playerCamera;

    [Header("Speed")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float runMultiplier = 1.5f;

    [Header("Optional Rotation")]
    [SerializeField] private bool rotateToMoveDir = true;
    [SerializeField] private float rotateSpeed = 12f;

    private AdvancedPlayerMotor motor;

    private void Awake()
    {
        motor = GetComponent<AdvancedPlayerMotor>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (inputReader == null)
            inputReader = GetComponent<InputReader>();
    }

    private void Update()
    {
        if (inputReader == null)
            return;

        // Read input from InputReader (New Input System wrapper)
        Vector2 move = inputReader.Move;                 // X = strafe, Y = forward :contentReference[oaicite:1]{index=1}
        bool jumpPressed = inputReader.JumpPressed;      // one-frame flag :contentReference[oaicite:2]{index=2}
        bool runHeld = inputReader.RunHeld;              // held flag :contentReference[oaicite:3]{index=3}
        bool slideHeld = inputReader.CrouchHeld;         // held flag :contentReference[oaicite:4]{index=4}

        // Normalize move so diagonal isn't faster
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        // Camera-relative move direction (XZ only)
        Vector3 moveDir = Vector3.zero;

        if (playerCamera != null)
        {
            Vector3 camF = playerCamera.transform.forward;
            Vector3 camR = playerCamera.transform.right;

            camF.y = 0f;
            camR.y = 0f;

            camF.Normalize();
            camR.Normalize();

            // move.y = forward, move.x = right
            moveDir = camF * move.y + camR * move.x;

            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();
        }
        else
        {
            // Fallback: local forward/right
            moveDir = (transform.forward * move.y + transform.right * move.x);

            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();
        }

        float speedCap = maxSpeed * (runHeld ? runMultiplier : 1f);

        // Delegate to motor
        motor.ProcessMove(moveDir, speedCap, jumpPressed, slideHeld);

        // Optional: rotate character to face move direction
        if (rotateToMoveDir && moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }
    }
}
