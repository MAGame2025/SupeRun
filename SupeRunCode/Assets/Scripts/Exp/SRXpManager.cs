using System;
using System.Collections.Generic;
using UnityEngine;

// Manages player XP, levels, and XP orbs on the ground.
public class SRXpManager : MonoBehaviour
{
    public static SRXpManager Instance { get; private set; }

    [SerializeField] private Transform playerTransform;

    [Header("XP Progression")]
    [SerializeField] private int startingLevel = 1;
    [SerializeField] private int baseXpToLevel = 10;      // XP needed for level 1 -> 2
    [SerializeField] private float xpGrowthFactor = 1.25f; // how fast XP requirement grows
    [SerializeField] private int maxLevel = 0;            // 0 = infinite

    [Header("XP Orbs")]
    [SerializeField] private XPOrb xpOrbPrefab;
    [SerializeField] private int initialOrbPoolSize = 100;
    [SerializeField] private int maxActiveOrbs = 200;

    [Tooltip("Base XP granted by the lowest-tier orb (Blue).")]
    [SerializeField] private int baseXpPerOrb = 2;

    [Header("References")]
    [SerializeField] private LevelUpController levelUpController;

    // Runtime state
    public int CurrentLevel { get; private set; }
    public int CurrentXp { get; private set; }
    public int XpToNextLevel { get; private set; }

    /// Player the orbs should follow / measure distance to.
    public Transform PlayerTransform => playerTransform;

    // Events for UI
    public event Action<int, int, int> OnXpChanged;   // (level, currentXp, xpToNext)

    // Orb pooling
    private readonly Queue<XPOrb> orbPool = new();
    private readonly List<XPOrb> activeOrbs = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SRXpManager: duplicate instance, destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("SRXpManager: Awake – instance set.", this);

        if (levelUpController == null)
            levelUpController = GetComponent<LevelUpController>();
    }

    private void Start()
    {
        Debug.Log("SRXpManager: Start()");

        // Fallback: try to find a Player in the scene *if* we don't already have one.
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                Debug.Log("SRXpManager: PlayerTransform auto-found as " + playerTransform.name);
            }
            else
            {
                Debug.LogWarning("SRXpManager: PlayerTransform is still NULL in Start().");
            }
        }
        else
        {
            Debug.Log("SRXpManager: PlayerTransform (already set) is " + playerTransform.name);
        }

        // Init XP state
        CurrentLevel = startingLevel;
        CurrentXp = 0;
        XpToNextLevel = CalculateXpToNextLevel(CurrentLevel);
        Debug.Log($"SRXpManager: Init XP – Level {CurrentLevel}, XP {CurrentXp}/{XpToNextLevel}");

        // Prewarm orb pool
        if (xpOrbPrefab != null)
        {
            for (int i = 0; i < initialOrbPoolSize; i++)
            {
                XPOrb orb = Instantiate(xpOrbPrefab, transform);
                orb.gameObject.SetActive(false);
                // pass null player, we'll always ask manager at runtime
                orb.Initialize(this, null);
                orbPool.Enqueue(orb);
            }
            Debug.Log($"SRXpManager: Prewarmed {initialOrbPoolSize} orbs.");
        }

        NotifyXpChanged();
    }

    /// Call this from your player-spawn code once the real player instance exists.
    public void RegisterPlayer(Transform player)
    {
        playerTransform = player;
        Debug.Log("SRXpManager: RegisterPlayer → " + (player ? player.name : "NULL"));
    }

    private int CalculateXpToNextLevel(int level)
    {
        float f = baseXpToLevel * Mathf.Pow(xpGrowthFactor, level - 1);
        return Mathf.CeilToInt(f);
    }

    private void NotifyXpChanged()
    {
        OnXpChanged?.Invoke(CurrentLevel, CurrentXp, XpToNextLevel);
    }

    //  Public API
    public void AddXp(int amount)
    {
        if (amount <= 0)
            return;

        Debug.Log($"SRXpManager.AddXp: +{amount} XP (before: {CurrentXp}/{XpToNextLevel}, level {CurrentLevel})");

        CurrentXp += amount;

        bool leveledUp = false;

        while (CurrentXp >= XpToNextLevel && (maxLevel == 0 || CurrentLevel < maxLevel))
        {
            CurrentXp -= XpToNextLevel;
            CurrentLevel++;
            XpToNextLevel = CalculateXpToNextLevel(CurrentLevel);
            leveledUp = true;

            Debug.Log($"SRXpManager.AddXp: LEVEL UP → now level {CurrentLevel}, XP {CurrentXp}/{XpToNextLevel}");

            if (levelUpController != null)
            {
                levelUpController.TriggerLevelUp();
            }
        }

        NotifyXpChanged();
    }


    /// Enemy death calls this.
    /// orbCount: how many small orbs to try to spawn (2-5).
    /// xpPerOrb: how much XP the lowest-tier orb should grant (usually 2).
    public void SpawnXpOrbs(Vector3 position, int orbCount, int xpPerOrb)
    {
        Debug.Log($"SRXpManager.SpawnXpOrbs at {position}, count {orbCount}, xpPerOrb {xpPerOrb}");

        if (xpOrbPrefab == null || orbCount <= 0)
            return;

        for (int i = 0; i < orbCount; i++)
        {
            // If we already have too many orbs on the ground,
            // upgrade an existing orb instead of spawning a new one.
            if (activeOrbs.Count >= maxActiveOrbs && activeOrbs.Count > 0)
            {
                XPOrb target = activeOrbs[UnityEngine.Random.Range(0, activeOrbs.Count)];
                target.UpgradeTier(xpPerOrb);
            }
            else
            {
                XPOrb orb = GetOrbFromPool();
                orb.transform.position = position + UnityEngine.Random.insideUnitSphere * 0.5f;
                orb.transform.position = new Vector3(orb.transform.position.x, position.y, orb.transform.position.z);

                orb.SetTierAndValue(XPOrbTier.Blue, xpPerOrb);
                orb.gameObject.SetActive(true);
                activeOrbs.Add(orb);
            }
        }
    }

    internal void CollectOrb(XPOrb orb)
    {
        Debug.Log($"SRXpManager.CollectOrb: orb {orb.name} value {orb.CurrentXp}");
        // Called by XPOrb when player picks it up.
        AddXp(orb.CurrentXp);

        orb.gameObject.SetActive(false);
        activeOrbs.Remove(orb);
        orbPool.Enqueue(orb);
    }

    private XPOrb GetOrbFromPool()
    {
        XPOrb orb;
        if (orbPool.Count > 0)
        {
            orb = orbPool.Dequeue();
        }
        else
        {
            orb = Instantiate(xpOrbPrefab, transform);
            orb.Initialize(this, playerTransform);
        }
        return orb;
    }
}
