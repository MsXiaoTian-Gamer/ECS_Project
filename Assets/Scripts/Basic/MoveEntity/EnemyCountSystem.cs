using Unity.Entities;
public partial struct EnemyCountSystem :ISystem
{
    private EntityQuery enemyQuery;
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
                    .WithAll<EnemyTag, Health>()
                    .WithNone<DeadTag>()
                    .Build();
    }
    public void OnUpdate(ref SystemState state)
    {
        int enemyCount = enemyQuery.CalculateEntityCount();
    }
}
public struct Health:IComponentData
{
    public int Value;
}