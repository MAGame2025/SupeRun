using UnityEngine;

public class PlayerStates : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Jumping,
        Falling,
        Crouching,
        Sliding
    }
}
