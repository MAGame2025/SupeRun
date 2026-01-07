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
        ProjectileCount,
        CritChance
    }


    [Header("Grenade Launcher Settings")]
    [SerializeField] private SRGrenade grenadePrefab;
    [SerializeField] private Transform muzzleTransform;

    [Header("Aim Indicator")]
    [SerializeField] private SRGrenadeAimIndicator aimIndicator;

    [Header("Spread")]
    [Tooltip("Max cone angle for multi-projectile shots.")]
    [SerializeField] private float spreadAngleDegrees = 8f;

    [Header("Pooling")]
    [SerializeField] private int prewarmCount = 16;

    [Header("Base Stats")]
    [SerializeField] private float baseDamage = 50f;
    [SerializeField] private float baseExplosionRadius = 4f;
    [SerializeField] private float baseMaxKnockbackForce = 25f;
    [SerializeField] private float baseCooldown = 0.8f;
    [SerializeField] private int baseProjectileCount = 1;

    [Header("Muzzle FX")]
    [SerializeField] private ParticleSystem muzzleVfx;
    [SerializeField] private AudioClip muzzleSfx;
    [SerializeField] private float muzzleSfxVolume = 1f;

    [Header("Weapon Recoil")]
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private Vector3 recoilLocalOffset = new Vector3(0f, 0f, -0.12f);
    [SerializeField] private float recoilKickSpeed = 30f;
    [SerializeField] private float recoilReturnSpeed = 18f;

    [Header("Aiming")]
    [SerializeField]
    private bool useCustomAimViewportPoint = true;

    [SerializeField]
    private Vector2 aimViewportPoint = new Vector2(0.5f, 1.0f); // or 0.95

    public override bool UseCustomAimViewportPoint => useCustomAimViewportPoint;
    public override Vector2 AimViewportPoint => aimViewportPoint;

    public SRGrenade GrenadePrefab => grenadePrefab;
    public override Transform MuzzleTransform => muzzleTransform;

    public float CurrentRadius => currentRadius;
    public int CurrentProjectileCount => currentProjectileCount;
    public float SpreadAngleDegrees => spreadAngleDegrees;

    // runtime stats
    private float currentDamage;
    private float currentRadius;
    private float currentMaxKnockback;
    private float currentCooldown;
    private int currentProjectileCount;

    private Vector3 weaponVisualDefaultLocalPos;
    private Vector3 weaponVisualTargetLocalPos;

    private readonly Queue<SRGrenade> grenadePool = new Queue<SRGrenade>(32);
    private Transform poolRoot;

    protected override void Awake()
    {
        base.Awake();

        currentDamage = baseDamage;
        currentRadius = baseExplosionRadius;
        currentMaxKnockback = baseMaxKnockbackForce;
        currentCooldown = baseCooldown;
        currentProjectileCount = baseProjectileCount;

        fireRate = 1f / currentCooldown;

        if (weaponVisual != null)
        {
            weaponVisualDefaultLocalPos = weaponVisual.localPosition;
            weaponVisualTargetLocalPos = weaponVisualDefaultLocalPos;
        }

        poolRoot = new GameObject("GrenadePool").transform;
        poolRoot.SetParent(transform, false);

        PrewarmPool();
    }

    protected override void Update()
    {
        base.Update();

        if (weaponVisual != null)
        {
            float dt = Time.deltaTime;

            weaponVisualTargetLocalPos = Vector3.Lerp(
                weaponVisualTargetLocalPos,
                weaponVisualDefaultLocalPos,
                1f - Mathf.Exp(-recoilReturnSpeed * dt));

            weaponVisual.localPosition = Vector3.Lerp(
                weaponVisual.localPosition,
                weaponVisualTargetLocalPos,
                1f - Mathf.Exp(-recoilKickSpeed * dt));
        }
    }


    protected override void OnFire(Vector3 origin, Vector3 direction)
    {
        if (grenadePrefab == null)
        {
            Debug.LogWarning("SRGrenadeLauncher has no grenadePrefab assigned.");
            return;
        }

        Vector3 spawnPos = muzzleTransform != null ? muzzleTransform.position : origin;
        Vector3 baseAimDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        // Muzzle FX
        if (muzzleVfx != null)
            muzzleVfx.Play();

        if (muzzleSfx != null)
            AudioSource.PlayClipAtPoint(muzzleSfx, spawnPos, muzzleSfxVolume);

        // Recoil kick
        if (weaponVisual != null)
            weaponVisualTargetLocalPos = weaponVisualDefaultLocalPos + recoilLocalOffset;

        Vector3[] dirs = BuildSpreadDirections(baseAimDir, currentProjectileCount, spreadAngleDegrees);

        for (int i = 0; i < dirs.Length; i++)
        {
            SRGrenade grenade = GetGrenadeFromPool();
            grenade.transform.position = spawnPos;
            grenade.transform.rotation = Quaternion.identity;

            bool isCrit;
            float dmg = RollCritDamage(currentDamage, currentCritChance, out isCrit);
            grenade.ConfigureStats(dmg, currentRadius, currentMaxKnockback, isCrit);
            grenade.Launch(spawnPos, dirs[i]);
        }
    }

    protected override void ApplyRandomUpgrades(
        int upgradeCount,
        WeaponUpgradeContext context,
        WeaponLevelUpResult result)
    {
        Array stats = Enum.GetValues(typeof(GrenadeStat));
        int statsCount = stats.Length;

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
                    currentRadius *= 1.3f;
                    result.AddDescription($"Explosion radius increased to {currentRadius:F1}");
                    break;

                case GrenadeStat.FireRate:
                    currentCooldown *= 0.9f;
                    currentCooldown = Mathf.Max(0.1f, currentCooldown);
                    fireRate = 1f / currentCooldown;
                    result.AddDescription($"Cooldown reduced to {currentCooldown:F2}s");
                    break;

                case GrenadeStat.ProjectileCount:
                    currentProjectileCount += 1;
                    result.AddDescription($"Projectile count increased to {currentProjectileCount}");
                    break;

                case GrenadeStat.CritChance:
                    currentCritChance += 0.05f;
                    currentCritChance = Mathf.Clamp01(currentCritChance);
                    result.AddDescription($"Crit chance increased to {currentCritChance:P0}");
                    break;

            }
        }
    }

    // ---------------- Pooling ----------------

    private void PrewarmPool()
    {
        if (grenadePrefab == null)
            return;

        for (int i = 0; i < prewarmCount; i++)
        {
            SRGrenade g = Instantiate(grenadePrefab, poolRoot);
            g.gameObject.SetActive(false);
            g.OnReturnToPool = ReturnGrenadeToPool;
            grenadePool.Enqueue(g);
        }
    }

    private SRGrenade GetGrenadeFromPool()
    {
        SRGrenade g;

        if (grenadePool.Count > 0)
        {
            g = grenadePool.Dequeue();
        }
        else
        {
            g = Instantiate(grenadePrefab, poolRoot);
            g.OnReturnToPool = ReturnGrenadeToPool;
        }

        g.gameObject.SetActive(true);
        return g;
    }

    private void ReturnGrenadeToPool(SRGrenade g)
    {
        if (g == null) return;

        g.gameObject.SetActive(false);
        g.transform.SetParent(poolRoot, false);
        grenadePool.Enqueue(g);
    }

    // ---------------- Spread ----------------

    private static Vector3[] BuildSpreadDirections(Vector3 aimDir, int count, float coneAngleDeg)
    {
        if (count <= 1)
            return new Vector3[] { aimDir.normalized };

        aimDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, aimDir);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 up = Vector3.Cross(aimDir, right).normalized;

        // cluster pattern:
        // - include center
        // - rest distributed on a circle at coneAngleDeg
        Vector3[] dirs = new Vector3[count];
        dirs[0] = aimDir;

        float rad = coneAngleDeg * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);

        int ringCount = count - 1;
        for (int i = 0; i < ringCount; i++)
        {
            float yaw = (360f * i) / ringCount;
            float yawRad = yaw * Mathf.Deg2Rad;

            Vector3 around = (right * Mathf.Cos(yawRad) + up * Mathf.Sin(yawRad)).normalized;
            Vector3 d = (aimDir * cos + around * sin).normalized;
            dirs[i + 1] = d;
        }

        return dirs;
    }
}
