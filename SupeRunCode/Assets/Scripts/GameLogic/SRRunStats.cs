using System;
using UnityEngine;

public class SRRunStats : MonoBehaviour
{
    public static SRRunStats Instance { get; private set; }

    [SerializeField] private int enemiesKilled;
    public int EnemiesKilled => enemiesKilled;

    [SerializeField] private float elapsedTime;
    public float ElapsedTime => elapsedTime;

    public event Action<int> OnEnemiesKilledChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // If you add SRRunState later, you can gate time here:
        // if (SRRunState.IsLevelComplete) return;

        elapsedTime += Time.deltaTime;
    }

    public void ResetRun()
    {
        enemiesKilled = 0;
        elapsedTime = 0f;
        OnEnemiesKilledChanged?.Invoke(enemiesKilled);
    }

    public void AddKill(int amount = 1)
    {
        if (amount <= 0) return;

        enemiesKilled += amount;
        OnEnemiesKilledChanged?.Invoke(enemiesKilled);
    }
}
