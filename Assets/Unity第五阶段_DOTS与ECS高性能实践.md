# Unity 第五阶段：DOTS / ECS 与高性能数据导向实践

这份学习计划适合已经完成第三阶段的项目架构与工程化训练，并正在学习或已经完成第四阶段部分内容的人。

你现在应该已经具备这些基础：

- 能独立完成一个中小型 Unity 功能模块
- 能区分配置数据、运行时数据和持久化数据
- 理解模块边界、依赖方向和数据驱动设计
- 熟悉泛型、接口、委托、事件与常用集合
- 使用过对象池、状态机或行为树等系统
- 知道优化前要先使用 Profiler 定位问题

第五阶段不再以“一个 GameObject 对应一个脚本”为唯一视角，而是开始学习：

- 如何按数据布局和访问方式设计运行时逻辑
- 如何批量处理大量同构对象
- 如何让代码适合 Burst 编译和多线程执行
- 如何用 Entity、Component、System 表达游戏模拟
- 如何在 GameObject 与 ECS 之间划清合理边界
- 如何通过测量证明 DOTS 是否真的解决了性能问题

核心目标：

> 从“能设计可维护的传统 Unity 项目”，进步到“能针对大规模模拟设计、实现并验证高性能混合架构”。

---

## 一、第五阶段应该优先学什么

建议按以下顺序学习：

1. 数据导向设计与 ECS 核心思维
2. Entity、Component、System 与 EntityQuery
3. Authoring、Baker、Entity Prefab 与 SubScene
4. 实体生命周期与结构变化
5. EntityCommandBuffer 与 Enableable Component
6. Jobs、Burst 和线程安全
7. Dynamic Buffer、Blob Asset 与数据建模
8. 系统分组、更新顺序与系统间通信
9. Entities Profiler 与性能验证
10. GameObject / ECS 混合架构

不建议一开始就冲这些内容：

- Netcode for Entities
- Entities Graphics 深度定制
- 大型开放世界流式加载
- 完整 ECS 行为树框架
- 完整 ECS 动画框架
- 自己封装一套“万能 ECS 框架”
- 把现有项目全面重构成 ECS

第五阶段首先解决的是“如何正确地设计和使用 ECS”，而不是“如何把所有 Unity 功能都塞进 ECS”。

---

## 二、环境与版本原则

DOTS 相关 API 经历过较大变化，学习时必须先固定版本。

建议使用：

- 一个受支持的 Unity LTS 或稳定版本
- 与该 Unity 版本兼容的 Entities 1.x
- Burst
- Collections
- Mathematics
- Entities Graphics（需要渲染大量实体时再安装）

通过 Package Manager 安装并确认版本，不要把不同年代教程里的 API 混在同一个项目中。

### 现代教程中常见的 API

- `ISystem`
- `SystemAPI.Query`
- `IJobEntity`
- `Baker<TAuthoring>`
- `LocalTransform`
- `EntityCommandBuffer`
- `ComponentLookup<T>`
- `BufferLookup<T>`
- `IEnableableComponent`

### 看到这些 API 时要检查教程年代

- `ComponentSystem`
- `JobComponentSystem`
- `Entities.ForEach`
- `IJobForEach`
- `Translation`
- `Rotation`
- `[GenerateAuthoringComponent]`
- 旧式 Conversion Workflow

这些名称并不代表知识完全无效，但代码通常不能直接复制到现代 Entities 1.x 项目。

### 建议建立独立练习项目

不要在现有主项目中直接安装 DOTS 并重构。新建一个独立项目，例如：

```text
DotsLearning/
  Assets/
    DotsLearning/
      01_Basics/
      02_Baking/
      03_StructuralChanges/
      04_JobsBurst/
      05_AdvancedData/
      06_FinalProject/
```

每个目录都对应一个可以独立运行的 Sample Scene，避免后续练习相互污染。

---

## 三、先理解数据导向，而不是先背 API

### 3.1 面向对象与数据导向关注点不同

传统 MonoBehaviour 常把数据和行为放在一个对象中：

```csharp
using UnityEngine;

public sealed class Enemy : MonoBehaviour
{
    // SerializeField 使私有字段可在 Inspector 中配置，同时避免对外暴露写权限。
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private int health = 100;

    // 每个 Enemy GameObject 每帧都会被 Unity 单独调用一次 Update。
    private void Update()
    {
        // transform.forward 是当前物体的世界前方；乘 DeltaTime 使速度与帧率无关。
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }
}
```

这段代码用于少量独立对象时很自然。但是当场上有几万名单位时，CPU 需要在大量对象之间跳转，逐个调用生命周期方法并访问分散的数据。

ECS 会将问题重新表达为：

```text
移动需要哪些数据？
  LocalTransform
  MoveSpeed
  MoveDirection

哪些实体应该移动？
  同时拥有以上数据的所有实体

谁负责移动？
  MovementSystem
```

其关键不是把 `Enemy` 改名成 `EnemyComponent`，而是围绕“数据集合”和“批量处理”组织程序。

### 3.2 ECS 三个核心角色

#### Entity

Entity 可以理解为一个带版本信息的身份标识。它不是 GameObject，也不是承载大量方法的传统对象。

Entity 自身不说明它是敌人、子弹还是建筑。它拥有哪些 Component，决定它当前具有什么数据和能力。

#### Component

Component 主要保存数据：

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct MoveSpeed : IComponentData
{
    // IComponentData 仅保存运行时数据；行为由 System 实现。
    public float Value;
}

public struct MoveDirection : IComponentData
{
    // float3 是 Unity.Mathematics 的非托管向量，适合 Burst 和 Job。
    public float3 Value;
}

public struct Health : IComponentData
{
    // Current 是频繁变化的实例状态，Maximum 是该实体当前的上限。
    public int Current;
    public int Maximum;
}
```

学习初期应遵守：

- Component 尽量只保存数据
- 不在 Component 中保存 `GameObject`、普通 C# 对象或任意托管引用
- 不把所有数据塞进一个巨大的组件
- 也不要为了“纯粹”把每个字段都拆成一个组件
- 按访问频率和系统需求划分数据

#### System

System 查询拥有特定组件组合的实体，然后批量处理数据。

```text
MovementSystem
  查询：LocalTransform + MoveSpeed + MoveDirection

DamageSystem
  查询：Health + DamageEvent

DeathSystem
  查询：Health，其中 Current <= 0
```

System 不是传统意义上的“全局 Manager”。System 更像一条数据处理流水线中的一个阶段。

### 3.3 Archetype 与 Chunk

拥有相同组件类型组合的实体属于相同 Archetype。

```text
Archetype A
  LocalTransform + MoveSpeed

Archetype B
  LocalTransform + MoveSpeed + Health

Archetype C
  LocalTransform + Health + EnemyTag
```

同一 Archetype 的实体数据会按 Chunk 组织。系统可以连续处理同类型数据，这有利于 CPU 缓存和批量执行。

这里必须理解一个重要结论：

> 添加或移除组件不仅是修改一个字段，还可能让实体从一个 Archetype 移动到另一个 Archetype。

这类操作称为 Structural Change（结构变化）。后面学习 `EntityCommandBuffer` 时会再次使用这个概念。

### 3.4 第一阶段练习：只做数据建模

选择三个熟悉的对象，不写完整代码，只设计 ECS 数据：

1. 子弹
2. 敌人
3. Buff

例如子弹可以拆成：

```text
Bullet Entity
  LocalTransform
  MoveSpeed
  MoveDirection
  Damage
  Lifetime
  Owner
```

```text
Enemy Entity 
  LocalTransform
  MoveSpeed
  MoveDirection
  Health
  EnemyTag
  AttackState
  AttackDamage
```

```text
Buff Entity
  BuffId
  RemainingTime
  StackCount
  Magnitude
  BuffTag
```

然后回答：

- 哪些数据每帧都会读取？
- 哪些数据只在碰撞或命中时读取？
- 哪些数据是配置？
- 哪些数据是实例状态？
- 哪些数据可以共享？
- 哪些行为会造成结构变化？

### 本节验收标准

- 能用自己的话解释 Entity、Component、System
- 能解释 Archetype 为什么与组件组合有关
- 能解释 Structural Change 为什么不是普通字段赋值
- 能把一个传统 MonoBehaviour 拆成数据和处理流程
- 不再把 Entity 理解为“更快的 GameObject”

---

## 四、ECS 基础：组件、系统与查询

这一部分的目标是完成第一个真正运行的 ECS 场景：大量实体朝不同方向移动。

### 4.1 定义移动数据

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct MoveSpeed : IComponentData
{
    // 使用单精度浮点数表示每秒移动距离。
    public float Value;
}

public struct MoveDirection : IComponentData
{
    // 方向可在 Baker/生成系统中归一化；此例在使用时安全归一化。
    public float3 Value;
}
```

组件使用 `struct`，有利于形成适合批量处理的非托管数据布局。

