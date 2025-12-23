using UnityEngine;

public class SRWeaponSwitcher : MonoBehaviour
{
    [SerializeField] private SRWeaponManager weaponManager;

    private void Awake()
    {
        if (weaponManager == null)
            weaponManager = GetComponent<SRWeaponManager>();
    }

    private void Update()
    {
        if (weaponManager == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f) weaponManager.CycleWeapon(+1);
        else if (scroll < 0f) weaponManager.CycleWeapon(-1);
    }
}
