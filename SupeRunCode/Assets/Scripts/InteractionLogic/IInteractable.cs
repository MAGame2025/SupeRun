using UnityEngine;

public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);

    // Optional: for highlighting
    void OnFocusEnter(GameObject interactor);
    void OnFocusExit(GameObject interactor);

    // Where should we measure distance from?
    Transform InteractionTransform { get; }
}
