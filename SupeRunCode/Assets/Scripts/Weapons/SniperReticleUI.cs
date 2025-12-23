using UnityEngine;
using UnityEngine.UI;

public class SniperReticleUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SRWeaponManager weaponManager;

    [Tooltip("The UI Image that represents the reticle (do NOT disable the GameObject running this script).")]
    [SerializeField] private Image reticleImage;

    private void Awake()
    {
        if (weaponManager == null)
            weaponManager = FindAnyObjectByType<SRWeaponManager>();

        if (reticleImage == null)
            reticleImage = GetComponentInChildren<Image>(true);
    }

    private void Update()
    {
        if (weaponManager == null || reticleImage == null)
            return;

        var current = weaponManager.CurrentWeapon;
        bool isSniper = false;

        // Most reliable check: compare the *actual type name* of the current weapon script.
        if (current != null)
        {
            string t = current.GetType().Name;
            isSniper = (t == "SRSniperRifle" || t == "SniperRifle" || t.Contains("Sniper"));
        }

        // Toggle the IMAGE, not the GameObject, so this script keeps running.
        if (reticleImage.enabled != isSniper)
            reticleImage.enabled = isSniper;
    }
}
