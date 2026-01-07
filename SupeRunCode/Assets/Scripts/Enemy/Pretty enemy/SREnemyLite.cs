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

    [Header("Slope Follow (Full LOD)")]
    [Tooltip("If enabled, Full enemies follow slopes by projecting their move direction on the ground plane.")]
    [SerializeField] private bool enableSlopeFollow = true;

    [Tooltip("How far down we probe to find ground normal.")]
    [SerializeField] private float slopeProbeDistance = 1.8f;

    [Header("Hop Obstacle Probe")]
    [SerializeField] private float obstacleProbeDistance = 0.6f;

    [Tooltip("Probe starts at position + up * this height.")]
    [SerializeField] private float slopeProbeStartHeight = 0.8f;

    [Tooltip("Only treat it as a slope above this angle.")]
    [SerializeField] private float minSlopeAngle = 3f;

    [Tooltip("Update slope probe every N frames (1 = every frame).")]
    [SerializeField] private int slopeProbeIntervalFrames = 2;

    [Header("Uphill Hop (Full LOD)")]
    [Tooltip("If blocked while moving uphill for this long, do a small hop.")]
    [SerializeField] private float blockedBeforeHopTime = 0.20f;

    [Tooltip("Upward velocity when hopping uphill.")]
    [SerializeField] private float hopUpVelocity = 4.5f;

    [Tooltip("Minimum time between hops.")]
    [SerializeField] private float hopCooldown = 0.25f;

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

    [Header("Far LOD (cheap but non-phasing)")]
    [Tooltip("Far enemies: move speed multiplier.")]
    [SerializeField] private float farSpeedMultiplier = 0.4f;

    [Tooltip("Far enemies: sphere radius for obstacle check (prevents phasing).")]
    [SerializeField] private float farObstacleRadius = 0.35f;

    [Tooltip("Far enemies: cast origin height above position.")]
    [SerializeField] private float farObstacleCastHeight = 0.8f;

    [Tooltip("Far enemies: cast down from this height to snap to ground.")]
    [SerializeField] private float farGroundSnapCastHeight = 2.0f;

    [Tooltip("Far enemies: max distance for ground snap raycast.")]
    [SerializeField] private float farGroundSnapDistance = 10f;

    [Tooltip("Far enemies: small offset above ground to avoid clipping.")]
    [SerializeField] private float farGroundOffset = 0.05f;

    [SerializeField] private float hopSpawnGraceTime = 0.5f;

    [Tooltip("If slope is steeper than this, allow uphill hop even without a forward obstacle hit.")]
    [SerializeField] private float steepSlopeAngle = 20f;

    [Header("Spawn Height")]
    [SerializeField] private float spawnHeightOffset = 0.6f;

    [Header("Elite Visual")]
    [SerializeField] private Transform crown;

    // ==========================
    // ✅ DEBUG (ADDED ONLY)
    // ==========================
    [Header("Debug")]
    [SerializeField] private bool debugThisEnemy = false;
    [SerializeField] private float debugLastDeltaY;
    [SerializeField] private bool debugIsClimbing;
    [SerializeField] private float debugVerticalVelocity;
    [SerializeField] private float debugSlopeAngle;
    [SerializeField] private string debugLastClimbHitName;
    [SerializeField] private int debugLastClimbHitLayer;
    [SerializeField] private string debugLastSlopeHitName;
    [SerializeField] private int debugLastSlopeHitLayer;

    private float debugLastY;
    private float debugNextLogTime;

    private CharacterController controller;
    private Transform player;
    private PlayerHealth playerHealth;

    private float spawnTime;
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

    // ---- slope cache (kept cheap) ----
    private int slopeProbeCounter;
    private bool hasGround;
    private Vector3 groundNormalCached = Vector3.up;
    private float slopeAngleCached;
    private Vector3 downSlopeCached = Vector3.zero;
    private float nextHopTime;

    // ---- Compatibility with your newer manager/spawner ----
    public int PoolIndex { get; private set; } = -1;
    public Vector3 Position => transform.position;
    public int FrameOffset => frameOffset;

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

        slopeProbeCounter = 0;
        hasGround = false;
        groundNormalCached = Vector3.up;
        slopeAngleCached = 0f;
        downSlopeCached = Vector3.zero;
        nextHopTime = 0f;

        // DEBUG init (ADDED ONLY)
        debugLastY = transform.position.y;
        debugNextLogTime = 0f;
        debugLastDeltaY = 0f;
        debugIsClimbing = false;
        debugVerticalVelocity = 0f;
        debugSlopeAngle = 0f;
        debugLastClimbHitName = "";
        debugLastClimbHitLayer = -1;
        debugLastSlopeHitName = "";
        debugLastSlopeHitLayer = -1;
    }

    private void OnEnable()
    {
        SREnemyManager.Instance?.Register(this);

        lastPosition = transform.position;
        blockedTimer = 0f;
        climbCheckCounter = 0;
        spawnTime = Time.time;

        slopeProbeCounter = 0;
        hasGround = false;
        groundNormalCached = Vector3.up;
        slopeAngleCached = 0f;
        downSlopeCached = Vector3.zero;
        nextHopTime = 0f;

        // DEBUG init (ADDED ONLY)
        debugLastY = transform.position.y;
        debugNextLogTime = 0f;
        debugLastDeltaY = 0f;
        debugIsClimbing = false;
        debugVerticalVelocity = 0f;
        debugSlopeAngle = 0f;
        debugLastClimbHitName = "";
        debugLastClimbHitLayer = -1;
        debugLastSlopeHitName = "";
        debugLastSlopeHitLayer = -1;
    }

    private void OnDisable()
    {
        SREnemyManager.Instance?.Unregister(this);
    }

    public void Initialize(Transform playerTransform, bool isElite, int poolIndex)
    {
        player = playerTransform;
        PoolIndex = poolIndex;
        spawnTime = Time.time;

        verticalVelocity = 0f;
        isClimbing = false;
        knockbackVelocity = Vector3.zero;
        contactDamageCooldownTimer = 0f;

        blockedTimer = 0f;
        nextHopTime = 0f;

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

    // New manager calls this. Old manager won’t.
    public void ApplyLODState(EnemyLOD lod, bool disableComponentsWhenNotFull)
    {
        // We intentionally do not disable CharacterController here by default,
        // because you explicitly want far enemies to not phase through things.
        // If you later add an Animator and want to disable it, you can do it here.
        // Keeping this method ensures compatibility and prevents compile errors.
    }

    // Spawner calls this.
    public void ResetHealthIfAny()
    {
        // Keep compatibility without knowing your exact health component:
        gameObject.SendMessage("ResetHealth", SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("Reset", SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("Respawn", SendMessageOptions.DontRequireReceiver);
    }

    // Manager can call this for silent despawn.
    public void DespawnWithoutRewards()
    {
        if (PoolIndex >= 0 && SREnemySpawner.Instance != null)
        {
            SREnemySpawner.Instance.DespawnEnemy(this, PoolIndex);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Called by SREnemyManager. LOD controls how detailed we simulate this enemy.
    public void Tick(float dt, float distSq, EnemyLOD lod)
    {
        if (player == null) return;

        frameCounter++;

        if (lod == EnemyLOD.Far)
        {

            // FAR LOD rotation (restores facing player even when we early-return)
            Vector3 toPlayerFar = player.position - transform.position;
            toPlayerFar.y = 0f;
            if (toPlayerFar.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredDirFar = toPlayerFar.normalized;
                Quaternion targetRotFar = Quaternion.LookRotation(desiredDirFar, Vector3.up);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotFar, dt * turnSpeed);
            }

            // Your new manager already throttles far ticks.
            // Keep THIS cheap and deterministic: float in XZ, snap to ground, don’t phase through walls.
            CheapFarMovement_NoPhase(dt);
            return;
        }

        // FULL LOGIC for closest enemies:

        // 1) direction to player (XZ)
        Vector3 toPlayer = player.position - transform.position;
        Vector3 desiredDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (desiredDir.sqrMagnitude > 0.0001f)
            desiredDir.Normalize();

        // blocked detection (same as your original, but using desiredDir)
        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;

            float forwardDistance = Vector3.Dot(delta, desiredDir);
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

        // 2) climbing (your original behavior)
        HandleClimb(desiredDir);

        // 3) rotate (face player intent)
        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, dt * turnSpeed);
        }

        // 4) slope cache update (only when not climbing)
        if (enableSlopeFollow && !isClimbing)
        {
            UpdateSlopeCache();
        }
        else
        {
            hasGround = false;
            groundNormalCached = Vector3.up;
            slopeAngleCached = 0f;
            downSlopeCached = Vector3.zero;
        }

        // 5) compute movement direction (project on slope plane if valid)
        Vector3 moveDir = desiredDir;

        if (enableSlopeFollow && hasGround && slopeAngleCached > minSlopeAngle && !isClimbing)
        {
            Vector3 projected = Vector3.ProjectOnPlane(desiredDir, groundNormalCached);
            if (projected.sqrMagnitude > 0.0001f)
                moveDir = projected.normalized;
        }

        // 6) uphill hop when blocked (mostly against the slope itself)
        if (enableSlopeFollow && hasGround && !isClimbing)
        {
            TryUphillHop(desiredDir);
        }

        // 7) gravity (your original behavior)
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

        // 8) move – includes knockbackVelocity
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

        // 9) contact damage to player (distance-based)
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

        // ==========================
        // DEBUG: detect "flying up"
        // ==========================
        if (debugThisEnemy)
        {
            float y = transform.position.y;
            debugLastDeltaY = y - debugLastY;
            debugLastY = y;

            debugIsClimbing = isClimbing;
            debugVerticalVelocity = verticalVelocity;
            debugSlopeAngle = slopeAngleCached;

            if (debugLastDeltaY > 0.05f && Time.time >= debugNextLogTime)
            {
                Debug.Log(
                    $"[EnemyFlyDebug] {name} dY={debugLastDeltaY:F3} vv={verticalVelocity:F2} " +
                    $"climb={isClimbing} slope={slopeAngleCached:F1} hasGround={hasGround} grounded={controller.isGrounded} " +
                    $"climbHit='{debugLastClimbHitName}'(L{debugLastClimbHitLayer}) " +
                    $"slopeHit='{debugLastSlopeHitName}'(L{debugLastSlopeHitLayer})"
                );

                debugNextLogTime = Time.time + 0.25f;
            }
        }

        if (transform.position.y < -50f)
            Kill();
    }

    private void UpdateSlopeCache()
    {
        slopeProbeCounter++;

        int interval = slopeProbeIntervalFrames;
        if (interval < 1) interval = 1;

        if (slopeProbeCounter % interval != 0 && hasGround)
            return;

        // Probe down to find ground normal
        Vector3 start = transform.position + Vector3.up * slopeProbeStartHeight;

        float radius = controller != null ? Mathf.Max(0.05f, controller.radius * 0.75f) : 0.25f;

        bool gotValidGround = false;
        RaycastHit hit = default;

        // Try up to 2 times:
        // - First cast normally.
        // - If we hit an enemy, shift up slightly and try again.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (Physics.SphereCast(
                start,
                radius,
                Vector3.down,
                out hit,
                slopeProbeDistance,
                groundMask,
                QueryTriggerInteraction.Collide))
            {
                if (IsGroundHitValid(hit))
                {
                    gotValidGround = true;
                    break;
                }

                // Hit an enemy/CC - try again slightly higher to "see past" it
                start += Vector3.up * 0.35f;
                continue;
            }

            break;
        }

        if (gotValidGround)
        {
            hasGround = true;
            groundNormalCached = hit.normal;
            slopeAngleCached = Vector3.Angle(groundNormalCached, Vector3.up);

            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormalCached);
            if (downSlope.sqrMagnitude > 0.0001f)
                downSlopeCached = downSlope.normalized;
            else
                downSlopeCached = Vector3.zero;

            // DEBUG record (keeps your debug behavior)
            if (debugThisEnemy)
            {
                debugLastSlopeHitName = hit.collider != null ? hit.collider.name : "";
                debugLastSlopeHitLayer = hit.collider != null ? hit.collider.gameObject.layer : -1;
            }
        }
        else
        {
            hasGround = false;
            groundNormalCached = Vector3.up;
            slopeAngleCached = 0f;
            downSlopeCached = Vector3.zero;
        }
    }

    private void TryUphillHop(Vector3 desiredDir)
    {
        // --- NEW: don't allow hop right after spawn ---
        if (Time.time < spawnTime + hopSpawnGraceTime)
            return;

        if (!hasGround) return;

        // --- NEW: hop only when actually grounded ---
        if (controller == null || !controller.isGrounded)
            return;

        // --- NEW: don't hop if already moving upward (prevents "rocket" behavior) ---
        if (verticalVelocity > 0.25f)
            return;

        if (Time.time < nextHopTime) return;
        if (slopeAngleCached <= minSlopeAngle) return;

        // Are we trying to move uphill?
        if (downSlopeCached.sqrMagnitude < 0.0001f) return;

        Vector3 uphillFlat = -downSlopeCached;
        uphillFlat.y = 0f;
        if (uphillFlat.sqrMagnitude > 0.0001f)
            uphillFlat.Normalize();
        else
            return;

        float uphillDot = Vector3.Dot(desiredDir, uphillFlat);
        if (uphillDot <= 0.25f) return;

        // Must be blocked long enough
        if (blockedTimer < blockedBeforeHopTime) return;

        // --- IMPORTANT CHANGE: require either an obstacle ahead OR a genuinely steep slope ---
        // This prevents hop-spam on mild slope seams / bad normals.
        bool obstacleAhead = IsObstacleAhead(desiredDir);
        bool steepEnough = slopeAngleCached >= steepSlopeAngle;

        if (!obstacleAhead && !steepEnough)
            return;

        if (verticalVelocity < hopUpVelocity)
            verticalVelocity = hopUpVelocity;

        // tiny nudge prevents immediate re-stick
        controller.Move(Vector3.up * 0.05f);

        nextHopTime = Time.time + hopCooldown;
        blockedTimer = 0f;
    }

    // Far movement: float toward player, but:
    // - snap to ground
    // - spherecast to avoid phasing through obstacles
    private void CheapFarMovement_NoPhase(float dt)
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        Vector3 dir = toPlayer.normalized;
        float farSpeed = moveSpeed * farSpeedMultiplier;

        Vector3 startPos = transform.position;
        Vector3 desiredStep = dir * (farSpeed * dt);
        Vector3 desiredPos = startPos + desiredStep;

        float castDist = desiredStep.magnitude;
        if (castDist > 0.0001f)
        {
            Vector3 castOrigin = startPos + Vector3.up * farObstacleCastHeight;

            if (Physics.SphereCast(
                castOrigin,
                farObstacleRadius,
                dir,
                out RaycastHit hit,
                castDist,
                climbObstacleMask,
                QueryTriggerInteraction.Collide))
            {
                // slide along obstacle
                Vector3 slideDir = Vector3.ProjectOnPlane(dir, hit.normal);
                if (slideDir.sqrMagnitude > 0.0001f)
                {
                    slideDir.Normalize();
                    desiredPos = startPos + slideDir * castDist;
                }
                else
                {
                    desiredPos = startPos;
                }
            }
        }
        // snap to ground (never below terrain)
        Vector3 groundCastOrigin = desiredPos + Vector3.up * farGroundSnapCastHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            groundCastOrigin,
            Vector3.down,
            farGroundSnapDistance,
            groundMask,
            QueryTriggerInteraction.Collide
        );

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                // ignore our own colliders
                if (hits[i].collider != null && hits[i].collider.transform.IsChildOf(transform))
                    continue;

                // also ignore enemy/CC hits using your existing filter
                if (!IsGroundHitValid(hits[i]))
                    continue;

                desiredPos.y = hits[i].point.y + farGroundOffset;
                break;
            }
        }


        transform.position = desiredPos;
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

            // DEBUG record (ADDED ONLY)
            if (debugThisEnemy && hitObstacle)
            {
                debugLastClimbHitName = hitInfo.collider != null ? hitInfo.collider.name : "";
                debugLastClimbHitLayer = hitInfo.collider != null ? hitInfo.collider.gameObject.layer : -1;
            }

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

        climbCheckCounter++;
        int interval = climbCheckInterval;
        if (interval < 1) interval = 1;

        if (climbCheckCounter % interval != 0)
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

        // DEBUG record (ADDED ONLY)
        if (debugThisEnemy && obstacleHit)
        {
            debugLastClimbHitName = obstacleHitInfo.collider != null ? obstacleHitInfo.collider.name : "";
            debugLastClimbHitLayer = obstacleHitInfo.collider != null ? obstacleHitInfo.collider.gameObject.layer : -1;
        }

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
        SRGameEvents.RaiseEnemyKilled();
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

    private bool IsObstacleAhead(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return false;

        float chestH = controller != null ? controller.height * 0.5f : 1f;
        Vector3 origin = transform.position + Vector3.up * chestH;

        return Physics.Raycast(
            origin,
            dir,
            obstacleProbeDistance,
            climbObstacleMask,
            QueryTriggerInteraction.Collide);
    }

    private bool IsGroundHitValid(RaycastHit hit)
    {
        if (hit.collider == null) return false;

        // If we hit another enemy (or our own hierarchy), this is NOT ground.
        // This prevents slope probes from treating enemy capsules as terrain.
        SREnemyLite enemy = hit.collider.GetComponentInParent<SREnemyLite>();
        if (enemy != null)
            return false;


        // Also reject any CharacterController that isn't ours (extra safety)
        CharacterController cc = hit.collider.GetComponentInParent<CharacterController>();
        if (cc != null)
            return false;


        return true;
    }

}
