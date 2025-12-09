using UnityEngine;

/// Single XP orb that can be upgraded to higher tiers (more XP value).
public class XPOrb : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private float attractRadius = 6f;
    [SerializeField] private float attractSpeed = 10f;

    [Header("Visuals")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color blueColor = Color.blue;
    [SerializeField] private Color yellowColor = Color.yellow;
    [SerializeField] private Color orangeColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color redColor = Color.red;

    private SRXpManager xpManager;
    private bool attracting;

    public XPOrbTier CurrentTier { get; private set; } = XPOrbTier.Blue;
    public int CurrentXp { get; private set; }

    public void Initialize(SRXpManager manager, Transform player)
    {
        xpManager = manager;
        // we ignore the player argument now and always use manager.PlayerTransform
        Debug.Log($"XPOrb.Initialize: {name}, player={(player != null ? player.name : "NULL (manager will supply)")}");
    }

    public void SetTierAndValue(XPOrbTier tier, int baseXp)
    {
        CurrentTier = tier;

        int multiplier = tier switch
        {
            XPOrbTier.Blue => 1,
            XPOrbTier.Yellow => 5,
            XPOrbTier.Orange => 25,
            XPOrbTier.Red => 100,
            _ => 1
        };

        CurrentXp = baseXp * multiplier;
        UpdateColor();
    }

    public void UpgradeTier(int baseXp)
    {
        // simple example: bump tier up one step, clamp at Red
        int tierInt = (int)CurrentTier;
        tierInt = Mathf.Clamp(tierInt + 1, (int)XPOrbTier.Blue, (int)XPOrbTier.Red);
        CurrentTier = (XPOrbTier)tierInt;

        SetTierAndValue(CurrentTier, baseXp);
    }

    private void UpdateColor()
    {
        if (meshRenderer == null)
            return;

        Color c = CurrentTier switch
        {
            XPOrbTier.Blue => blueColor,
            XPOrbTier.Yellow => yellowColor,
            XPOrbTier.Orange => orangeColor,
            XPOrbTier.Red => redColor,
            _ => blueColor
        };

        meshRenderer.material.color = c;
    }

    private void Update()
    {
        if (xpManager == null)
            return;

        // ALWAYS get the current player from the XP manager.
        Transform player = xpManager.PlayerTransform;
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        float sqrDist = toPlayer.sqrMagnitude;

        if (sqrDist <= pickupRadius * pickupRadius)
        {
            Debug.Log($"XPOrb.Update: {name} within pickup radius, collecting.");
            xpManager.CollectOrb(this);
            return;
        }

        if (!attracting && sqrDist <= attractRadius * attractRadius)
        {
            attracting = true;
            Debug.Log($"XPOrb.Update: {name} started attracting.");
        }

        if (attracting)
        {
            Vector3 dir = toPlayer.normalized;
            transform.position += dir * (attractSpeed * Time.deltaTime);
        }
    }
}

public enum XPOrbTier
{
    Blue = 0,
    Yellow = 1,
    Orange = 2,
    Red = 3
}