### 4.2 使用 ISystem 编写移动系统

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 缓存帧间隔，避免在循环中重复取值。
        float deltaTime = SystemAPI.Time.DeltaTime;

        // RefRW 声明写 LocalTransform；RefRO 声明只读速度和方向。
        // 查询只匹配同时拥有这三种组件的实体。
        foreach ((RefRW<LocalTransform> transform,
                  RefRO<MoveSpeed> speed,
                  RefRO<MoveDirection> direction)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                                    RefRO<MoveSpeed>,
                                    RefRO<MoveDirection>>())
        {
            // normalizesafe 对零向量返回零，避免 normalize 产生 NaN。
            float3 normalizedDirection = math.normalizesafe(direction.ValueRO.Value);
            // 只修改 Position，不改变旋转和缩放，也不产生结构变化。
            transform.ValueRW.Position += normalizedDirection * speed.ValueRO.Value * deltaTime;
        }
    }
}
```

需要理解：

- `RefRO<T>` 表示只读访问
- `RefRW<T>` 表示可写访问
- 尽量准确声明读写权限
- 查询只会匹配拥有全部指定组件的实体
- System 不需要保存每一个实体的引用

不要急着把它改成 Job。先在主线程系统中把数据设计和查询逻辑写正确。

### 4.3 Tag Component

没有字段的组件可以用于表达分类或状态：

```csharp
using Unity.Entities;

public struct EnemyTag : IComponentData
{
    // 空组件不承载数值，只用于查询分类。
}

public struct PlayerTag : IComponentData
{
}
```

Tag Component 不等于传统继承关系。它只是让系统能够查询某类实体。

例如只有敌人需要自动移动时，可以把 `EnemyTag` 加入查询条件。

### 4.4 查询条件与排除条件

简单查询可使用 `SystemAPI.Query`。需要更明确的过滤、批量操作或复用查询时，可以创建 `EntityQuery`。

```csharp
using Unity.Entities;

public partial struct EnemyCountSystem : ISystem
{
    private EntityQuery enemyQuery;

    public void OnCreate(ref SystemState state)
    {
        // WithAll 是必须拥有，WithNone 是必须不拥有。查询可跨帧复用。
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, Health>()
            .WithNone<DeadTag>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        // CalculateEntityCount 返回当前匹配实体数；本地变量未消费仅用于演示。
        int aliveEnemyCount = enemyQuery.CalculateEntityCount();
    }
}

public struct DeadTag : IComponentData
{
}
```

### 4.5 RequireForUpdate

如果某个系统必须在存在特定组件时才有意义，可以在创建阶段声明：

```csharp
public void OnCreate(ref SystemState state)
{
    // 世界中没有 SimulationConfig 时，OnUpdate 不会执行。
    state.RequireForUpdate<SimulationConfig>();
}
```

这样缺少必要数据时，系统不会无意义地每帧执行。

### 4.6 单例组件

全局模拟配置或统计状态可以使用 Singleton Component，但不要把它变成新的“万能单例”。

```csharp
using Unity.Entities;

public struct SimulationConfig : IComponentData
{
    // 这是整局共享的模拟参数，通常放在唯一实体上。
    public int SpawnCount;
    public float SpawnRadius;
}
```

读取方式示意：

```csharp
// 要求恰好有一个匹配实体；零个或多个都是数据建模错误。
SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();
```

单例组件适合：

- 一局模拟只有一份的配置
- 全局时间倍率
- 关卡级参数
- 聚合后的统计数据

不适合：

- 把所有系统需要的数据全部塞进去
- 用它绕过合理查询
- 用它保存大量不断变化且访问模式完全不同的数据

### 本节练习

实现一个移动场景：

- 至少 1,000 个实体
- 每个实体有不同速度或方向
- 只让带 `MovingTag` 的实体移动
- 用一个单例组件控制全局速度倍率
- 在 Entities Hierarchy 中观察实体和组件
- 在 Systems 窗口中观察系统是否执行

### 本节验收标准

- 能自己定义 `IComponentData`
- 能使用 `ISystem` 和 `SystemAPI.Query`
- 能正确区分 `RefRO` 与 `RefRW`
- 能使用 Tag Component 控制查询范围
- 能解释单例组件适用和不适用的场景

---

## 五、Authoring、Baking 与 SubScene

ECS 运行时强调纯数据，但制作内容时仍然需要 Unity 编辑器、Inspector 和 GameObject 工作流。

Authoring 与 Baking 就是两者之间的桥梁。

### 5.1 正确理解转换流程

```text
编辑阶段
GameObject
  └── MoveSpeedAuthoring (MonoBehaviour)
                │
                │ Baker 转换
                ▼
运行阶段
Entity
  ├── LocalTransform
  └── MoveSpeed (IComponentData)
```

Authoring Component：

- 用于 Inspector 编辑
- 可以引用 Unity 资产或其他 GameObject
- 不是最终的 ECS 运行时数据

Baker：

- 读取 Authoring 数据
- 获得或创建 Entity
- 添加 ECS Component
- 声明烘焙依赖

### 5.2 编写第一个 Baker

```csharp
using Unity.Entities;
using UnityEngine;

public sealed class MoveSpeedAuthoring : MonoBehaviour
{
    // Authoring 只在编辑/烘焙阶段提供 Inspector 输入。
    [Min(0f)] public float Value = 3f;

    private sealed class MoveSpeedBaker : Baker<MoveSpeedAuthoring>
    {
        public override void Bake(MoveSpeedAuthoring authoring)
        {
            // Dynamic 表示运行时会更新变换，Baker 会为主 GameObject 取得实体。
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // 把托管 Authoring 值复制成非托管运行时组件。
            AddComponent(entity, new MoveSpeed
            {
                Value = authoring.Value
            });
        }
    }
}
```

需要理解 `TransformUsageFlags` 的意图：

- `Dynamic`：实体的 Transform 会在运行时变化
- `Renderable`：需要参与渲染相关转换
- `None`：不要求转换 Transform 数据

具体可用标志和转换行为可能随包版本调整，以当前安装版本文档为准。

### 5.3 SubScene

SubScene 用于把场景中的 Authoring 内容烘焙为实体场景数据。

学习阶段建议：

1. 主场景保留相机和必要的 GameObject 管理对象
2. 创建一个 SubScene
3. 把需要转换的 Authoring GameObject 放入 SubScene
4. 进入 Play Mode 后使用 Entities Hierarchy 检查结果

必须能回答：

- 编辑模式看到的是 GameObject 还是 Entity？
- Play Mode 中系统处理的是哪一份数据？
- 修改 Authoring 字段后，什么时候触发重新 Baking？

### 5.4 Entity Prefab

运行时批量生成单位时，通常先把 GameObject Prefab 烘焙为 Entity Prefab，再由系统实例化。

```csharp
using Unity.Entities;

public struct UnitSpawner : IComponentData
{
    // Prefab 是烘焙后的 Entity Prefab，不是 GameObject 引用。
    public Entity Prefab;
    public int Count;
}
```

对应 Authoring：

```csharp
using Unity.Entities;
using UnityEngine;

public sealed class UnitSpawnerAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    [Min(1)] public int Count = 1000;

    private sealed class UnitSpawnerBaker : Baker<UnitSpawnerAuthoring>
    {
        public override void Bake(UnitSpawnerAuthoring authoring)
        {
            // Spawner 自身不需要运行时变换数据。
            Entity entity = GetEntity(TransformUsageFlags.None);
            // 被生成的 Prefab 需要可动变换数据。GetEntity 也声明烘焙依赖。
            Entity prefabEntity = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new UnitSpawner
            {
                Prefab = prefabEntity,
                Count = authoring.Count
            });
        }
    }
}
```

注意：

- Authoring 中引用的是 `GameObject`
- 烘焙后的组件保存的是 `Entity`
- 不要在运行时组件里直接保存 Prefab GameObject 引用
- Spawner 完成一次生成后，应通过状态或组件移除避免重复生成

### 5.5 Baking 数据不是存档数据

这三类数据仍需分开：

```text
ScriptableObject / Authoring
  静态配置与编辑器输入

IComponentData / Buffer
  ECS 运行时状态

SaveData
  需要持久化到磁盘的内容
