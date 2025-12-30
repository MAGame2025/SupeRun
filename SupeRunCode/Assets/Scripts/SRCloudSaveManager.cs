using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

public static class SRCloudSaveManager
{
    private const string KEY_PROGRESS = "playerProgress";

    public static async Task SaveProgress(PlayerProgress progress)
    {
        var data = new Dictionary<string, object>
        {
            { KEY_PROGRESS, progress }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log("[CloudSave] Saved playerProgress");
    }

    public static async Task<PlayerProgress> LoadProgress()
    {
        var keys = new HashSet<string> { KEY_PROGRESS };
        Dictionary<string, Item> loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        if (loaded.TryGetValue(KEY_PROGRESS, out var item))
        {
            var progress = item.Value.GetAs<PlayerProgress>();
            Debug.Log("[CloudSave] Loaded existing playerProgress");
            return progress;
        }

        Debug.Log("[CloudSave] No playerProgress found. Creating new.");
        return new PlayerProgress();
    }
}
