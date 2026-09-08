using Unity.Entities;
public struct EnemyTag:IComponentData
{
}
public struct PlayerTag:IComponentData
{
}
public struct DeadTag : IComponentData
{
}
public struct MoveTag : IComponentData
{
}
public struct Stuuned : IComponentData,IEnableableComponent
{
}
public struct Target :IComponentData
{
    public Entity Value;
}