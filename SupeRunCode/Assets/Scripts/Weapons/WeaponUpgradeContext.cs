using UnityEngine;

using System.Collections.Generic;

public struct WeaponUpgradeContext
{
    // Base minimum amount of upgrades per level (usually 1).
    // Items / luck can raise this.
    public int baseMinUpgrades;

    // Base maximum amount of upgrades per level (usually 3).
    public int baseMaxUpgrades;

    // 0..1 chance that this level is "legendary" and gives max upgrades.
    // Later Luck stat can increase this.
    public float legendaryChance;

    // Placeholder for future "luck" modifiers.
    public float luck;

    public static WeaponUpgradeContext Default =>
        new WeaponUpgradeContext
        {
            baseMinUpgrades = 1,
            baseMaxUpgrades = 3,
            legendaryChance = 0.1f,
            luck = 0f
        };
}
