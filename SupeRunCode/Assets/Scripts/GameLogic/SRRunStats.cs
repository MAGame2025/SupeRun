using System;
using UnityEngine;

public class SRRunStats : MonoBehaviour
{
    public static SRRunStats Instance { get; private set; }

    [SerializeField] private int enemiesKilled;
    public int EnemiesKilled => enemiesKilled;

    public event Action<int> OnEnemiesKilledChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optional: DontDestroyOnLoad(gameObject); // only if your runs span scenes
    }

    public void ResetRun()
    {
        enemiesKilled = 0;
        OnEnemiesKilledChanged?.Invoke(enemiesKilled);
    }

    public void AddKill(int amount = 1)
    {
        if (amount <= 0) return;

        enemiesKilled += amount;
        OnEnemiesKilledChanged?.Invoke(enemiesKilled);
    }
}
