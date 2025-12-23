using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Single authoritative cursor controller.
/// - Persist across scenes (singleton)
/// - Auto-unlock in UI scenes (MainMenu, LevelSelect, etc.)
/// - Gameplay: lock by default, allow ESC toggle
/// - SetUIMode(true) forces unlock for in-game UI (LevelUp, Pause, etc.)
public class CursorLockController : MonoBehaviour
{
    [Header("Gameplay Toggle Key")]
    [SerializeField] private Key toggleKey = Key.Escape;

    [Header("Menu / UI Scenes (auto-unlock here)")]
    [Tooltip("Exact scene names (case-insensitive) where the cursor must be UNLOCKED.")]
    [SerializeField] private string[] uiScenes = { "MainMenu", "LevelSelect" };

    [Header("Debug")]
    [SerializeField] private bool logSceneChanges = true;

    private static CursorLockController instance;

    // True if the current loaded scene is a UI/menu scene.
    private bool isUISceneNow;

    // True if gameplay UI is currently open (LevelUp, Pause, etc.)
    private bool uiOverlayOpen;

    private void Awake()
    {
        // Singleton: only one controller may exist.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void Start()
    {
        ApplySceneState(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        // In menu scenes: never toggle, always unlocked.
        if (isUISceneNow)
            return;

        // If an in-game UI overlay is open: do not toggle, keep unlocked.
        if (uiOverlayOpen)
            return;

        // Gameplay: allow Esc to toggle lock/unlock.
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked) Unlock();
            else Lock();
        }
    }

    private void LateUpdate()
    {
        // Hard enforce correct state so other scripts can't fight it.

        if (isUISceneNow)
        {
            // Menu scenes must always be usable.
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                Unlock();
            return;
        }

        if (uiOverlayOpen)
        {
            // In-game UI (level up / pause) must be usable.
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                Unlock();
            return;
        }

        // Gameplay default: locked (unless user manually unlocked with ESC).
        // If you want to FORCE lock always during gameplay, uncomment:
        // if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
        //     Lock();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Re-apply proper state (helps after alt-tab).
        ApplySceneState(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneState(scene.name);
    }

    private void ApplySceneState(string sceneName)
    {
        isUISceneNow = IsUIScene(sceneName);

        if (logSceneChanges)
            Debug.Log($"[CursorLockController] Scene='{sceneName}' -> isUISceneNow={isUISceneNow}, uiOverlayOpen={uiOverlayOpen}");

        // Menu scenes always win (unlock no matter what).
        if (isUISceneNow)
        {
            Unlock();
            return;
        }

        // In gameplay: if overlay open, unlock, else lock.
        if (uiOverlayOpen) Unlock();
        else Lock();
    }

    private bool IsUIScene(string sceneName)
    {
        if (uiScenes == null) return false;
        if (string.IsNullOrEmpty(sceneName)) return false;

        for (int i = 0; i < uiScenes.Length; i++)
        {
            string s = uiScenes[i];
            if (string.IsNullOrEmpty(s)) continue;

            if (string.Equals(sceneName, s.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// Called by in-game UI (LevelUpUI, PauseMenu, etc.)
    /// enabled=true -> unlock cursor, stop toggling
    /// enabled=false -> return to scene-default behavior (usually lock)
    public void SetUIMode(bool enabled)
    {
        uiOverlayOpen = enabled;

        // Apply state immediately based on current scene + overlay.
        ApplySceneState(SceneManager.GetActiveScene().name);
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