```

ECS 不会自动替代存档系统。加载存档时，需要由桥接层把存档数据写入实体世界。

### 本节练习

- 创建一个 Unit Authoring Prefab
- 使用 Baker 添加速度、方向、生命值和阵营数据
- 在 SubScene 中放置一个 Spawner
- 将 GameObject Prefab 烘焙为 Entity Prefab
- 运行时生成至少 5,000 个实体
- 使用 Entities Hierarchy 检查生成结果

### 本节验收标准

- 能解释 Authoring 和运行时 Component 的区别
- 能独立编写 `Baker<T>`
- 能选择基本的 `TransformUsageFlags`
- 能使用 SubScene 检查 Baking 结果
- 能将 GameObject Prefab 引用转换成 Entity Prefab 引用

---

## 六、实体生命周期与结构变化

这一部分开始处理实体生成、死亡、状态切换和组件变化。

### 6.1 什么是 Structural Change

以下操作会造成结构变化：

- 创建实体
- 销毁实体
- 添加组件
- 移除组件
- 修改 Shared Component 导致实体迁移

以下通常不是结构变化：

- 修改生命值字段
- 修改速度字段
- 修改 `LocalTransform.Position`
- 修改 Dynamic Buffer 中已有元素的内容

结构变化可能导致实体迁移到其他 Chunk，所以不能把它当成普通字段写入。

### 6.2 为什么需要 EntityCommandBuffer

遍历实体时直接删除当前实体或修改其组件组合，会使当前查询的数据结构发生变化。

`EntityCommandBuffer` 用于先记录命令，在安全的时间点统一回放。

```text
System 遍历实体
  ├── 记录 DestroyEntity(A)
  ├── 记录 AddComponent(B, DeadTag)
  └── 记录 Instantiate(Prefab)
            │
            ▼
指定的 ECB System 在安全点回放命令
```

### 6.3 死亡处理示例

```csharp
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct DeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 从帧末 ECB 系统取得命令缓冲；此处只记录，不立即销毁。
        EntityCommandBuffer commandBuffer =
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

        // WithEntityAccess 让查询同时返回 Entity ID，便于录制销毁命令。
        foreach ((RefRO<Health> health, Entity entity)
                 in SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
        {
            if (health.ValueRO.Current <= 0)
            {
                // 实际销毁发生在 EndSimulation ECB 回放时。
                commandBuffer.DestroyEntity(entity);
            }
        }
    }
}
```

学习重点不是记住固定写法，而是理解：

- 命令在当前系统中被记录
- 实际结构变化发生在后续回放阶段
- 选择哪个 ECB System 会影响回放时机
- 其他系统在回放前仍可能看见旧结构

### 6.4 用 Enableable Component 表达高频开关状态

如果某个状态会频繁切换，反复添加和移除组件可能造成大量结构变化。可以考虑 `IEnableableComponent`。

```csharp
using Unity.Entities;

public struct Stunned : IComponentData, IEnableableComponent
{
    // 启用/禁用该组件不改变 Archetype，适合频繁开关状态。
}
```

组件类型仍然存在于实体的 Archetype 中，但可以启用或禁用。默认查询通常只匹配启用状态的该组件。

适合表达：

- 是否眩晕
- 是否暂停移动
- 是否需要本帧处理
- 是否处于某个高频切换状态

不要把所有状态都改成 Enableable Component。需要根据切换频率、查询方式和数据含义判断。

### 6.5 实体引用与版本

`Entity` 包含索引与版本信息。实体被销毁后，旧引用不应该继续使用。

需要养成这些习惯：

- 使用前确认实体是否仍存在
- 不长期缓存大量不受控制的 Entity 引用
- 通过关系组件明确所有者和目标
- 目标销毁时设计清理策略

```csharp
using Unity.Entities;

public struct Target : IComponentData
{
    // Entity 引用含 Index 和 Version；读取目标组件前应验证实体仍存在。
    public Entity Value;
}
```

系统处理 `Target` 时，需要考虑目标实体已经被销毁的情况。

### 本节练习

实现一套最小生命周期流程：

- Spawner 生成实体
- Lifetime System 递减生存时间
- 生存时间归零后通过 ECB 销毁
- 生命值归零后添加或启用死亡状态
- 死亡状态延迟一小段时间后销毁实体
- 统计每帧生成数与销毁数

### 本节验收标准

- 能判断某个操作是否属于结构变化
- 能解释 ECB 的记录与回放时机
- 能使用 ECB 创建、销毁或添加组件
- 能说明 Enableable Component 与添加/移除组件的区别
- 能处理失效的 Entity 引用

---

## 七、Jobs 与 Burst：从正确执行到并行执行

DOTS 不等于“所有代码都必须多线程”。正确顺序应该是：

```text
建立正确的数据模型
        ↓
写出正确的主线程 System
        ↓
使用 Profiler 定位耗时
        ↓
将适合的计算改成 Job
        ↓
启用 Burst 并重新测量
```

### 7.1 Burst 解决什么问题

Burst 会把符合约束的 C# 代码编译成本地优化代码，特别适合：

- 大量数值计算
- 重复的数据变换
- 向量与矩阵运算
- 可预测的批量循环
- 不依赖托管对象的算法

Burst 不是一个“加上特性就一定更快”的魔法开关。代码的数据布局、访问方式和算法复杂度仍然决定性能上限。

### 7.2 IJobEntity

把移动逻辑改成 Job：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    // Job 是值类型，调度时把本帧的 DeltaTime 复制进去。
    public float DeltaTime;

    private void Execute(
        ref LocalTransform transform, // ref：每个匹配实体的可写组件
        in MoveSpeed speed,
        in MoveDirection direction)    // in：只读，利于依赖分析和并行调度
    {
        float3 normalizedDirection = math.normalizesafe(direction.Value);
        transform.Position += normalizedDirection * speed.Value * DeltaTime;
    }
}

[BurstCompile]
public partial struct JobMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        MoveJob job = new MoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        // 传入旧依赖并保存新 JobHandle，不阻塞主线程。
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}
```

需要理解：

- `in T` 表示只读组件
- `ref T` 表示可写组件
- `ScheduleParallel` 允许批次在工作线程上并行执行
- `state.Dependency` 表示系统之间的数据依赖链
- Unity 根据组件读写关系帮助管理 Job 依赖

### 7.3 Schedule、ScheduleParallel 与 Run

概念上可以这样区分：

- `Run`：立即在当前线程执行，适合调试或极少量工作
- `Schedule`：调度 Job，但不要求并行分片
- `ScheduleParallel`：允许将工作拆分后并行处理

不要默认所有 Job 都应该 `ScheduleParallel`。工作量太小时，调度成本可能高于计算本身。

### 7.4 Job 依赖

假设系统 A 写入 `Velocity`，系统 B 读取 `Velocity` 并更新位置，那么 B 必须等待 A 的相关 Job 完成。

```text
VelocityCalculationJob
          │ 写 Velocity
          ▼
MovementJob
          │ 读 Velocity，写 LocalTransform
          ▼
Transform / Presentation
```

正确维护 `state.Dependency`，不要随意调用 `Complete()` 强制主线程等待。过早完成 Job 会破坏并行流水线。

只有这些情况可能需要主动完成依赖：

- 主线程马上需要读取 Job 输出
- 需要执行与当前 Job 数据冲突的同步操作
- 调试依赖关系
- 某些 API 明确要求同步完成

### 7.5 Native Container

Job 无法随意使用普通 `List<T>`、`Dictionary<TKey,TValue>` 或普通 C# 对象。多线程数据通常使用 Unity Collections 提供的原生容器。

常见类型：

- `NativeArray<T>`：固定长度连续数组
- `NativeList<T>`：可变长度列表
- `NativeHashMap<TKey,TValue>`：键值查询
- `NativeParallelHashMap<TKey,TValue>`：支持相应并行使用场景
- `NativeQueue<T>`：队列
- `NativeStream`：适合并行写入的流式结果

需要掌握 Allocator 生命周期：

- 临时、单帧或短生命周期数据
- 跨帧保存的数据
- 谁创建，谁释放
- Job 使用期间不能提前释放

具体 Allocator 名称和限制以当前 Collections 版本文档为准。

### 7.6 并行写入冲突

并行 Job 最重要的不是“如何启动”，而是“如何避免多个线程写同一份数据”。

例如多名攻击者同时伤害同一个目标：

```text
Attacker A ─┐
Attacker B ─┼── 同时写 Target.Health → 数据竞争
Attacker C ─┘
```

更安全的设计通常是：

```text
攻击阶段
  每个攻击者只写一条 DamageEvent
            │
            ▼
伤害汇总阶段
  按目标聚合 DamageEvent
            │
            ▼
结算阶段
  每个目标只由一个执行路径修改 Health
```

这也是 ECS 中“阶段化流水线”非常重要的原因。

### 7.7 ComponentLookup 与 BufferLookup

当 Job 需要通过 Entity 访问另一实体的组件时，可能需要 `ComponentLookup<T>` 或 `BufferLookup<T>`。

它们适合随机访问，但随机访问通常不如顺序遍历友好，而且并行写入限制更严格。

使用原则：

- 能直接查询顺序处理，就不要先收集 Entity 再随机访问
- 明确只读还是可写
- 每帧正确更新 Lookup
- 对跨实体并行写入保持谨慎
- 不要为了方便而把所有组件都变成 Lookup

### 本节练习

对同一移动模拟完成三个版本：

1. `SystemAPI.Query` 主线程版本
2. `IJobEntity.Schedule` 版本
3. `IJobEntity.ScheduleParallel` + Burst 版本

分别测试：

- 1,000 个实体
- 10,000 个实体
- 50,000 个实体
- 100,000 个实体（设备允许时）

记录：

- 主线程耗时
- Job Worker 耗时
- 每帧总耗时
- 是否出现同步等待
- 不同实体数量下的性能拐点

