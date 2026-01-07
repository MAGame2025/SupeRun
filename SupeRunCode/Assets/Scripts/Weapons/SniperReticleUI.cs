using UnityEngine;
using UnityEngine.UI;

public class SniperReticleUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SRWeaponManager weaponManager;

    [Tooltip("The UI Image that represents the reticle (do NOT disable the GameObject running this script).")]
    [SerializeField] private Image reticleImage;

    [Header("Bloom / Recoil")]
    [SerializeField] private float kickScale = 1.25f;
    [SerializeField] private float kickAddPerShot = 0.08f;
    [SerializeField] private float maxKickScale = 1.6f;
    [SerializeField] private float returnSpeed = 14f;

    private SRWeaponBase lastWeapon;
    private float currentScale = 1f;
    private float targetScale = 1f;

    private void Awake()
    {
        if (weaponManager == null)
            weaponManager = FindAnyObjectByType<SRWeaponManager>();

        if (reticleImage == null)
            reticleImage = GetComponentInChildren<Image>(true);

        if (reticleImage != null)
            reticleImage.rectTransform.localScale = Vector3.one;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (weaponManager == null || reticleImage == null)
            return;

        SRWeaponBase current = weaponManager.CurrentWeapon;

        // Subscribe/unsubscribe when weapon changes
        if (current != lastWeapon)
        {
            Unsubscribe();
            lastWeapon = current;
            Subscribe();
        }

        bool isSniper = current != null && current.GetType().Name.Contains("Sniper");

        if (reticleImage.enabled != isSniper)
            reticleImage.enabled = isSniper;

        if (!isSniper)
            return;

        // Place reticle at the active weapon’s aim viewport point
        Vector2 vp = weaponManager.GetActiveAimViewportPoint();
        vp.x = Mathf.Clamp01(vp.x);
        vp.y = Mathf.Clamp(vp.y, 0.01f, 0.99f);

        RectTransform rt = reticleImage.rectTransform;
        rt.anchorMin = vp;
        rt.anchorMax = vp;
        rt.anchoredPosition = Vector2.zero;

        // Animate scale back to normal
        targetScale = Mathf.Lerp(targetScale, 1f, Time.deltaTime * returnSpeed);
        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * returnSpeed);

        rt.localScale = new Vector3(currentScale, currentScale, 1f);
    }

    private void Subscribe()
    {
        if (lastWeapon == null) return;
        lastWeapon.OnFired += HandleFired;
    }

    private void Unsubscribe()
    {
        if (lastWeapon == null) return;
        lastWeapon.OnFired -= HandleFired;
    }

    private void HandleFired()
    {
        // Kick outward immediately, stack slightly on rapid shots
        float desired = Mathf.Max(targetScale, kickScale);
        desired += kickAddPerShot;
        targetScale = Mathf.Min(desired, maxKickScale);

        // Also push currentScale up instantly so it feels snappy
        currentScale = Mathf.Max(currentScale, targetScale);
    }
}
