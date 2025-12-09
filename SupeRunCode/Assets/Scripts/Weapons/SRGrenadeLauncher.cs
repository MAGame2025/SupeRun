using System;
using System.Collections.Generic;
using UnityEngine;

public class SRGrenadeLauncher : SRWeaponBase
{
    private enum GrenadeStat
    {
        Damage,
        Knockback,
        Radius,
        FireRate,
        ProjectileCount
    }

    [Header("Grenade Launcher Settings")]
    [SerializeField] private SRGrenade grenadePrefab;
    [SerializeField] private Transform muzzleTransform;

    [Header("Base Stats")]
    [SerializeField] private float baseDamage = 50f;
    [SerializeField] private float baseExplosionRadius = 4f;
    [SerializeField] private float baseMaxKnockbackForce = 25f;

    // This is the time between shots in seconds.
    [SerializeField] private float baseCooldown = 0.8f;

    [SerializeField] private int baseProjectileCount = 1;

    // runtime stats
    private float currentDamage;
    private float currentRadius;
    private float currentMaxKnockback;
    private float currentCooldown;
    private int currentProjectileCount;

    protected override void Awake()
    {
        base.Awake();

        currentDamage = baseDamage;
        currentRadius = baseExplosionRadius;
        currentMaxKnockback = baseMaxKnockbackForce;
        currentCooldown = baseCooldown;
        currentProjectileCount = baseProjectileCount;

        fireRate = 1f / currentCooldown;
    }

    protected override void OnFire(Vector3 origin, Vector3 direction)
    {
        if (grenadePrefab == null)
        {
            Debug.LogWarning("SRGrenadeLauncher has no grenadePrefab assigned.");
            return;
        }

        Vector3 spawnPos = muzzleTransform != null ? muzzleTransform.position : origin;
        Vector3 aimDir = direction.normalized;

        for (int i = 0; i < currentProjectileCount; i++)
        {
            SRGrenade grenade = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            grenade.ConfigureStats(currentDamage, currentRadius, currentMaxKnockback);

            // For now no spread - all grenades share same direction.
            // Later its possible to add slight spread here for multi-projectile upgrades.
            grenade.Launch(spawnPos, aimDir);
        }
    }

    protected override void ApplyRandomUpgrades(
        int upgradeCount,
        WeaponUpgradeContext context,
        WeaponLevelUpResult result)
    {
        Array stats = Enum.GetValues(typeof(GrenadeStat));
        int statsCount = stats.Length;

        // Make a temporary list of all stats so we don't upgrade the same one twice in this level.
        List<GrenadeStat> availableStats = new List<GrenadeStat>(statsCount);
        for (int i = 0; i < statsCount; i++)
            availableStats.Add((GrenadeStat)stats.GetValue(i));

        for (int i = 0; i < upgradeCount; i++)
        {
            if (availableStats.Count == 0)
                break;

            int index = UnityEngine.Random.Range(0, availableStats.Count);
            GrenadeStat chosen = availableStats[index];
            availableStats.RemoveAt(index);

            switch (chosen)
            {
                case GrenadeStat.Damage:
                    currentDamage *= 1.2f;
                    result.AddDescription($"Damage increased to {currentDamage:F1}");
                    break;

                case GrenadeStat.Knockback:
                    currentMaxKnockback *= 1.25f;
                    result.AddDescription($"Knockback increased to {currentMaxKnockback:F1}");
                    break;

                case GrenadeStat.Radius:
                    currentRadius *= 1.3f;  // or currentRadius += 3f;
                    result.AddDescription($"Explosion radius increased to {currentRadius:F1}");
                    break;

                case GrenadeStat.FireRate:
                    currentCooldown *= 0.9f;  // shorter cooldown
                    currentCooldown = Mathf.Max(0.1f, currentCooldown);
                    fireRate = 1f / currentCooldown;
                    result.AddDescription($"Cooldown reduced to {currentCooldown:F2}s");
                    break;

                case GrenadeStat.ProjectileCount:
                    currentProjectileCount += 1;
                    result.AddDescription($"Projectile count increased to {currentProjectileCount}");
                    break;
            }
        }
    }
}
