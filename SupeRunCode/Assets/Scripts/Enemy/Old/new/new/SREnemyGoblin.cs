//using UnityEngine;

//[RequireComponent(typeof(CapsuleCollider))]
//public class SREnemyGoblin : SREnemyBase
//{
//    [Header("Movement")]
//    [SerializeField] private float moveSpeed = 5f;

//    [Header("Vertical Motion")]
//    [SerializeField] private float gravity = -20f;
//    [SerializeField] private float maxFallSpeed = -30f;

//    [Header("Blocked / Climb")]
//    [Tooltip("Layers that count as blocking the enemy (ground, obstacles, player, other enemies).")]
//    [SerializeField] private LayerMask blockMask = ~0;

//    [Tooltip("How far forward to check for a blocking collider.")]
//    [SerializeField] private float blockCheckDistance = 1.0f;

//    [Tooltip("How long the enemy must be blocked before it starts climbing.")]
//    [SerializeField] private float timeBlockedBeforeClimb = 1.5f;

//    [Tooltip("Upwards climb speed once blocked long enough.")]
//    [SerializeField] private float climbSpeed = 4f;

//    [Header("Standing / Ground Check")]
//    [Tooltip("Layers considered as 'standing on' (ground, enemies, player, obstacles).")]
//    [SerializeField] private LayerMask standMask = ~0;

//    [Tooltip("Max distance below feet to snap to something to stand on.")]
//    [SerializeField] private float groundSnapDistance = 0.5f;

//    [Header("Separation / Push")]
//    [Tooltip("Radius to push away from other enemies / player when overlapping.")]
//    [SerializeField] private float separationRadius = 1.0f;

//    [Tooltip("How strong the self-separation effect is.")]
//    [SerializeField] private float separationStrength = 15f;

//    [Tooltip("Layers used for separation (should include Enemy + Player).")]
//    [SerializeField] private LayerMask entityMask;

//    [Tooltip("Horizontal impulse used to push the player via SRPlayerMotor.ApplyExternalPush.")]
//    [SerializeField] private float pushPlayerImpulse = 10f;

//    private CapsuleCollider capsule;
//    private SRPlayerMotor playerMotor;

//    private float blockedTimer;
//    private bool isClimbing;
//    private bool isBlockedForward;
//    private float verticalVelocity;

//    private void Awake()
//    {
//        capsule = GetComponent<CapsuleCollider>();
//        if (capsule == null)
//        {
//            Debug.LogWarning("SREnemyGoblin: CapsuleCollider required for correct feet position.");
//        }
//    }

//    public override void OnSpawn(Transform playerTransform)
//    {
//        base.OnSpawn(playerTransform);

//        playerMotor = null;
//        if (playerTransform != null)
//        {
//            // try root first, then children
//            playerMotor = playerTransform.GetComponent<SRPlayerMotor>();
//            if (playerMotor == null)
//                playerMotor = playerTransform.GetComponentInChildren<SRPlayerMotor>();
//        }

//        blockedTimer = 0f;
//        isClimbing = false;
//        isBlockedForward = false;
//        verticalVelocity = 0f;
//    }

//    public override void TickEnemy(float deltaTime)
//    {
//        if (player == null)
//            return;

//        // --- 1. Horizontal direction towards player ---
//        Vector3 toPlayer = player.position - transform.position;
//        toPlayer.y = 0f;
//        float dist = toPlayer.magnitude;
//        if (dist < 0.01f)
//            return;

//        Vector3 dirToPlayer = toPlayer / dist;

//        // --- 2. Update blocked / climb state (ONE forward check) ---
//        UpdateBlockedAndClimbState(dirToPlayer, deltaTime);

//        // --- 3. Base horizontal movement towards player ---
//        Vector3 horizontalMove = dirToPlayer * moveSpeed * deltaTime;

//        // If blocked and not climbing yet, do not move forward
//        if (isBlockedForward && !isClimbing)
//        {
//            horizontalMove = Vector3.zero;
//        }

//        // --- 4. Separation and pushing (enemies + player) ---
//        horizontalMove += ComputeSeparationAndPush(deltaTime);

//        // --- 5. Vertical movement (climb vs gravity) ---
//        UpdateVertical(deltaTime);

//        // --- 6. Combine movement ---
//        Vector3 totalMove = horizontalMove + Vector3.up * (verticalVelocity * deltaTime);

