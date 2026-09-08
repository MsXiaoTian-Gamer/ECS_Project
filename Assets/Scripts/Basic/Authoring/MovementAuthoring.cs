using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
public sealed class MovementAuthoring : MonoBehaviour
{
    [Min(0f)] public float Value = 2f;
    private class Baker : Baker<MovementAuthoring>
    {
        public override void Bake(MovementAuthoring authoring)
        {
            
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MoveSpeed
            {
                Value = authoring.Value
            });
        }
        
    }
}
