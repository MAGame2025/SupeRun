using UnityEngine;

public class SREnemyWeaponHitscan : MonoBehaviour, ISREnemyWeapon
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float range = 40f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private Transform muzzle;

    public void Fire(Vector3 direction)
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        direction.Normalize();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Collide))
        {
            var hp = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.TakeDamage(damage);
            }

            Debug.DrawLine(origin, hit.point, Color.red, 0.15f);
        }
        else
        {
            Debug.DrawLine(origin, origin + direction * range, Color.yellow, 0.15f);
        }
    }
}
