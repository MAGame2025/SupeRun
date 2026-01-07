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

    [SerializeField] private Vector2 aimViewportPoint = new Vector2(0.5f, 0.62f);
    [SerializeField] private float aimMaxDistance = 300f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Visual Attach Point")]
    [SerializeField] private Transform weaponAnchor;

    [Header("Runtime References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform fireOrigin;   // muzzle or camera pivot
    [SerializeField] private InputReader inputReader;

    // Currently owned weapon instances (up to maxWeaponSlots).
    private readonly List<SRWeaponBase> equippedWeapons = new List<SRWeaponBase>();
    public static SRWeaponManager Instance { get; private set; }

    // Currently equipped/active weapon.
    private SRWeaponBase currentWeapon;
    private int currentIndex;
    public Camera PlayerCamera => playerCamera;
    public Vector2 AimViewportPoint => aimViewportPoint;
    public float AimMaxDistance => aimMaxDistance;
    public LayerMask AimMask => aimMask;

    private void Awake()
    {
        Instance = this;
    }

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
        float scroll = inputReader.SwitchWeaponValue;

        if (scroll > 0.1f)
        {
            CycleWeapon(+1);
            inputReader.ConsumeSwitchWeapon();
        }
        else if (scroll < -0.1f)
        {
            CycleWeapon(-1);
            inputReader.ConsumeSwitchWeapon();
        }


        if (fireInput)
        {
            Vector3 origin = GetFireOrigin();
            Vector3 aimPoint;
            Vector3 direction = GetAimDirection(origin, out aimPoint);
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

    public Vector3 GetAimDirection(Vector3 fireOrigin, out Vector3 aimPoint)
    {
        aimPoint = fireOrigin + playerCamera.transform.forward;

        if (playerCamera == null)
            return transform.forward;

        Vector2 vp = GetCurrentAimViewportPoint();
        float maxDist = GetAimMaxDistance();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, aimMask, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;
        else
            aimPoint = ray.origin + ray.direction * maxDist;

        Vector3 dir = (aimPoint - fireOrigin);
        if (dir.sqrMagnitude < 0.0001f)
            return ray.direction;

        return dir.normalized;
    }


    // If current weapon is sniper, use its range as the aim distance.
    private float GetAimMaxDistance()
    {
        if (currentWeapon != null)
            return currentWeapon.AimMaxDistance;

        return aimMaxDistance;
    }

    public bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;

        if (playerCamera == null)
            return false;

        Vector2 vp = GetCurrentAimViewportPoint();
        float maxDist = GetAimMaxDistance();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, aimMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            return true;
        }

        aimPoint = ray.origin + ray.direction * maxDist;
        return true;
    }

    public Vector3 GetFireOrigin()
    {
        if (currentWeapon != null)
        {
            Transform weaponMuzzle = currentWeapon.MuzzleTransform;
            if (weaponMuzzle != null)
                return weaponMuzzle.position;
        }

        if (fireOrigin != null) return fireOrigin.position;
        if (playerCamera != null) return playerCamera.transform.position;
        return transform.position;
    }



    public Vector3 GetLiveAimDirectionFromFireOrigin()
    {
        Vector3 origin = GetFireOrigin();

        Vector3 aimPoint;
        if (!TryGetAimPoint(out aimPoint))
            return (playerCamera != null ? playerCamera.transform.forward : transform.forward);

        Vector3 dir = (aimPoint - origin);
        if (dir.sqrMagnitude < 0.0001f)
            return (playerCamera != null ? playerCamera.transform.forward : transform.forward);

        return dir.normalized;
    }

    private Vector2 GetCurrentAimViewportPoint()
    {
        if (currentWeapon != null && currentWeapon.UseCustomAimViewportPoint)
            return currentWeapon.AimViewportPoint;

        return aimViewportPoint;
    }



    public Vector2 GetActiveAimViewportPoint()
    {
        return GetCurrentAimViewportPoint();
    }

    // --------------------------------------------------------------------
    //  PUBLIC QUERIES
    // --------------------------------------------------------------------

    public IReadOnlyList<SRWeaponBase> EquippedWeapons => equippedWeapons;
    public SRWeaponBase CurrentWeapon => currentWeapon;
    public int CurrentWeaponIndex => currentIndex;
    public int MaxWeaponSlots => maxWeaponSlots;
}
