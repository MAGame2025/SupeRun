using UnityEngine;

public class EnemyBrain2 : MonoBehaviour
{
    public Transform player;
    public Rigidbody rb;
    private float speed = 5f;

    private void FixedUpdate()
    {
        // Direction toward player, ignore Y axis
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        dir.Normalize();

        // Compute next position
        Vector3 nextPos = transform.position + dir * speed * Time.fixedDeltaTime;

        // Move enemy
        rb.MovePosition(nextPos);
    }
}
