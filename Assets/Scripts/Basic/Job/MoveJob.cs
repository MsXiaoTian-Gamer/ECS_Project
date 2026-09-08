using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial struct MoveJob : IJobEntity
{
    public float deltaTime;

    public void Execute(
        ref LocalTransform transform,
        in MoveSpeed moveSpeed,
        in MoveDirection moveDirection)
    {
        float3 normalizedDirection = math.normalize(moveDirection.Value);
        transform.Position += normalizedDirection * moveSpeed.Value * deltaTime;
    }
}

[BurstCompile]
public partial struct JobMoveSystem : ISystem
{
    private bool hasSpawned;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!hasSpawned)
        {
            hasSpawned = true;
            var random = new Unity.Mathematics.Random(1000);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var enemy in SystemAPI.Query<RefRO<Enemy>>())
            {
                for (int i = 0; i < 100; i++)
                {
                    var spawned = ecb.Instantiate(enemy.ValueRO.EnemyPrefab);
                    ecb.SetComponent(spawned, LocalTransform.FromPosition(
                        new float3(0, 0, 0)));
                    ecb.SetComponent(spawned, new MoveDirection
                    {
                        Value = random.NextFloat3Direction()
                    });
                }
                    
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        MoveJob moveJob = new MoveJob
        {
            deltaTime = SystemAPI.Time.DeltaTime
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}
