using UnityEngine;

public class WorldBillboard : MonoBehaviour
{
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        // Look at camera but keep upright
        Vector3 forward = transform.position - cam.transform.position;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward);
    }
}
