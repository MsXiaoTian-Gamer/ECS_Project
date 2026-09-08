using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

public partial struct SpawnSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;

        // 建 Mesh 和 Material
        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
            new(-0.5f, -0.5f,  0.5f), new(0.5f, -0.5f,  0.5f), new(0.5f, 0.5f,  0.5f), new(-0.5f, 0.5f,  0.5f)
        };
        mesh.triangles = new int[]
        {
            0,2,1, 0,3,2,   4,5,6, 4,6,7,
            4,7,3, 4,3,0,   1,2,6, 1,6,5,
            3,7,6, 3,6,2,   0,1,5, 0,5,4
        };
        mesh.RecalculateNormals();



        var material = new Material(Shader.Find("Standard"));
        material.color = Color.green;

        var desc = new RenderMeshDescription(ShadowCastingMode.On);
        var meshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
        var archetype = state.EntityManager.CreateArchetype(
            ComponentType.ReadWrite<MoveSpeed>(),
            ComponentType.ReadWrite<MoveDirection>(),
            ComponentType.ReadWrite<EnemyTag>(),
            ComponentType.ReadWrite<MoveTag>(),
            ComponentType.ReadWrite<LifeTiem>(),
            typeof(LocalTransform),
            typeof(LocalToWorld)
        );

        var entityArray = state.EntityManager.CreateEntity(archetype, 100, Allocator.Temp);
        var random = new Random(1234);

        for (int i = 0; i < entityArray.Length; i++)
        {
            state.EntityManager.SetComponentData(entityArray[i], new MoveSpeed
                { Value = random.NextFloat(1f, 5f) });
            state.EntityManager.SetComponentData(entityArray[i], new MoveDirection
                { Value = random.NextFloat3Direction() });
            state.EntityManager.SetComponentData(entityArray[i], LocalTransform.FromPosition(
                random.NextFloat3(new float3(-20, -10, -20), new float3(20, 10, 20))));
            state.EntityManager.SetComponentData(entityArray[i],
            new LifeTiem{Value = random.NextFloat(10f, 50f)});
            // 用 RenderMeshUtility 添加渲染组件
            RenderMeshUtility.AddComponents(
                entityArray[i],
                state.EntityManager,
                desc,
                meshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
        }
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (enemy,entity) in 
                SystemAPI.Query<RefRO<Enemy>>().
                WithEntityAccess())
        {
            for (int i = 0; i < enemy.ValueRO.Count; i++)
            {
                var spawned = ecb.Instantiate(enemy.ValueRO.EnemyPrefab);
                ecb.SetComponent(spawned, LocalTransform.FromPosition(
                    new float3(0, 0, 0)));
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        entityArray.Dispose();
    }
}
