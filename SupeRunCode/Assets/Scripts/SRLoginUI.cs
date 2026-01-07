using TMPro;
using UnityEngine;

public class SRLoginUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject mainMenuPanel;

    private bool signedInAsGuest;

    private void OnEnable()
    {
        if (SRAuthenticationManager.Instance != null)
            SRAuthenticationManager.Instance.OnSignedIn += HandleSignedIn;
    }

    private void OnDisable()
    {
        if (SRAuthenticationManager.Instance != null)
            SRAuthenticationManager.Instance.OnSignedIn -= HandleSignedIn;
    }

    private void Start()
    {
        if (SRAuthenticationManager.Instance != null && SRAuthenticationManager.Instance.IsSignedIn)
            HandleSignedIn();
        else
            ShowLogin();
    }

    private void ShowLogin()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    private void HandleSignedIn()
    {
        Debug.Log("HandleSignedIn -> switching UI to Main Menu");

        if (statusText != null)
            statusText.text = "";

        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (!signedInAsGuest && SRProgressManager.Instance != null)
            SRProgressManager.Instance.LoadFromCloud();
    }


    public async void OnLoginClicked()
    {
        signedInAsGuest = false;

        string user = usernameInput.text;
        string pass = passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            statusText.text = "Enter Username and Password";
            return;
        }

        statusText.text = "Logging in...";
        var result = await SRAuthenticationManager.Instance.Login(user, pass);
        statusText.text = result.message;

        if (result.success)
            HandleSignedIn();
    }

    public async void OnRegisterClicked()
    {
        signedInAsGuest = false;

        string user = usernameInput.text;
        string pass = passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            statusText.text = "Enter username and password";
            return;
        }

        statusText.text = "Registering...";
        var result = await SRAuthenticationManager.Instance.Register(user, pass);
        statusText.text = result.message;

        if (result.success)
            HandleSignedIn();
    }

    public async void OnGuestClicked()
    {
        signedInAsGuest = true;

        if (statusText != null) statusText.text = "Signing in as guest...";

        // Option A (recommended): anonymous sign-in (Unity Authentication style)
        var result = await SRAuthenticationManager.Instance.LoginAsGuest();
        if (statusText != null) statusText.text = result.message;

        if (result.success)
            HandleSignedIn();
    }
}
