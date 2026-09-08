using Unity.Entities;
using Unity.Collections;

public partial struct LifeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<LifeTiem> lifeTime, Entity entity)
         in SystemAPI
         .Query<RefRW<LifeTiem>>()
         .WithEntityAccess())
        {
            lifeTime.ValueRW.Value -= SystemAPI.Time.DeltaTime;
            if (lifeTime.ValueRW.Value <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}