### 本节验收标准

- 能独立编写 `IJobEntity`
- 能解释 `Run`、`Schedule` 和 `ScheduleParallel` 的差别
- 能维护 `state.Dependency`
- 能说明为什么频繁 `Complete()` 会降低并行收益
- 能选择基本 Native Container 并管理生命周期
- 能识别多线程写冲突

---

## 八、进阶数据结构：Buffer、Blob 与共享数据

基础 `IComponentData` 适合固定大小数据，但游戏中还会遇到技能列表、路径节点、Buff 列表和只读配置表。

### 8.1 Dynamic Buffer

Dynamic Buffer 用于给每个实体保存可变长度、由非托管元素组成的数据。

例如一个单位身上的 Buff：

```csharp
using Unity.Entities;

public struct ActiveBuff : IBufferElementData
{
    // BuffId 指向静态定义；其余字段是每个单位的运行时实例状态。
    public int BuffId;
    public float RemainingTime;
    public float Magnitude;
}
```

系统可以查询并修改 Buffer：

```csharp
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct BuffTickSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // DynamicBuffer 是挂在每个匹配实体上的可变长度非托管列表。
        foreach (DynamicBuffer<ActiveBuff> buffs
                 in SystemAPI.Query<DynamicBuffer<ActiveBuff>>())
        {
            // 倒序遍历，删除当前元素时不会跳过后续元素。
            for (int index = buffs.Length - 1; index >= 0; index--)
            {
                ActiveBuff buff = buffs[index];
                buff.RemainingTime -= deltaTime;

                if (buff.RemainingTime <= 0f)
                {
                    buffs.RemoveAt(index);
                    continue;
                }

                // Buffer 索引器取出的 struct 是值副本，修改后必须写回。
                buffs[index] = buff;
            }
        }
    }
}
```

适合使用 Buffer 的场景：

- Buff 列表
- 路径节点
- 技能槽
- 背包的纯运行时条目
- 伤害事件队列
- 一个实体关联的子实体列表

不适合时要考虑：

- 数据是否应该是独立实体
- 数据是否被大量实体共享
- Buffer 是否会无限增长
- 是否频繁扩容导致额外成本

### 8.2 Blob Asset

Blob Asset 适合大量实体共享的不可变数据，例如：

- 技能配置
- 武器成长曲线
- 行为树静态节点数据
- 地图导航静态数据
- 动画或采样表
- 敌人原型配置

可以把它理解为：

```text
许多实体
  ├── BlobAssetReference<EnemyDefinition> ─┐
  ├── BlobAssetReference<EnemyDefinition> ─┼── 同一份只读配置
  └── BlobAssetReference<EnemyDefinition> ─┘
```

Blob 中的数据必须以相对引用方式组织，构建过程通常在 Baking 或初始化阶段完成。

学习时先理解三个特点：

- 不可变
- 可共享
- 适合 Burst 读取

不要把不断变化的当前生命值放进 Blob。当前生命值属于实体实例状态，应该放在普通 Component 中。

### 8.3 Shared Component

Shared Component 可以让具有相同共享值的实体被组织到一起，但不同值过多会造成 Chunk 碎片化。

可能适合：

- 渲染材质分类
- 少量稳定的区域分组
- 少量稳定阵营或批次分类

通常不适合：

- 每个实体都有独一无二的值
- 高频变化的状态
- 当前目标、当前生命值、当前位置

使用 Shared Component 前必须理解其分组成本，不要因为名称中有“Shared”就用它替代所有公共配置。

### 8.4 Component、Buffer、Blob 如何选择

| 需求 | 推荐结构 |
|---|---|
| 单个实体固定大小的可变状态 | `IComponentData` |
| 单个实体可变长度的数据 | `DynamicBuffer` |
| 许多实体共享的不可变配置 | Blob Asset |
| 少量稳定值的实体分组 | Shared Component |
| 高频布尔状态开关 | Enableable Component |
| 本帧或短期处理请求 | Event Component 或 Buffer |

### 8.5 Buff 系统的 ECS 化思考

你在第四阶段学过的传统 Buff 系统，可能使用基类、接口和虚方法：

```text
BuffBase
  Apply()
  Tick()
  Remove()
```

ECS 版本不应该机械翻译成一个“包含方法的 BuffComponent”，而可以拆成：

```text
ActiveBuff Dynamic Buffer
  保存 BuffId、剩余时间、层数、来源

BuffDefinition Blob Asset
  保存不可变配置

BuffTickSystem
  统一递减持续时间

BuffEffectSystem
  计算属性修正

BuffExpireSystem
  移除到期 Buff
```

这个练习能很好地连接第四阶段与第五阶段。

### 本节练习

实现一个最小 ECS Buff 系统：

- 每个单位有 `DynamicBuffer<ActiveBuff>`
- 支持持续时间
- 支持层数
- Buff 静态定义与运行时实例分开
- 到期后正确移除
- 属性计算系统根据 Buff 结果更新最终速度或攻击力
- 不在每个 Buff 上创建一个托管对象

### 本节验收标准

- 能选择 Component、Buffer、Blob 或 Shared Component
- 能使用 Dynamic Buffer 保存可变长度数据
- 能解释 Blob 为什么只能保存不可变配置
- 能说明 Shared Component 值过多的问题
- 能将传统 Buff 系统重新设计成数据处理流水线

---

## 九、系统组织、更新顺序与通信

当系统数量增加后，仅仅“每个系统能运行”还不够，还要保证处理顺序明确。

### 9.1 System Group

常见系统阶段可以理解为：

```text
Initialization
  初始化、读取外部输入、准备本帧数据

Simulation
  游戏规则、移动、攻击、伤害、死亡

Presentation
  将模拟结果同步给渲染和表现
```

还可以创建自己的系统组，例如：

```text
CombatSimulationGroup
  TargetSearchSystem
  AttackRequestSystem
  DamageResolveSystem
  DeathMarkSystem
```

### 9.2 显式更新顺序

可以通过更新特性表达约束。示意：

```csharp
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
// 自定义组把战斗系统收敛到 Simulation 阶段内。
public partial class CombatSimulationGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(CombatSimulationGroup))]
public partial struct TargetSearchSystem : ISystem
{
}

[UpdateInGroup(typeof(CombatSimulationGroup))]
[UpdateAfter(typeof(TargetSearchSystem))]
// 攻击请求依赖搜索结果，因此显式声明在其后更新。
public partial struct AttackRequestSystem : ISystem
{
}
```

不要依赖“看起来创建得早”或脚本文件名来推断系统顺序。关键依赖应该显式表达。

### 9.3 系统间不要直接相互调用业务方法

传统写法可能是：

```text
AttackManager 调用 DamageManager.ApplyDamage()
DamageManager 再调用 DeathManager.Kill()
```

ECS 更适合通过数据阶段解耦：

```text
AttackSystem
  产生 DamageEvent
        ↓
DamageSystem
  消费 DamageEvent，修改 Health
        ↓
DeathSystem
  查询 Health <= 0
```

这样每个系统只关心输入数据和输出数据。

### 9.4 Event Component 与 Event Buffer

事件可以使用短生命周期实体：

```csharp
using Unity.Entities;

public struct DamageEvent : IComponentData
{
    // Source 便于追踪归属/统计，Target 是受击者，Amount 是本次原始伤害。
    public Entity Source;
    public Entity Target;
    public int Amount;
}
```

处理流程：

```text
本帧创建 DamageEvent Entity
              ↓
DamageResolveSystem 读取并结算
              ↓
本帧末或下一阶段销毁 Event Entity
```

也可以使用集中式 Dynamic Buffer 保存事件。两种方式各有取舍：

- 事件实体表达直观，查询方便，但会涉及实体生命周期
- 集中 Buffer 减少事件实体数量，但并行写入和聚合要谨慎设计

不要默认照搬 C# 委托事件。托管委托通常不适合 Burst Job 中的大规模数据流。

### 9.5 一帧内的数据流水线

以战斗模拟为例：

```text
Input / Spawn Requests
          ↓
Spatial Partition Update
          ↓
Target Search
          ↓
Attack Cooldown
          ↓
Damage Event Production
          ↓
Damage Resolution
          ↓
Death Marking
          ↓
Entity Cleanup
          ↓
Statistics Aggregation
          ↓
Presentation Sync
```

学习 ECS 时，要开始把“对象之间互相调用”转化为“数据经过多个明确阶段”。

### 本节练习

实现伤害流水线：

- 攻击系统只创建伤害事件
- 伤害系统负责扣除生命值
- 死亡系统只处理生命值归零实体
- 清理系统销毁已完成的事件
- 用 System Group 和更新特性明确顺序
- 在 Systems 窗口中验证实际执行顺序

### 本节验收标准

- 能使用系统组组织功能
- 能显式表达关键更新顺序
- 能使用数据而不是直接系统调用进行通信
- 能设计 Event Entity 或 Event Buffer 生命周期
- 能画出一帧模拟的数据流

