using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class Enemy : MonoBehaviour
{
    public uint Id { get; private set; }
    public int SummonerId { get; private set; }
    public int WaveNumber { get; private set; }

    private EnemyBrain brain;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    // Called by EnemyController when spawning
    public void Initialize(uint id, EnemyData data, Transform player, int summonerId, int waveNumber)
    {
        Id = id;
        SummonerId = summonerId;
        WaveNumber = waveNumber;

        brain.Initialize(player, data.moveSpeed);
    }

    // Called by EnemyController.FixedUpdate
    public void MyFixedUpdate(float dt)
    {
        brain.MyFixedUpdate(dt);
    }
}
