using UnityEngine;

public class NpcDialogUI : MonoBehaviour
{
    [SerializeField] private GameObject dialogRoot; // the object to show/hide

    private void Awake()
    {
        if (dialogRoot == null)
            dialogRoot = gameObject;

        dialogRoot.SetActive(false);
    }

    public void Show()
    {
        if (dialogRoot != null) dialogRoot.SetActive(true);
    }

    public void Hide()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }
}
