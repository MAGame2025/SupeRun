//using UnityEngine;

//public abstract class SREnemyBase : MonoBehaviour
//{
//    protected Transform player;

//    // Called when pulled from pool and spawned
//    public virtual void OnSpawn(Transform playerTransform)
//    {
//        player = playerTransform;
//        gameObject.SetActive(true);
//    }

//    // Called when returned to pool
//    public virtual void OnDespawn()
//    {
//        gameObject.SetActive(false);
//        player = null;
//    }

//    // Manager calls this when it’s this enemy’s turn to think
//    public abstract void TickEnemy(float deltaTime);

//    // Helper for death/despawn
//    public void Die()
//    {
//        if (SREnemyManager.Instance != null)
//            SREnemyManager.Instance.UnregisterEnemy(this);

//        if (SREnemyPool.Instance != null)
//            SREnemyPool.Instance.ReturnToPool(this);
//        else
//            OnDespawn();
//    }
//}
