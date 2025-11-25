using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyBrain : MonoBehaviour
{
    private Rigidbody rb;
    private Transform player;
    private float moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // keep upright
    }

    // Called from Enemy.Initialize
    public void Initialize(Transform player, float moveSpeed)
    {
        this.player = player;
        this.moveSpeed = moveSpeed;
    }

    // Called from Enemy.MyFixedUpdate (NOT from MonoBehaviour.FixedUpdate)
    public void MyFixedUpdate(float dt)
    {
        if (player == null || rb == null)
            return;

        // Move toward player on XZ
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        Vector3 nextPos = rb.position + dir * moveSpeed * dt;
        rb.MovePosition(nextPos);
    }
}
