using System.Collections.Generic;
using UnityEngine;

public class SRGrenadeAimIndicator : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private SRGrenadeLauncher launcher;

    [Header("Indicator Visuals")]
    [SerializeField] private Transform landingMarkerPrefab;
    [SerializeField] private Transform clusterRing;

    [Header("Prediction")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private int maxSimSteps = 80;
    [SerializeField] private float simStep = 0.03f;

    [Header("Aim (from WeaponManager)")]
    [SerializeField] private SRWeaponManager weaponManager;


    private readonly List<Transform> markerPool = new List<Transform>(16);
    private readonly List<Transform> activeMarkers = new List<Transform>(16);
    private void Awake()
    {
        if (weaponManager == null)
            weaponManager = SRWeaponManager.Instance;
    }

    private void Update()
    {
        if (weaponManager == null)
            weaponManager = SRWeaponManager.Instance;

        if (weaponManager == null)
        {
            HideAll();
            return;
        }
        if (launcher == null)
        {
            HideAll();
            return;
        }

        SRGrenade grenadePrefab = launcher.GrenadePrefab;
        if (grenadePrefab == null)
        {
            HideAll();
            return;
        }

        Transform muzzle = launcher.MuzzleTransform;
        Vector3 startPos = muzzle != null ? muzzle.position : launcher.transform.position;

        Vector3 aimDir = GetLiveAimDirection(startPos);
        if (aimDir.sqrMagnitude < 0.0001f)
        {
            HideAll();
            return;
        }

        int projectileCount = Mathf.Max(1, launcher.CurrentProjectileCount);
        float spread = launcher.SpreadAngleDegrees;

        Vector3[] dirs = BuildSpreadDirections(aimDir, projectileCount, spread);

        UpdateClusterIndicator(
            startPos,
            dirs,
            grenadePrefab.Speed,
            grenadePrefab.UpwardAngleDegrees,
            grenadePrefab.Gravity,
            launcher.CurrentRadius);
    }

    // --- Core update (same as your existing logic) ---
    private void UpdateClusterIndicator(
        Vector3 startPos,
        Vector3[] aimDirs,
        float speed,
        float upwardAngleDegrees,
        float gravity,
        float explosionRadius)
    {
        if (aimDirs == null || aimDirs.Length == 0)
        {
            HideAll();
            return;
        }

        Vector3[] hits = new Vector3[aimDirs.Length];
        int hitCount = 0;

        for (int i = 0; i < aimDirs.Length; i++)
        {
            if (TryPredictHit(startPos, aimDirs[i], speed, upwardAngleDegrees, gravity, out Vector3 hitPoint))
                hits[hitCount++] = hitPoint;
        }

        if (hitCount == 0)
        {
            HideAll();
            return;
        }

        EnsureMarkers(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            Transform m = activeMarkers[i];
            m.gameObject.SetActive(true);
            m.position = hits[i] + Vector3.up * 0.02f;
        }

        // Ring shows EXACT explosion radius of the weapon (currentRadius)
        Vector3 mainHit = hits[0];

        if (clusterRing != null)
        {
            clusterRing.gameObject.SetActive(true);
            clusterRing.position = mainHit + Vector3.up * 0.02f;

            float diameter = Mathf.Max(0.01f, explosionRadius * 2f);

            Vector3 s = clusterRing.localScale;
            clusterRing.localScale = new Vector3(diameter, s.y, diameter);
        }
    }

    private void HideAll()
    {
        for (int i = 0; i < activeMarkers.Count; i++)
            activeMarkers[i].gameObject.SetActive(false);

        if (clusterRing != null)
            clusterRing.gameObject.SetActive(false);
    }

    private void EnsureMarkers(int count)
    {
        for (int i = count; i < activeMarkers.Count; i++)
            activeMarkers[i].gameObject.SetActive(false);

        while (activeMarkers.Count < count)
        {
            Transform t = GetMarker();
            activeMarkers.Add(t);
        }
    }

    private Transform GetMarker()
    {
        for (int i = 0; i < markerPool.Count; i++)
        {
            if (!markerPool[i].gameObject.activeSelf)
                return markerPool[i];
        }

        if (landingMarkerPrefab == null)
        {
            GameObject go = new GameObject("LandingMarker");
            go.transform.SetParent(transform, false);
            markerPool.Add(go.transform);
            return go.transform;
        }

        Transform inst = Instantiate(landingMarkerPrefab, transform);
        markerPool.Add(inst);
        return inst;
    }

    private bool TryPredictHit(
        Vector3 startPos,
        Vector3 forwardDir,
        float speed,
        float upwardAngleDegrees,
        float gravity,
        out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        if (forwardDir.sqrMagnitude < 0.0001f)
            return false;

        forwardDir.Normalize();

        Vector3 axis = Vector3.Cross(Vector3.up, forwardDir);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;

        Quaternion tilt = Quaternion.AngleAxis(upwardAngleDegrees, axis);
        Vector3 launchDir = tilt * forwardDir;

        Vector3 vel = launchDir * speed;
        Vector3 prev = startPos;

        for (int i = 0; i < maxSimSteps; i++)
        {
            vel += Vector3.up * (gravity * simStep);
            Vector3 next = prev + vel * simStep;

            if (Physics.Linecast(prev, next, out RaycastHit hit, groundMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                return true;
            }

            prev = next;
        }

        return false;
    }

    // Aim ray through the center of the screen (reticle).
    // If your cursor is locked, this matches “mouse aim”.
    private Vector3 GetLiveAimDirection(Vector3 muzzlePos)
    {
        if (weaponManager == null)
            return Vector3.zero;

        Vector3 aimPoint;
        if (!weaponManager.TryGetAimPoint(out aimPoint))
            return Vector3.zero;

        Vector3 dir = aimPoint - muzzlePos;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return dir.normalized;
    }



    private static Vector3[] BuildSpreadDirections(Vector3 aimDir, int count, float coneAngleDeg)
    {
        if (count <= 1)
            return new Vector3[] { aimDir.normalized };

        aimDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, aimDir);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 up = Vector3.Cross(aimDir, right).normalized;

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
            dirs[i + 1] = (aimDir * cos + around * sin).normalized;
        }

        return dirs;
    }
}
