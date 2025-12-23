using System.Collections.Generic;
using UnityEngine;


// Builds level-up options (new weapons / weapon upgrades)
// and notifies the UI when the player levels up.

public class LevelUpController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SRWeaponManager weaponManager;

    [Header("Upgrade Settings")]
    [Tooltip("Default upgrade context. Later you can tweak this with luck/items.")]
    public WeaponUpgradeContext upgradeContext = WeaponUpgradeContext.Default;


    // Fired whenever we generate level-up options.
    // LevelUpUI should subscribe to this.

    public System.Action<List<LevelUpOption>> OnLevelUpOptionsGenerated;

    private void Awake()
    {
        TryResolveWeaponManager("Awake");
    }


    // Ensures weaponManager is set.
    // Tries serialized ref first, then searches the scene for SRWeaponManager
    // (which will be on the runtime-spawned player).

    private bool TryResolveWeaponManager(string source)
    {
        if (weaponManager != null)
            return true;

        // Try to find the runtime instance on the spawned player.
        weaponManager = FindAnyObjectByType<SRWeaponManager>();

        if (weaponManager != null)
        {
            Debug.Log($"LevelUpController: Auto-found SRWeaponManager from {source} on '{weaponManager.gameObject.name}'.");
            return true;
        }

        Debug.LogWarning(
            $"LevelUpController: No SRWeaponManager found in scene ({source}). " +
            "This usually means the player prefab hasn't been spawned yet.");
        return false;
    }

    [ContextMenu("Test Level Up")]
    public void TriggerLevelUp()
    {
        if (!TryResolveWeaponManager("TriggerLevelUp"))
            return;   // can't build weapon options without a manager

        List<LevelUpOption> options = GenerateOptions(3);

        if (options.Count == 0)
        {
            Debug.Log("LevelUpController: No level-up options available (all weapons maxed or pool empty).");
            return;
        }

        Debug.Log("=== LEVEL UP! Available options: ===");
        for (int i = 0; i < options.Count; i++)
        {
            Debug.Log($"Option {i + 1}: {options[i].Title} - {options[i].Description}");
        }

        if (OnLevelUpOptionsGenerated != null)
        {
            Debug.Log("LevelUpController: Sending options to UI.");
            OnLevelUpOptionsGenerated.Invoke(options);
        }
        else
        {
            Debug.Log("LevelUpController: No UI listening, auto-picking first option.");
            ApplyOption(options[0]);
        }
    }


    // Called by the UI when the player selects an option.

    public void ApplyOption(LevelUpOption option)
    {
        if (option == null)
            return;

        if (!TryResolveWeaponManager("ApplyOption"))
            return;

        switch (option.Type)
        {
            case LevelUpOptionType.NewWeapon:
                {
                    SRWeaponBase instance =
                        weaponManager.UnlockNewWeapon(option.WeaponPrefabToUnlock, autoEquip: true);

                    if (instance != null)
                        Debug.Log($"LevelUpController: Unlocked new weapon '{instance.name}'.");
                    else
                        Debug.LogWarning("LevelUpController: Failed to unlock new weapon (no slot or already owned).");
                    break;
                }

            case LevelUpOptionType.WeaponUpgrade:
                {
                    WeaponLevelUpResult result =
                        weaponManager.LevelUpWeapon(option.WeaponInstance, upgradeContext);

                    if (result.newLevel > 0)
                    {
                        Debug.Log($"LevelUpController: Upgraded '{result.weaponName}' to level {result.newLevel}.");

                        if (result.upgradeDescriptions != null)
                        {
                            foreach (var line in result.upgradeDescriptions)
                            {
                                Debug.Log("  - " + line);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("LevelUpController: Weapon upgrade failed or already max level.");
                    }
                    break;
                }

                // case LevelUpOptionType.PassiveUpgrade:
                //     // later: apply passive/tome upgrades here.
                //     break;
        }
    }


    // Builds up to 'count' random options from:
    //  - locked weapon prefabs (new weapons)
    //  - upgradeable equipped weapons

    private List<LevelUpOption> GenerateOptions(int count)
    {
        var options = new List<LevelUpOption>();
        var candidates = new List<LevelUpOption>();

        // --- 1. New weapon options ---
        List<SRWeaponBase> lockedPrefabs = weaponManager.GetLockedWeaponPrefabs();
        bool canOfferNewWeapon = weaponManager.HasFreeWeaponSlot && lockedPrefabs.Count > 0;

        if (canOfferNewWeapon)
        {
            foreach (var prefab in lockedPrefabs)
            {
                if (prefab == null) continue;

                candidates.Add(new LevelUpOption
                {
                    Type = LevelUpOptionType.NewWeapon,
                    WeaponPrefabToUnlock = prefab,
                    Title = $"Unlock: {prefab.name}",
                    Description = "New weapon (starts at level 1)."
                });
            }
        }

        // --- 2. Weapon upgrade options ---
        List<SRWeaponBase> upgradeable = weaponManager.GetUpgradeableWeapons();

        foreach (var inst in upgradeable)
        {
            if (inst == null) continue;

            candidates.Add(new LevelUpOption
            {
                Type = LevelUpOptionType.WeaponUpgrade,
                WeaponInstance = inst,
                Title = $"Upgrade: {inst.name}",
                Description = $"Level {inst.CurrentLevel + 1}/{inst.MaxLevel}"
            });
        }

        // (later: 3. passive / item options)

        if (candidates.Count == 0)
            return options;

        int picks = Mathf.Min(count, candidates.Count);

        for (int i = 0; i < picks; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            options.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }

        return options;
    }
}
