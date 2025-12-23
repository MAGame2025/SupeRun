using System.Collections;
using UnityEngine;

public class SRNpcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Talk";
    [SerializeField] private float showSeconds = 5f;

    [Header("Dialog UI (world-space)")]
    [SerializeField] private NpcDialogUI dialogUI;

    [Header("Optional Highlight")]
    [SerializeField] private InteractableHighlighter highlighter;

    private Coroutine activeRoutine;

    public string Prompt => prompt;

    public Transform InteractionTransform => transform;

    private void Awake()
    {
        if (dialogUI == null)
            dialogUI = GetComponentInChildren<NpcDialogUI>(true); // finds even if disabled
    }

    public bool CanInteract(GameObject interactor)
    {
        return dialogUI != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        // If already showing, restart timer.
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowDialogRoutine());
    }

    private IEnumerator ShowDialogRoutine()
    {
        dialogUI.Show();

        yield return new WaitForSeconds(showSeconds);

        dialogUI.Hide();
        activeRoutine = null;
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
