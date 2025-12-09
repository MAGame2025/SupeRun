using System.Collections.Generic;
using UnityEngine;

public class WeaponLevelUpResult
{
    public readonly string weaponName;
    public readonly int newLevel;

    // For UI: "Damage +20%", "Radius +3" etc.
    public readonly List<string> upgradeDescriptions = new List<string>();

    public static WeaponLevelUpResult Empty => new WeaponLevelUpResult("None", 0);

    public WeaponLevelUpResult(string weaponName, int newLevel)
    {
        this.weaponName = weaponName;
        this.newLevel = newLevel;
    }

    public void AddDescription(string text)
    {
        upgradeDescriptions.Add(text);
    }
}