---

## 十、空间查询与算法意识

当单位开始自动寻找目标时，性能瓶颈往往不在 ECS API，而在算法。

### 10.1 暴力搜索的问题

如果每个单位都遍历所有敌人：

```text
10,000 个单位 × 10,000 个候选目标
= 100,000,000 次比较
```

即使 Burst 很快，错误的算法复杂度仍会迅速耗尽预算。

### 10.2 空间划分

常见做法包括：

- Uniform Grid
- Spatial Hash
- 四叉树或八叉树
- 分区列表
- 物理世界查询（适用于符合需求的情况）

学习项目推荐先实现 Uniform Grid / Spatial Hash：

```text
世界空间
  ├── Cell (0,0): Unit 1, Unit 8
  ├── Cell (0,1): Unit 5
  ├── Cell (1,0): Unit 2, Unit 3
  └── Cell (1,1): Unit 4, Unit 7

单位只搜索自身格子与相邻格子
```

### 10.3 分阶段构建空间索引

```text
BuildSpatialHashSystem
  把单位位置写入空间索引
              ↓
TargetSearchSystem
  只查询邻近格子
              ↓
TargetValidationSystem
  检查目标是否仍然有效
```

要特别注意：

- 多线程向 HashMap 写入的安全方式
- 容器容量预估
- 每帧清空还是增量更新
- 目标死亡后的引用失效
- 格子尺寸如何影响候选数量

### 10.4 算法优化优先于微观优化

推荐优化顺序：

1. 降低算法复杂度
2. 减少不必要的处理对象数量
3. 改善数据布局和访问顺序
4. 减少同步点
5. 并行化适合的计算
6. 最后再做局部微优化

### 本节练习

比较两种目标搜索：

- 所有敌人暴力遍历
- 基于网格的邻域搜索

分别测试 1,000、5,000、10,000 个单位，并记录搜索耗时和找到目标的正确性。

### 本节验收标准

- 能识别 O(n²) 搜索
- 能实现最小 Spatial Hash 或 Uniform Grid
- 能处理索引构建与查询的更新顺序
- 能通过数据证明算法优化的效果

---

## 十一、性能分析：必须证明，而不是猜测

DOTS 学习最容易出现的误区是只看帧率，或者认为实体数量多就一定说明实现正确。

### 11.1 需要观察的工具

- Unity Profiler
- CPU Timeline
- Job System 相关线程
- Burst Inspector
- Entities Hierarchy
- Systems 窗口
- Entity / Chunk 相关分析视图（具体名称依版本而定）
- Memory Profiler（需要分析内存时）

### 11.2 每次实验都记录基线

建议建立测试表：

| 版本 | 实体数 | 主线程耗时 | Worker 耗时 | 总帧时 | GC Alloc | 备注 |
|---|---:|---:|---:|---:|---:|---|
| MonoBehaviour | 10,000 |  |  |  |  |  |
| ECS Main Thread | 10,000 |  |  |  |  |  |
| ECS Job | 10,000 |  |  |  |  |  |
| ECS Parallel + Burst | 10,000 |  |  |  |  |  |

测试时保持：

- 相同单位数量
- 相同算法
- 相同渲染开销或关闭渲染单独测模拟
- 相同硬件和构建配置
- 预热后再采样
- 不只观察某一帧

### 11.3 常见性能问题

- 每帧大量 Structural Change
- 频繁创建和释放 Native Container
- 过多 Shared Component 唯一值造成 Chunk 碎片
- 过大的组件导致无关系统读取多余数据
- 每帧把大量 ECS 数据复制到 GameObject
- 到处调用 `Complete()`
- Job 太小，调度成本高于计算
- 随机访问过多
- O(n²) 算法
- 实体数量很大但渲染才是真正瓶颈
- 编辑器中的测试结果被编辑器开销干扰

### 11.4 Chunk 利用率意识

需要逐渐建立这些判断：

- 当前 Archetype 有多少组件？
- 单个组件是否过大？
- 实体是否因为频繁增删组件不断迁移？
- Shared Component 是否制造了大量小分组？
- 某系统是否只需要一个字段，却加载了一个巨大组件？

不要求初学阶段手动计算所有 Chunk 布局，但必须知道数据设计会直接影响运行方式。

### 11.5 Development Build 与编辑器测试

最终性能判断不要只依赖 Editor Play Mode。应至少做一次 Development Build，并使用 Profiler 连接目标程序。

记录：

- 测试平台
- Unity 和包版本
- 是否启用 Burst
- 是否启用安全检查
- 是否包含渲染
- 目标帧率
- 采样窗口

### 本节练习

为移动模拟与目标搜索各写一份性能报告，至少包含：

- 测试目标
- 对照版本
- 测试硬件
- 实体数量
- Profiler 截图
- 主要耗时
- 结论
- 下一步优化方向

### 本节验收标准

- 能找到具体系统和 Job 的耗时
- 能区分主线程、工作线程和渲染瓶颈
- 能用对照实验验证优化效果
- 能解释为什么 Editor 帧率不能作为唯一依据
- 能写出一页简洁的性能报告

---

## 十二、GameObject 与 ECS 混合架构

生产项目通常不需要“所有东西都 ECS 化”。关键是让 ECS 负责真正适合批量处理的模拟，让传统 Unity 负责成熟且表现导向的部分。

### 12.1 推荐边界

| 功能 | 通常更适合 |
|---|---|
| 大量单位移动 | ECS |
| 大量投射物与范围检测 | ECS |
| 大规模属性、冷却和伤害结算 | ECS |
| UI、菜单、设置 | GameObject / MonoBehaviour |
| 相机控制 | GameObject / MonoBehaviour |
| 玩家输入 | Input System + 桥接层 |
| 少量主角的复杂表现 | GameObject 或混合方案 |
| 大量简单实体渲染 | Entities Graphics |
| 静态配置编辑 | ScriptableObject / Authoring |
| 存档序列化 | 普通 C# 数据结构 |
| 资源加载 | Addressables / 服务层 |

这个表不是强制规则。真正判断依据是：

- 数量是否足够大
- 数据是否同构
- 是否需要批量计算
- 是否适合非托管数据
- 是否需要频繁使用成熟 GameObject 生态
- 转换成本是否值得

### 12.2 输入桥接

可以由 MonoBehaviour 读取 Input System，再写入 ECS 单例组件：

```text
Input System
     ↓
PlayerInputBridge (MonoBehaviour)
     ↓
PlayerInputState (ECS Singleton)
     ↓
PlayerMovementSystem
```

注意：

- 输入采集和模拟处理分开
- 明确每帧何时写入、何时读取
- 不要让大量实体逐个访问 MonoBehaviour

### 12.3 UI 桥接

UI 不需要每帧遍历全部实体。更合理的方式是由 ECS 聚合统计数据：

```text
数万单位
   ↓ StatisticsSystem 聚合
SimulationStatistics Singleton
   ↓ 每帧或低频读取一次
HUD Presenter
   ↓
UI Text / Bar / Graph
```

UI 只读取聚合后的少量数据，例如：

- 当前单位数
- 红蓝双方存活数
- 玩家选中目标生命值
- 当前波次
- 模拟速度

### 12.4 配置桥接

第三阶段的 ScriptableObject 仍然有价值：

```text
EnemyConfig ScriptableObject
           ↓ Authoring / Baker
EnemyDefinition Blob Asset
           ↓
多个 Enemy Entity 共享
```

不要在运行时直接让 Burst Job 访问 ScriptableObject。应该在 Baking 或初始化阶段把需要的数据转换成 ECS 友好形式。

### 12.5 存档桥接

建议流程：

```text
保存
ECS World
  ↓ Save Extraction System
普通 SaveData
  ↓ Save Service
JSON / Binary / Cloud

加载
Save Service
  ↓ 普通 SaveData
World Initialization System
  ↓
创建实体并写入组件
```

不是所有短期实体都要存档。子弹、临时事件、视觉碎片通常无需保存。先定义“持久化边界”。

### 12.6 不要逐实体双向同步

最危险的混合方式之一是：

```text
每一个 ECS Entity
  ↔ 每一个 GameObject
  每帧双向复制全部数据
```

这样可能同时承担两套架构成本，并失去 ECS 的批量处理优势。

桥接应该：

- 数量少
- 数据方向清楚
- 更新频率受控
- 只同步表现真正需要的字段
- 尽可能聚合

### 本节练习

给前面的模拟添加：

- GameObject 相机
- Input System 控制模拟暂停和速度
- UI 显示实体总数、死亡数和帧耗时
- ECS 单例保存输入状态与统计结果
- UI 不直接遍历所有实体

### 本节验收标准

- 能判断某个功能是否值得 ECS 化
- 能设计输入和 UI 桥接层
- 能将 ScriptableObject 配置转换为 ECS 数据
- 能说明 ECS 与存档之间的边界
- 不使用逐实体、全数据、每帧双向同步

---

## 十三、推荐实践项目：大规模阵营战斗模拟

