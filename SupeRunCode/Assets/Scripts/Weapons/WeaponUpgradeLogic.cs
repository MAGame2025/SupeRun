using UnityEngine;
using System.Collections.Generic;

public static class WeaponUpgradeLogic
{
    public static int GetUpgradeCount(WeaponUpgradeContext ctx)
    {
        int min = Mathf.Max(1, ctx.baseMinUpgrades);
        int max = Mathf.Max(min, ctx.baseMaxUpgrades);

        // Simple version:
        // - If legendary proc: max upgrades.
        // - Else random between [min, max].
        float legendaryRoll = Random.value;
        if (legendaryRoll < ctx.legendaryChance + ctx.luck)
        {
            return max;
        }

        return Random.Range(min, max + 1);
    }
}
