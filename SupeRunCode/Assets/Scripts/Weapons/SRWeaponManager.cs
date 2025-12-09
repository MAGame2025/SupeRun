using System.Collections.Generic;
using UnityEngine;

// Manages all weapons the player can equip, fire, unlock and level up.
public class SRWeaponManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int maxWeaponSlots = 4;

    [Header("Weapon Pool (all possible weapon prefabs)")]
    [Tooltip("All weapon prefabs that CAN exist in the run (e.g., 10 total types).")]
    [SerializeField] private SRWeaponBase[] weaponPool;

    [Header("Starting Weapons (prefabs)")]
    [Tooltip("Weapons the player starts with. These will be instantiated at runtime.")]
    [SerializeField] private SRWeaponBase[] startingWeapons;

    [Header("Visual Attach Point")]
    [SerializeField] private Transform weaponAnchor;

    [Header("Runtime References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform fireOrigin;   // muzzle or camera pivot
    [SerializeField] private InputReader inputReader;

    // Currently owned weapon instances (up to maxWeaponSlots).
    private readonly List<SRWeaponBase> equippedWeapons = new List<SRWeaponBase>();

    // Currently equipped/active weapon.
    private SRWeaponBase currentWeapon;
    private int currentIndex;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (inputReader == null)
            inputReader = GetComponent<InputReader>();   // fallback if on same GameObject

        // Instantiate starting weapons as children of the player.
        if (startingWeapons != null)
        {
            foreach (var w in startingWeapons)
            {
                if (w == null) continue;
                if (equippedWeapons.Count >= maxWeaponSlots) break;

                Transform parent = weaponAnchor != null ? weaponAnchor : transform;

                SRWeaponBase instance = Instantiate(w, parent);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                instance.gameObject.SetActive(false);
                equippedWeapons.Add(instance);
            }
        }


        // Equip the first weapon, if any.
        if (equippedWeapons.Count > 0)
        {
            Equip(0);
        }
    }

    private void Update()
    {
        if (currentWeapon == null || inputReader == null)
            return;

        // ---- SHOOT INPUT ----
        // Use ShootHeld for auto-fire with internal cooldown.
        bool fireInput = inputReader.ShootHeld;

        if (fireInput)
        {
            Vector3 origin = fireOrigin != null
                ? fireOrigin.position
                : (playerCamera != null ? playerCamera.transform.position : transform.position);

            Vector3 direction = playerCamera != null
                ? playerCamera.transform.forward
                : transform.forward;

            currentWeapon.TryFire(origin, direction);
        }

        // NOTE:
        // Weapon switching input is NOT handled here to avoid mixing systems.
        // You can call Equip(index) or CycleWeapon(+1 / -1) from another script.
    }

    // Equip weapon in runtime slot index. Only that weapon's GameObject is active.
    public void Equip(int index)
    {
        if (index < 0 || index >= equippedWeapons.Count)
            return;

        if (currentWeapon != null)
            currentWeapon.OnUnequip();

        currentIndex = index;
        currentWeapon = equippedWeapons[currentIndex];

        for (int i = 0; i < equippedWeapons.Count; i++)
        {
            bool active = (i == currentIndex);
            if (equippedWeapons[i] != null)
                equippedWeapons[i].gameObject.SetActive(active);
        }

        currentWeapon.OnEquip();
    }

    // Convenience method if you want to cycle weapons from another script.
    public void CycleWeapon(int direction)
    {
        if (equippedWeapons.Count == 0)
            return;

        int newIndex = (currentIndex + direction + equippedWeapons.Count) % equippedWeapons.Count;
        Equip(newIndex);
    }

    // --------------------------------------------------------------------
    //  LEVELING API
    // --------------------------------------------------------------------


    // Level up a specific weapon instance (not limited to currently equipped).
    public WeaponLevelUpResult LevelUpWeapon(SRWeaponBase weapon, WeaponUpgradeContext context)
    {
        if (weapon == null)
            return WeaponLevelUpResult.Empty;

        return weapon.LevelUp(context);
    }


    // Level up the weapon in the given slot index (0..equippedWeapons.Count-1).
    public WeaponLevelUpResult LevelUpWeaponInSlot(int slotIndex, WeaponUpgradeContext context)
    {
        if (slotIndex < 0 || slotIndex >= equippedWeapons.Count)
            return WeaponLevelUpResult.Empty;

        return equippedWeapons[slotIndex].LevelUp(context);
    }


    // Level up the currently equipped weapon.
    public WeaponLevelUpResult LevelUpCurrentWeapon(WeaponUpgradeContext context)
    {
        return LevelUpWeapon(currentWeapon, context);
    }

    // --------------------------------------------------------------------
    //  UNLOCK / EQUIP NEW WEAPONS
    // --------------------------------------------------------------------

    public bool HasFreeWeaponSlot => equippedWeapons.Count < maxWeaponSlots;

 
    // Returns prefabs from weaponPool that are NOT yet owned (by type).
    // Used to randomly offer new weapons on level up.
    public List<SRWeaponBase> GetLockedWeaponPrefabs()
    {
        var locked = new List<SRWeaponBase>();

        if (weaponPool == null)
            return locked;

        foreach (var prefab in weaponPool)
        {
            if (prefab == null) continue;

            bool alreadyHaveType = equippedWeapons.Exists(
                w => w != null && w.GetType() == prefab.GetType());

            if (!alreadyHaveType)
                locked.Add(prefab);
        }

        return locked;
    }


    // Instantiates a new weapon from the pool and adds it to the equipped list.
    // Optionally auto-equips it.
    public SRWeaponBase UnlockNewWeapon(SRWeaponBase prefabToUnlock, bool autoEquip = true)
    {
        if (!HasFreeWeaponSlot || prefabToUnlock == null)
            return null;

        bool alreadyHaveType = equippedWeapons.Exists(
            w => w != null && w.GetType() == prefabToUnlock.GetType());
        if (alreadyHaveType)
            return null;

        Transform parent = weaponAnchor != null ? weaponAnchor : transform;

        SRWeaponBase instance = Instantiate(prefabToUnlock, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        instance.gameObject.SetActive(false);
        equippedWeapons.Add(instance);

        if (autoEquip)
        {
            Equip(equippedWeapons.Count - 1);
        }

        return instance;
    }



    // Returns currently equipped weapons that are not yet at max level.
    public List<SRWeaponBase> GetUpgradeableWeapons()
    {
        var list = new List<SRWeaponBase>();

        foreach (var w in equippedWeapons)
        {
            if (w == null) continue;

            // NOTE:
            // SRWeaponBase should expose a public MaxLevel property:
            // public int MaxLevel => maxLevel;
            if (w.CurrentLevel < w.MaxLevel)
                list.Add(w);
        }

        return list;
    }

    // --------------------------------------------------------------------
    //  PUBLIC QUERIES
    // --------------------------------------------------------------------

    public IReadOnlyList<SRWeaponBase> EquippedWeapons => equippedWeapons;
    public SRWeaponBase CurrentWeapon => currentWeapon;
    public int CurrentWeaponIndex => currentIndex;
    public int MaxWeaponSlots => maxWeaponSlots;
}
