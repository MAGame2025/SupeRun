using System.Collections.Generic;
using UnityEngine;

public class SRPlayerWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform firePoint;  // where the grenade spawns from
    [SerializeField] private SRGrenade grenadePrefab;

    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.3f;
    [Tooltip("How wide the spread cone is in degrees.")]
    [SerializeField] private float spreadAngle = 5f;

    [Header("Pooling")]
    [SerializeField] private int initialPoolSize = 20;

    private float fireTimer;
    private readonly Queue<SRGrenade> grenadePool = new();

    private void Awake()
    {
        if (inputReader == null)
            inputReader = FindAnyObjectByType<InputReader>();

        if (firePoint == null)
            firePoint = this.transform; // fallback

        // Prewarm grenade pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            var g = Instantiate(grenadePrefab);
            g.gameObject.SetActive(false);
            g.OnReturnToPool = ReturnGrenadeToPool;
            grenadePool.Enqueue(g);
        }
    }

    private void Update()
    {
        fireTimer -= Time.deltaTime;

        if (inputReader != null && inputReader.ShootPressed)
        {
            TryFire();
        }
    }

    private void TryFire()
    {
        if (fireTimer > 0f)
            return;

        fireTimer = fireCooldown;

        // Get a grenade from pool
        SRGrenade grenade;
        if (grenadePool.Count > 0)
        {
            grenade = grenadePool.Dequeue();
        }
        else
        {
            grenade = Instantiate(grenadePrefab);
            grenade.OnReturnToPool = ReturnGrenadeToPool;
        }

        grenade.gameObject.SetActive(true);

        // Forward direction from camera or player
        Vector3 forward = GetAimDirection();

        // Apply random spread within a cone
        forward = ApplySpread(forward, spreadAngle);

        grenade.Launch(firePoint.position, forward);
    }

    private void ReturnGrenadeToPool(SRGrenade grenade)
    {
        grenade.gameObject.SetActive(false);
        grenadePool.Enqueue(grenade);
    }

    private Vector3 GetAimDirection()
    {
        if (Camera.main != null)
        {
            // from camera forward projected horizontally
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.0001f)
                camForward = transform.forward;
            return camForward.normalized;
        }

        return transform.forward;
    }

    private Vector3 ApplySpread(Vector3 dir, float angle)
    {
        // Random rotation around up axis and small pitch up/down
        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-angle, angle), Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-angle, angle), Vector3.right);
        return (yaw * pitch * dir).normalized;
    }
}
