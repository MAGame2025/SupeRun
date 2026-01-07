using UnityEngine;

public class RBPlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputReader input;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private RBPlayerMovement movement;

    private void Reset()
    {
        movement = GetComponent<RBPlayerMovement>();
    }

    private void Update()
    {
        if (input == null || cameraTransform == null || movement == null)
            return;

        // Camera-relative move (XZ only)
        Vector3 desiredDir = GetCameraRelativeMove(input.Move, cameraTransform);

        // Convert world dir -> local X/Z input in the motor's space
        // (Motor expects x=strafe, y=forward in its own transform basis)
        Vector3 local = movement.transform.InverseTransformDirection(desiredDir);
        movement.moveInput = new Vector2(local.x, local.z);

        if (input.JumpPressed)
            movement.jumpPressed = true;

        movement.crouchHeld = input.CrouchHeld;
    }

    private static Vector3 GetCameraRelativeMove(Vector2 move, Transform cam)
    {
        if (move.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 fwd = cam.forward;
        Vector3 right = cam.right;

        fwd.y = 0f;
        right.y = 0f;

        fwd.Normalize();
        right.Normalize();

        Vector3 desired = (fwd * move.y) + (right * move.x);
        if (desired.sqrMagnitude > 1f)
            desired.Normalize();

        return desired;
    }
}