第五阶段的最终项目不是完整商业游戏，而是一个能够展示数据设计、并行计算和混合架构的垂直原型。

### 13.1 项目目标

实现两个阵营的大规模战斗模拟：

- 初始支持 5,000 个单位
- 优化后根据设备尝试 10,000～50,000 个单位
- 单位寻找附近敌人
- 移动到攻击范围
- 按冷却时间攻击
- 产生伤害事件
- 生命值归零后死亡
- UI 显示双方存活数量和性能数据
- 可暂停、改变模拟速度、重新开始

重点不在画面，而在模拟架构和性能报告。

### 13.2 数据设计参考

```text
Unit Entity
  LocalTransform
  MoveSpeed
  Health
  AttackStats
  AttackCooldown
  Faction
  Target
  UnitState
  UnitDefinitionReference

可选组件
  Stunned (Enableable)
  DeadTag
  SelectedTag
  ActiveBuff Buffer

事件
  DamageEvent
  SpawnRequest
  DeathEvent

单例
  SimulationConfig
  SimulationTime
  SimulationStatistics
```

需要自己决定：

- `Faction` 使用普通组件还是其他分组方式
- `UnitState` 使用字段、Tag 还是 Enableable Component
- 死亡后立即销毁还是保留短暂表现期
- Target 失效时如何重新搜索
- Buff 是否属于最终项目必须项

### 13.3 系统流水线参考

```text
InitializationSystemGroup
  SpawnRequestSystem
  SimulationBootstrapSystem

SimulationSystemGroup
  SpatialHashBuildSystem
  TargetValidationSystem
  TargetSearchSystem
  MovementSystem
  AttackCooldownSystem
  AttackEventSystem
  DamageResolveSystem
  DeathMarkSystem
  DeathCleanupSystem
  StatisticsSystem

PresentationSystemGroup
  SelectionPresentationSystem
  HybridUiSyncSystem
```

不要求照搬名称，但必须能解释为什么是这个顺序。

### 13.4 项目分阶段实现

#### 里程碑一：生成与移动

- Baker 转换 Entity Prefab
- 生成两个阵营
- 单位按给定方向移动
- 能稳定运行 10,000 个实体

#### 里程碑二：目标与追击

- 单位寻找目标
- 目标死亡后重新获取
- 首先实现正确的简单版本
- 再加入空间划分版本
- 保留两版性能对照数据

#### 里程碑三：攻击与伤害

- 攻击冷却
- Damage Event
- 伤害集中结算
- 避免并行写同一目标生命值

#### 里程碑四：死亡与清理

- 生命值归零进入死亡状态
- 停止移动与攻击
- 延迟销毁或立即销毁
- 使用 ECB 处理结构变化

#### 里程碑五：Buff 与配置

- 使用 Blob 保存单位静态配置
- 使用 Dynamic Buffer 保存运行时 Buff
- 至少实现减速或持续伤害中的一种

#### 里程碑六：混合表现

- Input System 控制暂停、倍速和重置
- GameObject UI 读取 ECS 聚合数据
- 相机仍使用传统 Unity 方案
- 不逐单位同步 GameObject

#### 里程碑七：性能验证

- 主线程版本
- Job 版本
- Burst + Parallel 版本
- 暴力搜索与空间划分版本
- Editor 与 Development Build 对比
- 输出最终性能报告

### 13.5 推荐目录结构

```text
Assets/
  BattleSimulation/
    Authoring/
    Baking/
    Components/
      Units/
      Combat/
      Events/
      Configuration/
    Systems/
      Initialization/
      Spatial/
      Movement/
      Combat/
      Cleanup/
      Statistics/
    Jobs/
    Aspects/
    Hybrid/
      Input/
      UI/
    Configuration/
    Prefabs/
    Scenes/
    Tests/
```

目录结构是为了表达边界，不要为了整齐创建大量只有一个文件的目录。随着项目增长再拆分。

### 13.6 最终交付物

- 一个可运行的战斗模拟场景
- 一份系统与数据结构图
- 一份每帧数据流水线图
- 一份性能测试表
- 至少两张 Profiler 截图
- 一份“为什么使用混合架构”的说明
- 一份已知问题与下一步优化清单

---

## 十四、8 周学习计划

建议每周学习 5 天，每天 1.5～3 小时。时间不足时可以扩展为 10～12 周，不需要压缩练习。

### 第 1 周：数据导向与 ECS 基础

学习内容：

- Entity、Component、System
- Archetype、Chunk、Query
- `IComponentData`
- `ISystem`
- `SystemAPI.Query`
- `RefRO` 与 `RefRW`
- Tag Component

练习：

- 把子弹、敌人、Buff 做纸面数据建模
- 创建第一个实体移动场景
- 实现速度、方向和全局倍率
- 使用 Entities Hierarchy 与 Systems 窗口

周验收：

- 不看教程独立写出移动组件和系统
- 能解释一个查询会匹配哪些实体
- 能说明 Component 划分依据

### 第 2 周：Authoring、Baking 与实体生成

学习内容：

- Authoring Component
- `Baker<T>`
- `TransformUsageFlags`
- SubScene
- Entity Prefab
- Baking 与运行时数据的边界

练习：

- 创建单位 Authoring Prefab
- 烘焙速度、方向、生命值和阵营
- 使用 Spawner 生成 5,000～10,000 个实体
- 检查 Baking 和运行时实体

周验收：

- 能独立完成 GameObject Prefab 到 Entity Prefab 的转换
- 能解释 Authoring 数据为什么不等于运行时状态
- 能定位常见 Baking 问题

### 第 3 周：生命周期与结构变化

学习内容：

- Structural Change
- `EntityCommandBuffer`
- Playback 时机
- Enableable Component
- Entity 引用有效性

练习：

- 生成、倒计时、死亡和销毁
- 用 ECB 添加状态并销毁实体
- 用 Enableable Component 切换移动或眩晕状态
- 观察结构变化造成的影响

周验收：

- 能判断哪些操作需要 ECB
- 能解释回放前后查询结果的差别
- 不在遍历时进行不安全的直接结构修改

### 第 4 周：Jobs、Burst 与 Native Container

学习内容：

- `IJobEntity`
- `Run`、`Schedule`、`ScheduleParallel`
- Burst
- Job Dependency
- Native Container
- 线程安全与数据竞争

练习：

- 将移动系统制作成三个执行版本
- 测试不同实体数量
- 实现一个并行安全的结果收集流程
- 使用 Profiler 查看 Worker Thread

周验收：

- 能正确调度并维护依赖
- 能说明为什么某个 Job 可以或不可以并行
- 能找到不必要的同步点

### 第 5 周：进阶数据建模与事件流水线

学习内容：

- Dynamic Buffer
- Blob Asset
- Shared Component 使用边界
- Event Entity / Event Buffer
- System Group 与更新顺序

练习：

- 实现 Damage Event
- 实现 Buff Buffer
- 使用 Blob 保存共享单位配置
- 完成攻击、伤害、死亡流水线

周验收：

- 能为实际需求选择合适的数据结构
- 系统之间通过数据通信
- 能画出明确的一帧处理顺序

### 第 6 周：空间划分与目标搜索

学习内容：

- 算法复杂度
- Uniform Grid / Spatial Hash
- 并行构建空间索引
- 跨实体读取
- 目标有效性

练习：

- 实现暴力目标搜索
- 实现网格目标搜索
- 对比不同单位数量的耗时
- 调整格子大小并分析影响

周验收：

- 能解释暴力搜索的增长速度
- 能使用空间划分减少候选目标
- 能用 Profiler 数据证明改进

### 第 7 周：混合架构与完整项目

学习内容：

- GameObject / ECS 边界
- Input 桥接
- UI 聚合数据桥接
- ScriptableObject 到 Blob / Component
- 存档边界

练习：

- 完成阵营战斗模拟主要功能
- 添加暂停、倍速和重置
- 添加统计 UI
- 补齐配置与运行时数据分离

周验收：

- UI 不遍历全部实体
- 输入不直接耦合战斗系统
- 能解释每一项技术为什么放在 GameObject 或 ECS 侧

### 第 8 周：性能报告与项目收尾

学习内容：

- Profiler 对照实验
- Development Build 测试
- Chunk 与结构变化分析
- 性能结论表达
- 代码与系统边界整理

练习：

- 测试多个实体规模
- 对比主线程、Job、Parallel + Burst
- 对比暴力搜索与空间搜索
- 修复最明显瓶颈
- 编写项目说明和性能报告

周验收：

- 项目能稳定运行
- 没有明显 Native Container 泄漏
- 能展示优化前后的量化数据
- 能说明系统设计的取舍和遗留问题

---

## 十五、每周学习方式

建议每周保持以下节奏：

### 第一天：概念和最小示例

- 阅读官方或与当前版本一致的资料
- 只做一个最小可运行例子
- 写下每个新类型解决的问题

### 第二天：脱离教程重写

- 不复制原代码
- 根据自己的理解重新实现
- 出错时先阅读错误和检查数据流

