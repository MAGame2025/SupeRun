using UnityEngine;

public class SRPlayerInteractor : MonoBehaviour
{
    public enum Mode
    {
        RaycastFromCamera,
        ProximitySphereClosest
    }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.ProximitySphereClosest;

    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    [Header("Raycast")]
    [SerializeField] private Camera cam;
    [SerializeField] private float rayDistance = 3f;
    [SerializeField] private LayerMask rayMask = ~0;

    [Header("Proximity")]
    [SerializeField] private float proximityRadius = 2.5f;
    [SerializeField] private LayerMask proximityMask = ~0;
    [SerializeField] private int maxHits = 32;

    [Header("Debug / UI")]
    [SerializeField] private bool logPrompt = false;

    private readonly Collider[] hitsBuffer = new Collider[32];
    private IInteractable focused;

    public IInteractable Focused => focused;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = FindAnyObjectByType<InputReader>();

        if (cam == null)
            cam = Camera.main;
    }

    private void Update()
    {
        UpdateFocus();

        if (inputReader != null && inputReader.InteractPressed)
        {
            TryInteract();
        }
    }

    private void UpdateFocus()
    {
        IInteractable candidate = null;

        if (mode == Mode.RaycastFromCamera)
            candidate = FindByRaycast();
        else
            candidate = FindByProximityClosest();

        if (candidate == focused)
            return;

        // focus changed
        if (focused != null)
            focused.OnFocusExit(gameObject);

        focused = candidate;

        if (focused != null)
        {
            focused.OnFocusEnter(gameObject);
            if (logPrompt) Debug.Log($"[Interact] Focus: {focused.Prompt}");
        }
    }

    private IInteractable FindByRaycast()
    {
        if (cam == null) return null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, rayMask, QueryTriggerInteraction.Collide))
        {
            var mono = hit.collider.GetComponentInParent<MonoBehaviour>();
            if (mono != null && mono is IInteractable interactable)
            {
                if (interactable.CanInteract(gameObject))
                    return interactable;
            }
        }
        return null;
    }

    private IInteractable FindByProximityClosest()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            proximityRadius,
            hitsBuffer,
            proximityMask,
            QueryTriggerInteraction.Collide);

        float bestDistSq = float.MaxValue;
        IInteractable best = null;

        for (int i = 0; i < count; i++)
        {
            var c = hitsBuffer[i];
            if (c == null) continue;

            var mono = c.GetComponentInParent<MonoBehaviour>();
            if (mono == null) continue;

            if (mono is not IInteractable interactable) continue;
            if (!interactable.CanInteract(gameObject)) continue;

            Transform t = interactable.InteractionTransform != null ? interactable.InteractionTransform : mono.transform;
            float distSq = (t.position - transform.position).sqrMagnitude;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = interactable;
            }
        }

        return best;
    }

    private void TryInteract()
    {
        if (focused == null)
            return;

        if (!focused.CanInteract(gameObject))
            return;

        focused.Interact(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (mode == Mode.ProximitySphereClosest)
        {
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }
    }
}
