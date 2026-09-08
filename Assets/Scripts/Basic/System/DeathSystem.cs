using Unity.Entities;
using Unity.Collections;

public partial struct DeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<Health> health, Entity entity)
         in SystemAPI.Query<RefRO<Health>>()
         .WithEntityAccess())
        {
            if (health.ValueRO.Value <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        foreach(var (lifetiem, entity)
            in SystemAPI
            .Query<RefRO<LifeTiem>>()
            .WithEntityAccess())
        {
            if (lifetiem.ValueRO.Value <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
