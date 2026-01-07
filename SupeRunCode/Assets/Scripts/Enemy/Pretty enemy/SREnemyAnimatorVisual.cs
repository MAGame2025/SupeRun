using UnityEngine;

public class SREnemyAnimatorVisual : MonoBehaviour, ISREnemyVisual
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Animator Params")]
    [Tooltip("Float param controlling locomotion. Example: 'Speed'")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("Trigger param for attack. Example: 'Attack'")]
    [SerializeField] private string attackTriggerParam = "Attack";

    [Tooltip("Bool param for death. Example: 'Dead'")]
    [SerializeField] private string deadBoolParam = "Dead";

    [Header("Tuning")]
    [Tooltip("Optional: convert world speed into animator value (depends on your clips).")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Smoothing for animator speed param.")]
    [SerializeField] private float speedDampTime = 0.1f;

    [Header("LOD")]
    [Tooltip("If false, we can stop updating speed every frame (cheap far LOD).")]
    [SerializeField] private bool allowFarLodFreeze = true;

    private int speedHash;
    private int attackHash;
    private int deadHash;

    private bool dead;
    private bool fullLod = true;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        speedHash = Animator.StringToHash(speedParam);
        attackHash = Animator.StringToHash(attackTriggerParam);
        deadHash = Animator.StringToHash(deadBoolParam);
    }

    public void OnSpawn()
    {
        dead = false;
        fullLod = true;

        if (animator == null) return;

        // Reset animator state for pooled enemies
        animator.Rebind();
        animator.Update(0f);

        // Make sure we're not stuck dead
        if (!string.IsNullOrEmpty(deadBoolParam))
            animator.SetBool(deadHash, false);

        // Clear speed
        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedHash, 0f);
    }

    public void SetMoveSpeed(float speed)
    {
        if (dead || animator == null) return;

        // Optional cheap far-LOD behavior
        if (!fullLod && allowFarLodFreeze)
        {
            // In far LOD we can just force a low speed or 0
            animator.SetFloat(speedHash, 0f);
            return;
        }

        float v = speed * speedMultiplier;

        // Use damping if parameter exists
        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedHash, v, speedDampTime, Time.deltaTime);
    }

    public void PlayHit()
    {
        if (dead || animator == null) return;
        if (string.IsNullOrEmpty(attackTriggerParam)) return;

        animator.SetTrigger(attackHash);
    }


    public void PlayDeath()
    {
        if (dead) return;
        dead = true;

        if (animator == null) return;

        if (!string.IsNullOrEmpty(deadBoolParam))
            animator.SetBool(deadHash, true);
        else
            animator.CrossFade("Death", 0.05f);
    }


    public void SetFullLod(bool fullLod)
    {
        this.fullLod = fullLod;

        // Optional: you can also disable animator updates when far
        // (Only do this if you're okay with animation freezing in distance)
        // animator.enabled = fullLod;
    }
}