//        // --- 7. Rotate towards horizontal movement ---
//        Vector3 flatMove = new Vector3(horizontalMove.x, 0f, horizontalMove.z);
//        if (flatMove.sqrMagnitude > 0.0001f)
//        {
//            transform.rotation = Quaternion.LookRotation(flatMove.normalized, Vector3.up);
//        }

//        // --- 8. Apply movement ---
//        transform.position += totalMove;

//        // --- 9. Snap down if we’re falling and near something to stand on ---
//        GroundSnapIfNeeded();
//    }

//    private void UpdateBlockedAndClimbState(Vector3 dirToTarget, float dt)
//    {
//        Vector3 bottom = GetBottomPosition();
//        Vector3 origin = bottom + Vector3.up * 0.1f;

//        Vector3 flatDir = new Vector3(dirToTarget.x, 0f, dirToTarget.z).normalized;
//        if (flatDir.sqrMagnitude < 0.0001f)
//        {
//            isBlockedForward = false;
//            blockedTimer = 0f;
//            isClimbing = false;
//            return;
//        }

//        // Single forward check to see if there is something directly in front
//        bool blocked = Physics.Raycast(
//            origin,
//            flatDir,
//            blockCheckDistance,
//            blockMask,
//            QueryTriggerInteraction.Ignore
//        );

//        isBlockedForward = blocked;

//        if (blocked)
//        {
//            blockedTimer += dt;
//        }
//        else
//        {
//            blockedTimer = 0f;
//            if (isClimbing)
//            {
//                // No longer blocked -> stop climbing, let gravity handle it
//                isClimbing = false;
//            }
//        }

//        if (blockedTimer >= timeBlockedBeforeClimb)
//        {
//            isClimbing = true;
//        }
//    }

//    private void UpdateVertical(float dt)
//    {
//        if (isClimbing)
//        {
//            verticalVelocity = climbSpeed;
//        }
//        else
//        {
//            verticalVelocity += gravity * dt;
//            if (verticalVelocity < maxFallSpeed)
//                verticalVelocity = maxFallSpeed;
//        }
//    }

//    private void GroundSnapIfNeeded()
//    {
//        if (isClimbing || verticalVelocity > 0f)
//            return;

//        Vector3 bottom = GetBottomPosition();
//        Vector3 origin = bottom + Vector3.up * 0.1f;

//        if (Physics.Raycast(
//            origin,
//            Vector3.down,
//            out RaycastHit hit,
//            groundSnapDistance + 0.1f,
//            standMask,
//            QueryTriggerInteraction.Ignore))
//        {
//            float newY = hit.point.y;
//            Vector3 pos = transform.position;

//            float bottomOffset = pos.y - bottom.y;
//            pos.y = newY + bottomOffset;
//            transform.position = pos;

//            verticalVelocity = 0f;
//        }
//    }

//    private Vector3 GetBottomPosition()
//    {
//        if (capsule != null)
//        {
//            float bottomOffset = -capsule.height * 0.5f + capsule.radius;
//            return transform.position + Vector3.up * bottomOffset;
//        }

//        return transform.position;
//    }

//    private Vector3 ComputeSeparationAndPush(float dt)
//    {
//        if (separationRadius <= 0f)
//            return Vector3.zero;

//        Vector3 separation = Vector3.zero;

//        Collider[] hits = Physics.OverlapSphere(
//            transform.position,
//            separationRadius,
//            entityMask,
//            QueryTriggerInteraction.Ignore);

//        foreach (var col in hits)
//        {
//            if (col == null)
//                continue;

//            Transform otherT = col.transform;
//            if (otherT == transform)
//                continue;

//            Vector3 diff = transform.position - otherT.position;
//            diff.y = 0f;
//            float d = diff.magnitude;
//            if (d < 0.0001f)
//                continue;

//            float overlap = separationRadius - d;
//            if (overlap <= 0f)
//                continue;

//            // diff points from other -> this
//            Vector3 away = diff / d;

//            // Push player away from us
//            if (player != null && otherT == player && playerMotor != null)
//            {
//                Vector3 pushDir = -away; // from us -> player
//                playerMotor.ApplyExternalPush(pushDir * pushPlayerImpulse);
//            }

//            // Self separation: move ourselves away
//            separation += away * overlap;
//        }

//        if (separation.sqrMagnitude > 0f)
//        {
//            separation = separation.normalized * (separationStrength * dt);
//        }

//        return separation;
//    }

//    private void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.yellow;
//        Gizmos.DrawWireSphere(transform.position, separationRadius);
//    }
//}