### 第三天：增加一个约束

例如：

- 从 100 个实体增加到 10,000 个
- 从只读查询增加到写入
- 从单系统增加到两阶段处理
- 从主线程增加到 Job

### 第四天：调试与性能观察

- 使用 Entities Hierarchy
- 使用 Systems 窗口
- 使用 Profiler
- 记录结构、时间和错误原因

### 第五天：总结与小测验

- 用自己的话解释本周概念
- 画出数据流
- 删除代码后重写核心部分
- 完成本周验收清单

不要把“视频看完”当成学会。第五阶段的学习单位应该是：

> 能运行的实验 + 可解释的数据设计 + 可复现的性能结果。

---

## 十六、判断自己有没有真正进入第五阶段

如果下面大部分问题都能独立完成，就说明你已经真正掌握了 DOTS/ECS 基础：

- 能解释 Entity 与 GameObject 的根本区别
- 能根据访问模式划分 Component
- 能解释 Archetype、Chunk 和 Structural Change
- 能使用 `ISystem` 与 `SystemAPI.Query`
- 能编写 Authoring 和 Baker
- 能使用 Entity Prefab 与 SubScene
- 能正确使用 EntityCommandBuffer
- 能判断是否应该使用 Enableable Component
- 能把合适的逻辑改成 `IJobEntity`
- 能维护 Job Dependency
- 能识别并行写入冲突
- 能使用 Dynamic Buffer 和 Blob Asset
- 能用事件数据组织系统间通信
- 能显式安排系统更新顺序
- 能使用空间划分优化目标搜索
- 能设计 GameObject / ECS 桥接层
- 能使用 Profiler 找到实际瓶颈
- 能用数字而不是感觉说明优化是否有效

真正的第五阶段能力不是“会写几个 ECS API”，而是：

> 能判断什么数据应该放在哪里、由哪个阶段处理、是否值得并行，以及如何证明这个设计有效。

---

## 十七、最常见的错误

### 错误一：把每个 MonoBehaviour 改成一个 System

ECS 是重新组织数据与处理方式，不是按文件一对一翻译。

### 错误二：把 Entity 当成更快的 GameObject

Entity 没有传统对象的完整生命周期和组件调用模型。它的优势来自数据布局、查询和批量处理。

### 错误三：一开始就全部 Job 化

先保证逻辑正确和数据模型合理，再根据 Profiler 决定并行化。

### 错误四：滥用 Structural Change

高频切换状态时，考虑字段、状态组件或 Enableable Component，而不是每帧反复添加删除组件。

### 错误五：系统之间互相直接调用

优先通过 Component、Buffer 和 Event 数据建立明确的处理阶段。

### 错误六：每个实体对应一个 GameObject

逐实体双向同步会吞掉 ECS 优势。桥接应该少量、聚合、单向且频率受控。

### 错误七：只看帧率，不看 Profiler

帧率可能受到垂直同步、GPU、编辑器和渲染限制。需要查看具体耗时。

### 错误八：认为 Burst 能拯救 O(n²) 算法

先降低复杂度，再优化执行方式。

### 错误九：使用过时教程却不检查版本

先确认 Unity、Entities、Collections 和 Burst 版本，再选择对应资料。

### 错误十：为了纯 ECS 放弃成熟工具

UI、相机、输入、资产编辑和少量复杂表现继续使用 GameObject 非常正常。

### 错误十一：组件拆得过细或过大

组件边界要同时考虑语义和访问模式，不能只追求理论上的纯粹。

### 错误十二：忽略生命周期和清理

Native Container、事件实体、失效 Entity 引用和跨帧数据都需要明确的所有权与清理规则。

---

## 十八、学习记录模板

每完成一个系统，可以使用以下模板复盘：

```text
系统名称：

输入数据：
- 

输出数据：
- 

读写权限：
- 只读：
- 写入：

执行阶段：
- 

是否产生结构变化：
- 

是否适合 Burst：
- 

是否适合并行：
- 

潜在写冲突：
- 

实体规模：
- 

Profiler 结果：
- 

优化前后差异：
- 

仍存在的问题：
- 
```

### 性能实验模板

```text
实验目标：

Unity 版本：
Entities 版本：
Burst 版本：
测试平台：
构建类型：

对照组：
实验组：

实体数量：
测试时长：
采样方式：

对照组主线程耗时：
实验组主线程耗时：

对照组总帧时：
实验组总帧时：

GC Alloc：
同步等待：

结论：

限制与误差来源：

下一步：
```

---

## 十九、第五阶段完成后的方向

完成本阶段后，可以根据职业或项目目标选择一条方向深入。

### 方向一：大规模玩法模拟

- 群体 AI
- 城市与经济模拟
- 大规模战斗
- 弹幕和投射物系统
- 空间划分与路径规划

### 方向二：Entities Graphics 与渲染

- 大量实体渲染
- 材质属性覆盖
- LOD
- GPU Instancing
- CPU 模拟与 GPU 瓶颈平衡

### 方向三：Unity Physics 与高性能物理

- ECS Physics World
- Collision / Trigger Event
- 大规模碰撞查询
- 物理结果与游戏规则分离

### 方向四：Netcode for Entities

- Ghost
- Prediction
- Snapshot
- Client / Server World
- 网络确定性与回滚意识

这条路线难度明显更高，建议在单机 ECS 数据流、系统顺序和调试能力稳定后再进入。

### 方向五：生产级混合架构

- Addressables 与实体场景加载
- 存档和世界重建
- 传统动画与 ECS 模拟桥接
- 测试、构建与性能回归
- 团队代码规范和数据制作流程

---

## 二十、最重要的提醒

1. DOTS 是解决特定规模和性能问题的工具，不是传统 Unity 的替代品。
2. 数据设计正确，比记住多少 API 更重要。
3. 先完成正确的单线程版本，再进行 Job 和 Burst 优化。
4. 系统之间通过数据流水线协作，不要重建一套 Manager 调用网。
5. 结构变化、随机访问和同步点都需要有意识地控制。
6. 算法复杂度往往比局部代码优化更重要。
7. UI、相机、输入和复杂表现继续使用 GameObject 很正常。
8. 所有性能结论都必须来自可复现的测量。
9. 教程代码必须与当前 Entities 版本匹配。
10. 第一个目标不是“做一款纯 ECS 游戏”，而是“做出一个能够证明架构价值的高性能原型”。

第五阶段完成时，你应该能够从需求出发回答四个问题：

```text
这项功能是否真的需要 ECS？

它的数据应该如何布局？

它应该在哪个系统阶段处理？

如何用 Profiler 证明设计有效？
```

当你能对这四个问题给出清楚、有数据支持的答案时，你就不只是“学过 DOTS”，而是开始具备真正的数据导向开发能力。

---

## 二十一、练习题与自测题参考答案

本章给出前文所有开放题的可执行参考答案。它们不是唯一答案；验收时应优先检查数据访问方式、生命周期和测量证据，而不是逐字匹配代码。

### 21.1 数据建模题：子弹、敌人和 Buff

| 对象 | 高频读取数据 | 低频/事件数据 | 配置数据 | 实例状态 | 可共享数据 | 可能的结构变化 |
|---|---|---|---|---|---|---|
| 子弹 | `LocalTransform`、方向、速度、生存时间 | 命中目标、伤害、Owner | 速度上限、伤害类型、Prefab | 当前坐标、剩余时间 | 相同子弹定义可用 Blob Asset | 生成、命中后销毁、切换穿透状态 |
| 敌人 | 坐标、速度、阵营、当前生命 | 攻击请求、目标引用 | 最大生命、攻击间隔、攻击距离 | 当前生命、冷却、目标 | 相同单位定义可用 Blob Asset | 生成、死亡标记、死亡后销毁 |
| Buff | 剩余时间、层数、效果值 | 应用/移除事件 | Buff 名称、图标、基础数值 | 每个单位的持续时间和层数 | 静态 Buff 定义适合 Blob Asset | 只有在 Buff 作为独立实体时才需生成/销毁 |

回答“每帧读取”的标准是：该字段是否参与每帧模拟，以及能否与其他字段一起顺序遍历。不要为了看起来细而把同一系统必定同时访问的数据拆成大量组件。配置不会因某个单位而改变，应在烘焙时写入 Blob；生命、冷却和剩余时间属于实例状态，必须写入实体组件或 Buffer。

### 21.2 ECS 基础移动练习

实现步骤：先烘焙一个带 `LocalTransform`、`MoveSpeed`、`MoveDirection`、`MovingTag` 的实体 Prefab；再创建唯一的 `SimulationConfig`，例如 `GlobalSpeedMultiplier = 1`；移动查询加入 `.WithAll<MovingTag>()`，并把位移乘以该倍率。生成 1,000 个实体时，使用 ECB 批量 `Instantiate`，不要在每个 GameObject 上挂一个 `Update`。

