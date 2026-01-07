using System;
using UnityEngine;


public abstract class SRWeaponBase : MonoBehaviour, ISRWeapon
{
    [Header("General Weapon Settings")]
    [SerializeField] protected string weaponName = "New Weapon";

    // We’ll treat fireRate as “shots per second”
    [SerializeField] protected float fireRate = 1f;

    [Header("Leveling")]
    [SerializeField] protected int maxLevel = 10;
    [SerializeField] protected int startLevel = 1;

    [Header("Critical Hits")]
    [Tooltip("0-1 chance to crit per hit/projectile.")]
    [Range(0f, 1f)]
    [SerializeField] protected float critChance = 0f;

    [Tooltip("Damage multiplier when a crit happens.")]
    [Min(1f)]
    [SerializeField] protected float critMultiplier = 2f;

    [SerializeField] protected float maxDistance = 300f;

    // Runtime (upgradeable) crit chance used during gameplay
    [NonSerialized]
    protected float currentCritChance;

    // Aiming (default behavior)
    public virtual bool UseCustomAimViewportPoint => false;
    public virtual Vector2 AimViewportPoint => Vector2.zero;

    public int CurrentLevel { get; protected set; }
    public int MaxLevel => maxLevel;

    protected float cooldownTimer;
    // WeaponManager expects a property-like value
    public virtual float AimMaxDistance => maxDistance;
    public bool CanFire => cooldownTimer <= 0f;

    public event Action OnFired;

protected void RaiseFired()
{
    OnFired?.Invoke();
}

    protected virtual void Awake()
    {
        CurrentLevel = Mathf.Clamp(startLevel, 1, maxLevel);
        currentCritChance = critChance;
    }

    public virtual Transform MuzzleTransform => null;

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool TryFire(Vector3 origin, Vector3 direction)
    {
        if (!CanFire)
            return false;

        OnFire(origin, direction);
        if (fireRate > 0f)
            cooldownTimer = 1f / fireRate;

        return true;
    }

    protected abstract void OnFire(Vector3 origin, Vector3 direction);

    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }

  
    // Called when this weapon gains a level (up to maxLevel).
    // Returns info about what was upgraded for UI.

    public WeaponLevelUpResult LevelUp(WeaponUpgradeContext context)
    {
        if (CurrentLevel >= maxLevel)
            return WeaponLevelUpResult.Empty;

        CurrentLevel++;

        // Decide how many stats to upgrade this level.
        int upgradeCount = WeaponUpgradeLogic.GetUpgradeCount(context);

        var result = new WeaponLevelUpResult(weaponName, CurrentLevel);
        ApplyRandomUpgrades(upgradeCount, context, result);
        return result;
    }

    protected float RollCritDamage(float baseDamage, float chance, out bool isCrit)
    {
        isCrit = chance > 0f && UnityEngine.Random.value < chance;
        return isCrit ? baseDamage * critMultiplier : baseDamage;
    }

    // Implemented in each weapon to choose & apply its own stat upgrades.

    protected abstract void ApplyRandomUpgrades(
        int upgradeCount,
        WeaponUpgradeContext context,
        WeaponLevelUpResult result);
}
