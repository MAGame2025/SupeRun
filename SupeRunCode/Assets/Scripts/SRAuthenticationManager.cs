using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class SRAuthenticationManager : MonoBehaviour
{
    public static SRAuthenticationManager Instance { get; private set; }

    public event Action OnSignedIn;

    private bool servicesInitialized;

    private async void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await UnityServices.InitializeAsync();
        servicesInitialized = true;

        Debug.Log("Unity Services initialized");

        // If Unity already has a valid session, IsSignedIn might be true here.
        // In that case, just continue to the game.
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Already signed in. PlayerId: {AuthenticationService.Instance.PlayerId}");
            OnSignedIn?.Invoke();
        }
    }

    public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

    public async Task<(bool success, string message)> Register(string username, string password)
    {
        if (!servicesInitialized)
            return (false, "Services not initialized yet");

        if (AuthenticationService.Instance.IsSignedIn)
            return (true, "Already signed in");

        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            Debug.Log($"Register success. PlayerId: {AuthenticationService.Instance.PlayerId}");
            OnSignedIn?.Invoke();
            return (true, "Register successful");
        }
        catch (AuthenticationException e)
        {
            Debug.LogError(e);
            return (false, e.Message);
        }
        catch (RequestFailedException e)
        {
            Debug.LogError(e);
            return (false, e.Message);
        }
    }

    public async Task<(bool success, string message)> Login(string username, string password)
    {
        if (!servicesInitialized)
            return (false, "Services not initialized yet");

        if (AuthenticationService.Instance.IsSignedIn)
            return (true, "Already signed in");

        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            Debug.Log($"Login success. PlayerId: {AuthenticationService.Instance.PlayerId}");
            OnSignedIn?.Invoke();
            return (true, "Login successful");
        }
        catch (AuthenticationException e)
        {
            Debug.LogError(e);
            return (false, e.Message);
        }
        catch (RequestFailedException e)
        {
            Debug.LogError(e);
            return (false, e.Message);
        }
    }

    public void SignOut()
    {
        if (!AuthenticationService.Instance.IsSignedIn) return;
        AuthenticationService.Instance.SignOut();
        Debug.Log("Signed out");
    }
}
