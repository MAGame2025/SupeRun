using UnityEngine;


    public interface ISRWeapon
    {
        // Try to fire the weapon. Returns true if fired.
        bool TryFire(Vector3 origin, Vector3 direction);

        // Is the weapon currently ready to fire? (no cooldown, ammo ok, etc.)
        bool CanFire { get; }

        // Called when this weapon gets equipped by the player.
        void OnEquip();

        // Called when this weapon gets unequipped.
        void OnUnequip();
    }


