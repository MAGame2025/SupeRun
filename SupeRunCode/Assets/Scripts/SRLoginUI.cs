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
        // If already signed in when this UI appears, skip login instantly
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

        if (statusText != null) statusText.text = "";

        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Cloud load hook (we'll add it next step)
        if (SRProgressManager.Instance != null)
            SRProgressManager.Instance.LoadFromCloud();
    }

    public async void OnLoginClicked()
    {
        string user = usernameInput.text;
        string pass = passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            statusText.text = "Enter Usernam and Password";
            return;
        }

        statusText.text = "Loggin in...";
        var result = await SRAuthenticationManager.Instance.Login(user, pass);
        statusText.text = result.message;

        if (result.success)
            HandleSignedIn(); // also covers "Already signed in"
    }

    public async void OnRegisterClicked()
    {
        string user = usernameInput.text;
        string pass = passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            statusText.text = "Enter username and password";
            return;
        }

        statusText.text = "Registered...";
        var result = await SRAuthenticationManager.Instance.Register(user, pass);
        statusText.text = result.message;

        if (result.success)
            HandleSignedIn(); 
    }
}
