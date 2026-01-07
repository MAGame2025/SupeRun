using TMPro;
using UnityEngine;

public class SRObjectiveUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text progressText;

    public void Set(string title, string progress)
    {
        if (titleText != null) titleText.text = title;
        if (progressText != null) progressText.text = progress;
    }
}
