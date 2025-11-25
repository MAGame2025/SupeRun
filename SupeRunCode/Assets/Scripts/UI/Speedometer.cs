using TMPro;
using UnityEngine;

public class Speedometer : MonoBehaviour
{
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private TextMeshProUGUI speedText;

    private void Update()
    {
        if (playerMotor != null && speedText != null)
        {
            float speed = playerMotor.CurrentSpeed;
            speedText.text = $"Speed: {speed:F1}";
        }
    }
}
