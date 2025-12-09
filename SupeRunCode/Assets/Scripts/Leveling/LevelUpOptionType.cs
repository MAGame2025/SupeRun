using UnityEngine;

public enum LevelUpOptionType
{
    WeaponUpgrade,
    NewWeapon,
    // PassiveUpgrade  // later for items
}

public class LevelUpOption
{
    public LevelUpOptionType Type;

    // For WeaponUpgrade
    public SRWeaponBase WeaponInstance;

    // For NewWeapon
    public SRWeaponBase WeaponPrefabToUnlock;

    public string Title;
    public string Description;
}
