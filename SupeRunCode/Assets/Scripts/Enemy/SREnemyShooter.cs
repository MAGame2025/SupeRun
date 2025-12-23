using System.Collections.Generic;
using UnityEngine;

public class SREnemyShooter : MonoBehaviour
{
    public enum FirePattern
    {
        FireAllThenCooldown,
        RandomOnePerCooldown
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float shootDistance = 12f;

    [Header("Weapons")]
    [SerializeField] private List<MonoBehaviour> weaponComponents = new List<MonoBehaviour>();

    [Header("Pattern")]
    [SerializeField] private FirePattern pattern = FirePattern.FireAllThenCooldown;
    [SerializeField] private float cooldown = 1.25f;

    [Header("Accuracy")]
    [Tooltip("0 = perfect, higher = more spread.")]
    [SerializeField] private float spreadDegrees = 4f;

    [Tooltip("Aim a bit upward (player chest).")]
    [SerializeField] private float aimHeightOffset = 1.0f;

    private readonly List<ISREnemyWeapon> weapons = new List<ISREnemyWeapon>();
    private float timer;

    private void Awake()
    {
        RebuildWeaponsList();
    }

    private void OnValidate()
    {
        // helps keep list clean in editor
        if (weaponComponents == null) weaponComponents = new List<MonoBehaviour>();
    }

    public void RebuildWeaponsList()
    {
        weapons.Clear();

        for (int i = 0; i < weaponComponents.Count; i++)
        {
            var mb = weaponComponents[i];
            if (mb == null) continue;

            if (mb is ISREnemyWeapon w)
                weapons.Add(w);
        }
    }

    private void Update()
    {
        if (target == null)
        {
            var mgr = SREnemyManager.Instance;
            if (mgr != null && mgr.Player != null)
                target = mgr.Player;
        }

        if (target == null || weapons.Count == 0)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        float distSq = (target.position - transform.position).sqrMagnitude;
        if (distSq > shootDistance * shootDistance)
            return;

        if (pattern == FirePattern.FireAllThenCooldown)
        {
            FireAll();
            timer = cooldown;
        }
        else
        {
            FireRandom();
            timer = cooldown;
        }
    }

    private void FireAll()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            Vector3 dir = GetAimDirectionWithSpread();
            weapons[i].Fire(dir);
        }
    }

    private void FireRandom()
    {
        int idx = Random.Range(0, weapons.Count);
        Vector3 dir = GetAimDirectionWithSpread();
        weapons[idx].Fire(dir);
    }

    private Vector3 GetAimDirectionWithSpread()
    {
        Vector3 aimPoint = target.position + Vector3.up * aimHeightOffset;
        Vector3 baseDir = (aimPoint - transform.position).normalized;

        if (spreadDegrees <= 0.001f)
            return baseDir;

        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.right);

        return (yaw * pitch * baseDir).normalized;
    }
}
