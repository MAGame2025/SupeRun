using UnityEngine;
using UnityEngine.InputSystem;

public class CursorLockController : MonoBehaviour
{
    [SerializeField] private bool lockOnStart = true;
    [SerializeField] private Key toggleKey = Key.Escape;

    private bool uiMode = false;

    private void Start()
    {
        if (lockOnStart)
            Lock();
    }

    private void Update()
    {
        // IMPORTANT: if UI is open, do not toggle cursor at all.
        if (uiMode)
            return;

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked) Unlock();
            else Lock();
        }
    }

    public void SetUIMode(bool enabled)
    {
        uiMode = enabled;

        if (uiMode) Unlock();
        else Lock();
    }

    public void Lock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
