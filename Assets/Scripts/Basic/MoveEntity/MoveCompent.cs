using Unity.Entities;
using Unity.Mathematics;
public struct MoveSpeed : IComponentData
{
    public float Value;
}
public struct MoveDirection : IComponentData
{
    public float3 Value;
}
public struct Enemy : IComponentData
{
    public int Count;
    public Entity EnemyPrefab;
    public float Value;
}
public struct LifeTiem : IComponentData
{
    public float Value;
}