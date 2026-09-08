using UnityEngine;
using Unity.Entities;
public sealed class SpawnAuthoring : MonoBehaviour
{
    [Min(0)]public int Count = 1000;
    public GameObject EnemyPrefab;
    private class Baker : Baker<SpawnAuthoring>
    {
        public override void Bake(SpawnAuthoring authoring)
        {
            var random = new Unity.Mathematics.Random(1000);
            Entity entity = GetEntity(TransformUsageFlags.None);
            Entity enemyPrefab =GetEntity(authoring.EnemyPrefab,
            TransformUsageFlags.Dynamic);
            AddComponent(entity, new Enemy
            {
                Count = authoring.Count,
                EnemyPrefab = enemyPrefab
            });
            AddComponent(entity, new MoveDirection
            {
               Value = random.NextFloat3Direction()
            });
        }
    }
}
