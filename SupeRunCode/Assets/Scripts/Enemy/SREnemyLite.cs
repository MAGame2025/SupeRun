using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SREnemyLite : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 10f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Climbing")]
    [SerializeField] private float climbCheckDistance = 0.6f;
    [SerializeField] private float climbUpSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float climbGravityScale = 0.1f;
    [SerializeField] private LayerMask climbObstacleMask;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float maxClimbHeight = 8f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDamping = 10f;

    [Header("Contact Damage")]
    [Tooltip("Damage dealt to player each time contact damage triggers.")]
    [SerializeField] private float contactDamage = 10f;

    [Tooltip("Seconds between damage ticks while enemy is in contact range.")]
    [SerializeField] private float contactDamageInterval = 0.5f;

    [Tooltip("Radius around the enemy within which it can hurt the player.")]
    [SerializeField] private float contactRadius = 1.2f;

    // how long we must be blocked before climbing
    [Tooltip("How long the enemy must be blocked before starting to climb.")]
    [SerializeField] private float blockedBeforeClimbTime = 1.5f;

    // how much forward movement counts as 'moving' (units / sec)
    [Tooltip("Minimum forward speed considered 'not blocked'.")]
    [SerializeField] private float minForwardSpeed = 0.5f;

    // how often we raycast for climb when not already climbing
    [Tooltip("Skip some frames between climb raycasts (1 = every frame).")]
    [SerializeField] private int climbCheckInterval = 2;

    [Header("Elite Visual")]
    [SerializeField] private Transform crown;

    private CharacterController controller;
    private Transform player;
    private PlayerHealth playerHealth;

    private float verticalVelocity;
    private bool isClimbing;
    private float climbStartY;
    private Vector3 knockbackVelocity;

    private Vector3 climbDir;   // locked direction while climbing
    private float blockedTimer; // how long we've been blocked
    private Vector3 lastPosition;
    private int climbCheckCounter;

    // for staggering far logic
    private int frameOffset;
    private int frameCounter;

    private float contactDamageCooldownTimer;

    public int PoolIndex { get; private set; } = -1;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        frameOffset = Random.Range(0, 8); // spread work

        if (crown == null)
        {
            Transform found = transform.Find("Crown");
            if (found != null) crown = found;
        }
        if (crown != null) crown.gameObject.SetActive(false);

        lastPosition = transform.position;
        blockedTimer = 0f;
        climbCheckCounter = 0;
    }

    private void OnEnable()
    {
        SREnemyManager.Instance?.Register(this);
        lastPosition = transform.position;
        blockedTimer = 0f;
        climbCheckCounter = 0;
    }

    private void OnDisable()
    {
        SREnemyManager.Instance?.Unregister(this);
    }

    public void Initialize(Transform playerTransform, bool isElite, int poolIndex)
    {
        player = playerTransform;
        PoolIndex = poolIndex;
        verticalVelocity = 0f;
        isClimbing = false;
        knockbackVelocity = Vector3.zero;
        contactDamageCooldownTimer = 0f;

        // cache player health for contact damage
        playerHealth = null;
        if (playerTransform != null)
        {
            playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();
        }

        if (crown != null)
            crown.gameObject.SetActive(isElite);
    }


    // Called by SREnemyManager. LOD controls how detailed we simulate this enemy.

    public void Tick(float dt, float distSq, EnemyLOD lod)
    {
        if (player == null) return;

        frameCounter++;

        if (lod == EnemyLOD.Far)
        {
            // VERY cheap far behavior: only every few frames and no CC/physics.
            int skipFrames = 8; // tweak: higher = cheaper
            if ((frameCounter + frameOffset) % skipFrames != 0)
                return;

            CheapFarMovement(dt);
            return;
        }

        // FULL LOGIC for closest enemies:

        // 1. direction to player (XZ)
        Vector3 toPlayer = player.position - transform.position;
        Vector3 moveDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (moveDir.sqrMagnitude > 0.0001f)
            moveDir.Normalize();

        // blocked detection
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            float forwardDistance = Vector3.Dot(delta, moveDir);
            float forwardSpeed = forwardDistance / dt;

            if (forwardSpeed < minForwardSpeed)
                blockedTimer += dt;
            else
                blockedTimer = 0f;
        }
        else
        {
            blockedTimer = 0f;
        }

        lastPosition = transform.position;

        // 2. climbing
        HandleClimb(moveDir);

        // 3. rotate
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, dt * turnSpeed);
        }

        // 4. gravity
        if (isClimbing)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, climbUpSpeed);
            verticalVelocity += gravity * climbGravityScale * dt;
        }
        else
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedGravity;
            else
                verticalVelocity += gravity * dt;
        }

        // 5. move –  includes knockbackVelocity
        Vector3 velocity = moveDir * moveSpeed + knockbackVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * dt);

        // decay knockback over time
        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity,
                Vector3.zero,
                knockbackDamping * dt);
        }

        // 6. contact damage to player (distance-based)
        if (contactDamageCooldownTimer > 0f)
            contactDamageCooldownTimer -= dt;

        if (playerHealth != null && contactDamageCooldownTimer <= 0f)
        {
            float contactRadiusSq = contactRadius * contactRadius;
            if (distSq <= contactRadiusSq)
            {
                playerHealth.TakeDamage(contactDamage);
                contactDamageCooldownTimer = contactDamageInterval;
            }
        }

        if (transform.position.y < -50f)
            Kill();
    }


    // Super cheap "slide toward player" for far enemies: no CC, no physics, no climbing.

    private void CheapFarMovement(float dt)
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        Vector3 dir = toPlayer.normalized;
        float farSpeed = moveSpeed * 0.4f;
        transform.position += dir * (farSpeed * dt);
    }

    private void HandleClimb(Vector3 moveDir)
    {
        // If we're not trying to move, do not climb.
        if (moveDir.sqrMagnitude < 0.0001f)
        {
            if (!isClimbing)
            {
                blockedTimer = 0f;
            }
            return;
        }

        // Already climbing: keep using locked climbDir.
        if (isClimbing)
        {
            if (climbDir.sqrMagnitude < 0.0001f)
            {
                climbDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;
            }

            float chestHeight = controller != null ? controller.height * 0.5f : 1f;
            Vector3 origin = transform.position + Vector3.up * chestHeight;

            bool hitObstacle = Physics.Raycast(
                origin,
                climbDir,
                out RaycastHit hitInfo,
                climbCheckDistance,
                climbObstacleMask,
                QueryTriggerInteraction.Collide);

            if (!hitObstacle)
            {
                Vector3 ahead = transform.position
                                + climbDir * controller.radius
                                + Vector3.up * (controller.height + 0.5f);

                if (Physics.Raycast(
                        ahead,
                        Vector3.down,
                        out RaycastHit downHit,
                        controller.height + 1f,
                        groundMask,
                        QueryTriggerInteraction.Collide))
                {
                    isClimbing = false;
                    return;
                }
            }

            if (transform.position.y > climbStartY + maxClimbHeight)
            {
                isClimbing = false;
            }

            return;
        }

        // CHECK IF WE'VE BEEN BLOCKED LONG ENOUGH 

        if (blockedTimer < blockedBeforeClimbTime)
        {
            return;
        }

        float chestH = controller != null ? controller.height * 0.5f : 1f;
        Vector3 start = transform.position + Vector3.up * chestH;
        Vector3 forward = moveDir;

        bool obstacleHit = Physics.Raycast(
            start,
            forward,
            out RaycastHit obstacleHitInfo,
            climbCheckDistance,
            climbObstacleMask,
            QueryTriggerInteraction.Collide);

        if (obstacleHit)
        {
            isClimbing = true;
            climbStartY = transform.position.y;
            climbDir = new Vector3(forward.x, 0f, forward.z).normalized;
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        // Add to current knockback; Tick will move & decay it.
        knockbackVelocity += force;
    }

    public void Kill()
    {
        // If this enemy came from a pool, return it there
        if (PoolIndex >= 0 && SREnemySpawner.Instance != null)
        {
            SREnemySpawner.Instance.DespawnEnemy(this, PoolIndex);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
