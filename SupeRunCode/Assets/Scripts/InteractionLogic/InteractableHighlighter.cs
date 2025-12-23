using UnityEngine;

public class InteractableHighlighter : MonoBehaviour
{
    [SerializeField] private GameObject highlightObject;

    private void Awake()
    {
        if (highlightObject != null)
            highlightObject.SetActive(false);
    }

    public void SetHighlighted(bool on)
    {
        if (highlightObject != null)
            highlightObject.SetActive(on);
    }
}
