public interface ISREnemyVisual
{
    void OnSpawn();
    void SetMoveSpeed(float speed);
    void PlayHit();
    void PlayDeath();
    void SetFullLod(bool fullLod);
}
