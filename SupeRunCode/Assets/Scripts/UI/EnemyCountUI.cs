using TMPro;
using UnityEngine;

public class EnemyCountUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    void Update()
    {
        if (SREnemyManager.Instance != null)
        {
            text.text = "Enemies: " + SREnemyManager.Instance.ActiveEnemyCount;
        }
    }
}
