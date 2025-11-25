//using UnityEngine;

//[RequireComponent(typeof(CharacterController))]
//[RequireComponent(typeof(CapsuleCollider))]
//public class SREnemy : MonoBehaviour
//{
//    [Header("Movement")]
//    [SerializeField] private float moveSpeed = 3f;
//    [SerializeField] private float turnSpeed = 10f;

//    [Header("Separation (Megabonk-ish blob)")]
//    [SerializeField] private float separationRadius = 0.8f;
//    [SerializeField] private float separationStrength = 2f;
//    [SerializeField] private LayerMask enemyLayer;

//    [Header("Gravity")]
//    [SerializeField] private float gravity = -25f;
//    [SerializeField] private float groundedGravity = -2f;

//    [Header("Elite Visual")]
//    [Tooltip("Optional crown object – will be enabled only on elite enemies.")]
//    [SerializeField] private Transform crown; // assign in inspector OR auto-detect

//    private CharacterController controller;
//    private Transform target;
//    private float verticalVelocity;
//    private SREnemySpawner spawner;
//    public int PoolIndex { get; private set; } = -1;

//    private void Awake()
//    {
//        controller = GetComponent<CharacterController>();

//        // We WANT collisions between enemies, so leave this as non-trigger.
//        var capsule = GetComponent<CapsuleCollider>();
//        capsule.isTrigger = false;

//        // Try to auto-find a child named "Crown" if not set.
//        if (crown == null)
//        {
//            Transform found = transform.Find("Crown");
//            if (found != null)
//            {
//                crown = found;
//            }
//        }

//        // Default off at start; spawner will toggle on elites.
//        if (crown != null)
//        {
//            crown.gameObject.SetActive(false);
//        }
//    }

//    public void Initialize(SREnemySpawner ownerSpawner, Transform targetToChase, bool isElite, int poolIndex)
//    {
//        spawner = ownerSpawner;
//        target = targetToChase;
//        PoolIndex = poolIndex;
//        verticalVelocity = 0f;

//        if (crown != null)
//        {
//            crown.gameObject.SetActive(isElite);
//        }

//        // Debug: uncomment this once to verify elites actually happen
//        // if (isElite) Debug.Log($"Spawned ELITE enemy: {name}");
//    }

//    private void Update()
//    {
//        if (target == null || controller == null)
//            return;

//        // --- Horizontal movement toward player ---
//        Vector3 toTarget = target.position - transform.position;
//        toTarget.y = 0f;

//        Vector3 moveDir = toTarget.sqrMagnitude > 0.01f
//            ? toTarget.normalized
//            : Vector3.zero;

//        // --- Separation ---
//        if (separationRadius > 0.01f)
//        {
//            Collider[] hits = Physics.OverlapSphere(
//                transform.position,
//                separationRadius,
//                enemyLayer,
//                QueryTriggerInteraction.Collide);

//            Vector3 separation = Vector3.zero;
//            int separationCount = 0;

//            foreach (Collider hit in hits)
//            {
//                if (hit.gameObject == gameObject)
//                    continue;

//                Vector3 offset = transform.position - hit.transform.position;
//                float distance = offset.magnitude;
//                if (distance < 0.0001f)
//                    continue;

//                separation += offset.normalized / distance;
//                separationCount++;
//            }

//            if (separationCount > 0)
//            {
//                separation /= separationCount;
//                moveDir += separation * separationStrength;
//            }
//        }

//        if (moveDir.sqrMagnitude > 1f)
//            moveDir.Normalize();

//        // --- Rotate toward movement direction ---
//        if (moveDir.sqrMagnitude > 0.001f)
//        {
//            Vector3 lookDir = moveDir;
//            lookDir.y = 0f;
//            if (lookDir.sqrMagnitude > 0.001f)
//            {
//                Quaternion targetRot = Quaternion.LookRotation(lookDir);
//                transform.rotation = Quaternion.Lerp(
//                    transform.rotation,
//                    targetRot,
//                    turnSpeed * Time.deltaTime);
//            }
//        }

//        // --- Gravity & ground ---
//        if (controller.isGrounded && verticalVelocity < 0f)
//        {
//            verticalVelocity = groundedGravity;
//        }
//        else
//        {
//            verticalVelocity += gravity * Time.deltaTime;
//        }

//        Vector3 velocity =
//            moveDir * moveSpeed +
//            Vector3.up * verticalVelocity;

//        controller.Move(velocity * Time.deltaTime);

//        if (transform.position.y < -50f)
//        {
//            Kill();
//        }
//    }

//    public void Kill()
//    {
//        if (spawner != null)
//        {
//            spawner.DespawnEnemy(this);
//        }
//        else
//        {
//            gameObject.SetActive(false);
//        }
//    }
//}
