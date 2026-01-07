using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SRDamageNumberManager : MonoBehaviour
{
    public static SRDamageNumberManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private SRDamageNumber damageNumberPrefab;

    [Header("Pooling")]
    [SerializeField] private int prewarmCount = 30;

    private readonly Queue<SRDamageNumber> pool = new Queue<SRDamageNumber>(64);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Prewarm();
    }

    private void Prewarm()
    {
        if (damageNumberPrefab == null) return;

        for (int i = 0; i < prewarmCount; i++)
        {
            SRDamageNumber dn = Instantiate(damageNumberPrefab, transform);
            dn.gameObject.SetActive(false);
            dn.SetPoolReturn(ReturnToPool);
            pool.Enqueue(dn);
        }
    }

    public void Spawn(Vector3 worldPos, int damage, bool isCrit)
    {
        if (damageNumberPrefab == null) return;

        SRDamageNumber dn;
        if (pool.Count > 0) dn = pool.Dequeue();
        else
        {
            dn = Instantiate(damageNumberPrefab, transform);
            dn.SetPoolReturn(ReturnToPool);
        }

        // --- NEW: random offset so it’s not static ---
        Vector2 r = UnityEngine.Random.insideUnitCircle;
        float horiz = 0.25f;     // tweak in inspector later if you want
        float vert = 0.35f;
        worldPos += new Vector3(r.x * horiz, UnityEngine.Random.Range(0.05f, vert), r.y * horiz);

        dn.gameObject.SetActive(true);
        dn.transform.position = worldPos;

        dn.Play(damage, isCrit);
    }


    private void ReturnToPool(SRDamageNumber dn)
    {
        dn.gameObject.SetActive(false);
        dn.transform.SetParent(transform, true);
        pool.Enqueue(dn);
    }
}
