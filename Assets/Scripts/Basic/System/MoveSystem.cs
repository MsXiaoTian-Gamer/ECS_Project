using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
[BurstCompile]
public partial struct MovementSystem: ISystem
{   
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach((RefRW<LocalTransform> transform,
                RefRO<MoveSpeed> moveSpeed,
                RefRO<MoveDirection> moveDirection) in
        SystemAPI.Query<RefRW<LocalTransform>
                    , RefRO<MoveSpeed>
                    , RefRO<MoveDirection>>()
        ){
            float3 normalizedDirection = math.normalize(
                moveDirection.ValueRO.Value
            );
            transform.ValueRW.Position+=normalizedDirection*moveSpeed.ValueRO.Value*deltaTime;
        }
    }  
}