验收时应验证三点：不带 `MovingTag` 的实体位置不变；把倍率改成 0.5 后速度减半；Entities Hierarchy 中实体拥有预期组件且没有意外的托管组件。`RefRO` 只能读，试图写入会在编译期或安全检查中报错；`RefRW` 只授予确实需要的写权限。

### 21.3 Authoring、Baking 与 SubScene 练习

`UnitAuthoring` 保存 Inspector 字段，`Baker` 使用 `GetEntity` 和 `AddComponent` 产生运行时数据，SubScene 负责把场景内容纳入 Baking。Prefab 必须通过 `GetEntity(authoring.Prefab, ...)` 得到 Entity Prefab；不能把 `GameObject` 字段直接放进 `IComponentData`。若 Baker 看不到引用，检查对象是否位于可烘焙场景、Prefab 是否有效，以及 `TransformUsageFlags` 是否与运行时需求一致。

Baking 数据不是存档：它描述“如何从编辑内容得到初始世界”。存档加载应读取普通 `SaveData`，再由初始化系统创建实体并写入当前生命、位置等状态；否则会把编辑器资产和玩家进度错误地耦合在一起。

### 21.4 生命周期与 ECB 练习

推荐流水线为：`SpawnerSystem` 记录实例化；`LifetimeSystem` 只写 `Remaining`；`DeathMarkSystem` 通过 ECB 添加 `DeadTag` 或启用 `Dead`；`CleanupSystem` 读取死亡延迟并销毁；统计系统在销毁前汇总数量。遍历查询时不要直接 `EntityManager.DestroyEntity`，因为这会立即改变 Archetype/Chunk 并使迭代失效。ECB 的命令按记录顺序回放，但其他系统在回放前仍可能看到旧结构，因此要用 System Group 明确“标记—回放—清理”的顺序。

高频开关（眩晕、暂停、是否需要处理）使用 `IEnableableComponent`，低频且代表实体类型改变的状态（例如从 `Projectile` 变成 `Pickup`）才添加/移除组件。读取 `Target.Value` 前使用 `state.EntityManager.Exists(target)` 或相应的 Lookup 检查；目标销毁后应清除引用或改为无目标状态。

### 21.5 Jobs、Burst 与并行安全练习

同一移动逻辑的三个版本应保持完全相同的输入、实体数量和测量方式。`Run` 用于基线和调试；`Schedule` 适合不可安全分片或工作量较小的 Job；`ScheduleParallel` 只有在每个实体写入互不重叠时才安全。每次调度都把旧 `state.Dependency` 作为输入并保存返回值；只有主线程必须马上读取输出时才 `Complete()`。

多名攻击者写同一 `Health` 是数据竞争。正确答案是每个攻击者写独立 `DamageEvent`，再按目标聚合，最后由一个阶段写生命值。`NativeArray` 适合已知长度，`NativeList` 适合可增长结果，`NativeParallelHashMap` 适合并行生产且键不重复（或使用明确的并行写入 API）。创建者负责释放，并保证 Job 完成后再释放容器；不能把 `Allocator.Temp` 数据跨帧保存。

### 21.6 Buffer、Blob 与 Shared Component 练习

每个单位挂一个 `DynamicBuffer<ActiveBuff>` 保存可变长度的运行时 Buff；Buff 定义（名称、基础数值、图标索引）放在 Blob Asset 中，由多个实体只读共享。Tick 系统倒序遍历并删除到期元素，避免索引移动导致跳过；计算系统把基础属性与 Buff 修正汇总后写入派生组件，例如 `EffectiveMoveSpeed`。

`IComponentData` 适合固定字段，Buffer 适合每个实体长度不同的列表，Blob 适合不可变且层级复杂的共享配置，Shared Component 适合确实需要按值分组/排序的数据。Shared 值数量过多会产生大量 Archetype/Chunk，降低批量查询效率，因此不要把唯一 ID 或不断变化的数值放进 Shared Component。

### 21.7 系统通信、事件和更新顺序练习

把战斗组放入 `SimulationSystemGroup`，按 `TargetSearch → AttackRequest → DamageResolve → DeathMark → Cleanup` 排序。攻击系统只产生事件，不直接调用伤害系统；事件可以是短生命周期 Event Entity，也可以是集中 Buffer。前者查询直观但实体创建/销毁成本更高，后者实体少但需要设计并行追加和消费边界。事件消费完成后必须在明确阶段清空或销毁，防止下一帧重复结算。

### 21.8 空间搜索练习

暴力方案对每个单位检查所有候选，复杂度是 `O(n²)`；Uniform Grid 将坐标除以格子尺寸得到整数 Cell Key，仅检查自身及相邻格子，平均候选数近似固定，复杂度接近 `O(n + k)`。网格构建必须先于目标搜索，目标销毁后要在验证阶段过滤失效 Entity。实验必须同时记录搜索耗时和命中正确性；只变快但漏目标不是有效优化。

### 21.9 性能报告参考答案

报告至少固定：Unity/Entities/Burst 版本、CPU/GPU、实体数量、分辨率、是否开启安全检查、是否包含渲染、采样时长和预热方式。分别记录主线程、Worker Thread、渲染线程和同步等待；同一场景比较 MonoBehaviour、主线程 ECS、Job ECS、并行 Burst ECS 四个版本。结论应写成“在 50,000 实体时 MovementSystem 从 8.2 ms 降到 2.1 ms，Presentation 占 6.4 ms，下一步优化渲染”，而不是“感觉更快”。Editor 帧率只能用于功能检查，最终数据应来自 Development/Release Player，并说明异常值和重复次数。

### 21.10 GameObject/ECS 混合架构练习

输入由一个 `MonoBehaviour` 读取并写入 `PlayerInputState` 单例，模拟系统只读该组件；统计系统把实体数量聚合到 `SimulationStatistics` 单例，HUD 每帧或每 0.1 秒读取一次。相机、菜单和复杂主角表现保留 GameObject；大量移动、冷却和伤害留在 ECS。桥接只同步表现需要的字段，且规定单向数据流，避免数万实体与 GameObject 每帧双向复制。

### 21.11 十八项综合自测的标准答案

1. **Entity 与 GameObject：** Entity 是带索引/版本的身份，数据存于组件并按 Archetype/Chunk 批处理；GameObject 是带层级、Transform、MonoBehaviour 生命周期和大量面向对象能力的场景对象。
2. **按访问模式划分组件：** 同一系统经常一起顺序读取的数据放在相关组件；不同频率、不同生命周期或不同写入者的数据分开，避免无效搬运和伪共享。
3. **Archetype、Chunk、Structural Change：** 组件组合决定 Archetype，同组合实体放入 Chunk；增删组件或改变 Shared 值会迁移实体，因而不是普通字段赋值。
4. **`ISystem`/`SystemAPI.Query`：** `ISystem` 是值类型系统；Query 描述组件读写集合，`RefRO` 只读、`RefRW` 可写，系统无需保存实体引用。
5. **Authoring/Baker：** Authoring 面向 Inspector，Baker 在编辑/导入时把它转换为非托管 ECS 数据，并声明资产依赖。
6. **Prefab/SubScene：** SubScene 触发场景 Baking；Prefab 经 Baker 变成 Entity Prefab，运行时用 ECB/实体 API 实例化。
7. **ECB：** 在遍历期间记录结构命令，在指定 ECB System 的安全时机回放，从而避免迭代器失效。
8. **Enableable：** 高频启停且仍属于同一数据形状的状态使用 Enableable；实体类型或低频结构变化使用增删组件。
9. **`IJobEntity`：** 当循环是同构、非托管且有足够工作量时使用；先写正确主线程版本，再调度并行版本。
10. **Job Dependency：** 后续 Job 读取前序写入时必须依赖其 JobHandle；保存 `state.Dependency` 才能让调度器构建正确流水线。
11. **并行写冲突：** 多线程写同一组件、同一 Buffer 索引或同一 HashMap 键会竞争；用事件、分区所有权或归并阶段解决。
12. **Buffer/Blob：** Buffer 是实体私有可变列表；Blob 是共享、不可变、可 Burst 读取的配置；二者都不等于托管 `List`。
13. **事件通信：** 生产系统写事件数据，消费系统按顺序读取并清理，系统之间不直接调用业务方法。
14. **更新顺序：** 用 System Group、`UpdateBefore/After` 表达真正的数据依赖，不依赖文件名或创建顺序。
15. **空间划分：** 网格/空间哈希把全量候选缩小为邻域候选，先建立索引再查询，并验证目标有效性。
16. **混合桥接：** 输入、UI、相机和成熟表现保留 GameObject；桥接通过少量单例/聚合数据连接，不做逐实体全量双向同步。
17. **Profiler：** 先定位具体系统、Job、同步点或渲染瓶颈，再决定数据布局、算法或并行化方向。
18. **量化优化：** 固定硬件、版本、实体数和采样窗口，比较基线与改动后的中位数/百分位耗时，报告收益及新瓶颈。

如果某题只能背出 API 名称而无法说明“谁写数据、何时写、谁消费、怎样测量”，就还没有完成该题；应回到对应章节重做最小样例。
