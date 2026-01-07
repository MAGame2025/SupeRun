using System;
using TMPro;
using UnityEngine;

public class SRDamageNumber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text tmp;

    [Header("Anim")]
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float floatUpSpeed = 1.6f;
    [SerializeField] private float sidewaysDrift = 0.6f;

    [Header("Scale Punch")]
    [SerializeField] private float startScaleMultiplier = 1.15f;
    [SerializeField] private float scaleReturnSpeed = 14f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color critColor = new Color(1f, 0.55f, 0.1f, 1f); // orange-ish

    private float currentLifetime;
    private float timer;
    private Vector3 driftDir;
    private Vector3 baseScale;
    private Color activeColor;

    private Action<SRDamageNumber> returnToPool;

    private Camera cam;

    private Color baseNormal;
    private Color baseCrit;

    private void Awake()
    {
        if (tmp == null) tmp = GetComponentInChildren<TMP_Text>();
        baseScale = transform.localScale;

        baseNormal = normalColor;
        baseCrit = critColor;
    }


    public void SetPoolReturn(Action<SRDamageNumber> onReturn)
    {
        returnToPool = onReturn;
    }

    public void Play(int damage, bool isCrit)
    {
        if (cam == null) cam = Camera.main;

        currentLifetime = isCrit ? (lifetime * 1.15f) : lifetime;
        timer = currentLifetime;

        Vector2 r = UnityEngine.Random.insideUnitCircle.normalized;
        driftDir = new Vector3(r.x, 0f, r.y);

        if (isCrit)
        {
            tmp.text = $"Crit! {damage}";
            activeColor = critColor;
            transform.localScale = baseScale * (startScaleMultiplier * 1.20f);
        }
        else
        {
            tmp.text = damage.ToString();
            activeColor = normalColor;
            tmp.fontStyle = FontStyles.Normal;
            transform.localScale = baseScale * startScaleMultiplier;
        }

        // apply immediately at full alpha
        tmp.color = new Color(activeColor.r, activeColor.g, activeColor.b, 1f);

    }


    private void Update()
    {
        timer -= Time.deltaTime;

        // Float up + sideways drift
        transform.position += Vector3.up * (floatUpSpeed * Time.deltaTime);
        transform.position += driftDir * (sidewaysDrift * Time.deltaTime);

        // Smooth scale back to base
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale, scaleReturnSpeed * Time.deltaTime);

        // Billboard
        if (cam != null && tmp != null)
        {
            Vector3 toCam = cam.transform.position - tmp.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
                tmp.transform.rotation = Quaternion.LookRotation(toCam);
        }

        float t = Mathf.Clamp01(timer / currentLifetime); 
        float a = Mathf.SmoothStep(0f, 1f, t);

        // force the intended RGB every frame (pool-safe)
        tmp.color = new Color(activeColor.r, activeColor.g, activeColor.b, a);


        if (timer <= 0f)
        {
            returnToPool?.Invoke(this);
        }
    }
}
