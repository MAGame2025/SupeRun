using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SRChestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Open Chest";
    [SerializeField] private bool oneTime = true;

    [Header("Visuals (can be null)")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;

    [Header("Optional Highlight")]
    [SerializeField] private InteractableHighlighter highlighter;

    private bool opened;

    public string Prompt => opened ? "Opened" : prompt;
    public Transform InteractionTransform => transform;

    private void Awake()
    {
        // Ensure the collider used for interaction exists and stays active.
        // IMPORTANT: This collider must be on an always-active object (this object).
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        ApplyVisualState();
    }

    public bool CanInteract(GameObject interactor)
    {
        if (!oneTime) return true;
        return !opened;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        opened = true;
        ApplyVisualState();

        Debug.Log("[Chest] Opened");
    }

    private void ApplyVisualState()
    {
        if (!opened)
        {
            // Closed state: prefer showing closedVisual if we have it.
            if (closedVisual != null) closedVisual.SetActive(true);
            if (openVisual != null) openVisual.SetActive(false);
        }
        else
        {
            // Opened state: prefer showing openVisual if we have it.
            if (openVisual != null) openVisual.SetActive(true);
            if (closedVisual != null) closedVisual.SetActive(false);

            // If openVisual is missing, we still "open" logically, but visuals won't change.
            // That's fine for placeholder chests.
        }
    }

    public void OnFocusEnter(GameObject interactor)
    {
        if (highlighter != null)
            highlighter.SetHighlighted(true);
    }

    public void OnFocusExit(GameObject interactor)
    {
        if (highlighter != null)
            highlighter.SetHighlighted(false);
    }
}
