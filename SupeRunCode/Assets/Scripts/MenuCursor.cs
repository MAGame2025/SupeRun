using UnityEngine;

public class MenuCursor : MonoBehaviour
{
    private void OnEnable()
    {
        UnlockCursor();
    }

    private void Start()
    {
        UnlockCursor();
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
