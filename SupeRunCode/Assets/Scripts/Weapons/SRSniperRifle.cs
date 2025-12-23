using System;
using System.Collections.Generic;
using UnityEngine;

public class SRSniperRifle : SRWeaponBase
{
    private enum SniperStat
    {
        Damage,
        Knockback,
        Size,
        PreShotCooldown,
        PostShotCooldown,
        ProjectileCount,
        CritChance
    }

    [Header("Sniper Settings")]
    [Tooltip("How far the sniper shot can reach.")]
    [SerializeField] private float range = 80f;

    [Tooltip("Layers that can be hit by the shot (enemies).")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Base Stats")]
    [SerializeField] private float baseDamage = 120f;
    [SerializeField] private float baseKnockback = 20f;

    [Tooltip("Cylinder radius (SphereCast radius). This is what Size upgrades scale.")]
    [SerializeField] private float baseRadius = 0.35f;

    [Tooltip("Time before the shot fires (charging).")]
    [SerializeField] private float basePreShotCooldown = 2f;

    [Tooltip("Time after the shot fires before you can start charging again.")]
    [SerializeField] private float basePostShotCooldown = 1f;

    [SerializeField] private int baseProjectileCount = 1;

    [Range(0f, 1f)]
    [SerializeField] private float baseCritChance = 0.05f;

    [Tooltip("Damage multiplier on crit.")]
    [SerializeField] private float critMultiplier = 2f;

    [Header("Multi-shot Spread")]
    [Tooltip("If ProjectileCount > 1, each shot is rotated by this many degrees around Y.")]
    [SerializeField] private float spreadDegrees = 2.5f;

    // Runtime stats (upgradable)
    private float currentDamage;
    private float currentKnockback;
    private float currentRadius;
    private float currentPreShotCooldown;
    private float currentPostShotCooldown;
    private int currentProjectileCount;
    private float currentCritChance;

    // Charging state
    private bool charging;
    private float chargeTimer;

    protected override void Awake()
    {
        base.Awake();

        currentDamage = baseDamage;
        currentKnockback = baseKnockback;
        currentRadius = baseRadius;
        currentPreShotCooldown = basePreShotCooldown;
        currentPostShotCooldown = basePostShotCooldown;
        currentProjectileCount = baseProjectileCount;
        currentCritChance = baseCritChance;

        // IMPORTANT:
        // We control cooldownTimer manually (charge+recovery),
        // so we don't want SRWeaponBase to overwrite it with fireRate logic.
        fireRate = 0f;
    }

    protected override void Update()
    {
        base.Update();

        if (!charging)
            return;

        chargeTimer -= Time.deltaTime;
        if (chargeTimer > 0f)
            return;

        charging = false;

        // Fire the actual delayed shot now.
        FireSniperShot();
    }

    protected override void OnFire(Vector3 origin, Vector3 direction)
    {
        // This is called only when cooldownTimer <= 0 (SRWeaponBase gate).
        // So here we START charging, and block firing until charge+recovery ends.

        charging = true;
        chargeTimer = currentPreShotCooldown;

        // Block TryFire() from being accepted until after charge + post cooldown.
        cooldownTimer = currentPreShotCooldown + currentPostShotCooldown;
    }

    private void FireSniperShot()
    {
        Camera cam = Camera.main;

        // Prefer a ray from the camera center, so it feels like "aim where you look".
        Vector3 origin = cam != null ? cam.transform.position : transform.position;
        Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
        dir.Normalize();

        int shots = Mathf.Max(1, currentProjectileCount);
        float radius = Mathf.Max(0.01f, currentRadius);

        // Multi-shot: small yaw spread around camera forward
        int mid = shots / 2;

        for (int i = 0; i < shots; i++)
        {
            int offsetIndex = i - mid;
            float yaw = offsetIndex * spreadDegrees;

            Vector3 shotDir = Quaternion.AngleAxis(yaw, Vector3.up) * dir;
            DoPiercingCylinderHit(origin, shotDir, radius);
        }
    }

    private void DoPiercingCylinderHit(Vector3 origin, Vector3 direction, float radius)
    {
        // SphereCastAll approximates a cylinder sweep (good enough for “cylinder hit detection”).
        Ray ray = new Ray(origin, direction);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            radius,
            range,
            hitMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return;

        // Sort by distance so it behaves predictably
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Crit roll once per "shot"
        bool isCrit = UnityEngine.Random.value < currentCritChance;
        float dmg = isCrit ? currentDamage * critMultiplier : currentDamage;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            // Damage (works even if you don't have a shared interface)
            // If your enemy health script has TakeDamage(float), this will call it.
            col.SendMessageUpwards("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);

            // Knockback: your enemy has SREnemyLite.ApplyKnockback(Vector3)
            var enemyLite = col.GetComponentInParent<SREnemyLite>();
            if (enemyLite != null)
            {
                Vector3 force = direction.normalized * currentKnockback;
                enemyLite.ApplyKnockback(force);
            }
        }
    }

    protected override void ApplyRandomUpgrades(int upgradeCount, WeaponUpgradeContext context, WeaponLevelUpResult result)
    {
        Array stats = Enum.GetValues(typeof(SniperStat));
        int statsCount = stats.Length;

        List<SniperStat> available = new List<SniperStat>(statsCount);
        for (int i = 0; i < statsCount; i++)
            available.Add((SniperStat)stats.GetValue(i));

        for (int i = 0; i < upgradeCount; i++)
        {
            if (available.Count == 0)
                break;

            int index = UnityEngine.Random.Range(0, available.Count);
            SniperStat chosen = available[index];
            available.RemoveAt(index);

            switch (chosen)
            {
                case SniperStat.Damage:
                    currentDamage *= 1.25f;
                    result.AddDescription($"Damage increased to {currentDamage:F1}");
                    break;

                case SniperStat.Knockback:
                    currentKnockback *= 1.25f;
                    result.AddDescription($"Knockback increased to {currentKnockback:F1}");
                    break;

                case SniperStat.Size:
                    currentRadius *= 1.18f;
                    result.AddDescription($"Size increased (radius {currentRadius:F2})");
                    break;

                case SniperStat.PreShotCooldown:
                    currentPreShotCooldown *= 0.90f;
                    currentPreShotCooldown = Mathf.Max(0.25f, currentPreShotCooldown);
                    result.AddDescription($"Charge time reduced to {currentPreShotCooldown:F2}s");
                    break;

                case SniperStat.PostShotCooldown:
                    currentPostShotCooldown *= 0.90f;
                    currentPostShotCooldown = Mathf.Max(0.10f, currentPostShotCooldown);
                    result.AddDescription($"Recovery reduced to {currentPostShotCooldown:F2}s");
                    break;

                case SniperStat.ProjectileCount:
                    currentProjectileCount += 1;
                    result.AddDescription($"Projectile count increased to {currentProjectileCount}");
                    break;

                case SniperStat.CritChance:
                    currentCritChance += 0.05f;
                    currentCritChance = Mathf.Clamp01(currentCritChance);
                    result.AddDescription($"Crit chance increased to {currentCritChance:P0}");
                    break;
            }
        }
    }
}
