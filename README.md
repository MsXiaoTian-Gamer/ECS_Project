# ECS_Project

Unity DOTS/ECS（Data-Oriented Technology Stack）学习与实践项目，基于 **Unity 6 + Entities 6.4** 搭建，演示从 Authoring / Baker 烘焙数据，到 Job System 并行处理与 EntityCommandBuffer 安全修改实体的完整 ECS 开发链路。

## 环境要求

| 组件 | 版本 |
| --- | --- |
| Unity Editor | 6000.4.10f1（Unity 6） |
| Entities | 6.4.0 |
| Entities Graphics | 6.4.0 |
| Universal RP | 17.4.0 |
| Input System | 1.19.0 |

> 首次打开请让 Unity 完成包解析与 Shader 编译；场景使用 Sub Scene 承载 ECS 内容。

## 项目结构

```
Assets/
├── Scenes/
│   ├── Basic.unity                  # ECS 演示主场景
│   └── Basic/New Sub Scene.unity    # Sub Scene（实体烘焙数据）
└── Scripts/Basic/
    ├── Authoring/                   # MonoBehaviour -> Entity 转换入口
    │   ├── MovementAuthoring.cs     # 移动组件烘焙（Baker）
    │   └── SpawnAuthoring.cs        # 生成逻辑烘焙
    ├── MoveEntity/                  # 基础组件与标签
    │   ├── MoveCompent.cs           # 移动速度等数据组件
    │   ├── TagComponent.cs          # 实体标记组件
    │   └── EnemyCountSystem.cs      # 敌人数统计系统
    ├── Job/                         # IJobEntity / 并行 Job 示例
    │   └── MoveJob.cs
    └── System/                      # ISystem / SystemBase 示例
        ├── MoveSystem.cs            # 位置更新
        ├── SpawnSystem.cs           # 实体生成
        ├── JobSystem.cs             # Job 化系统
        ├── LifeSystem.cs            # 生命周期管理
        └── DeathSystem.cs           # 死亡清理（EntityCommandBuffer）
```

## 涉及的核心概念

- **Authoring + Baker**：用普通 MonoBehaviour 做编辑器输入，运行时烘焙为 ECS 组件
- **Component / Tag**：数据与标记分离，查询按需匹配
- **System 调度**：ISystem 与 SystemBase 两种写法对照
- **Job System**：并行遍历实体，提升大批量实体处理性能
- **EntityCommandBuffer**：遍历中延迟增删实体，避免迭代失效

## 快速开始

1. 使用 Unity Hub 打开本项目（版本需 ≥ 6000.4）
2. 等待 Package Manager 还原依赖、Shader 编译完成
3. 打开 `Assets/Scenes/Basic.unity` 并进入 Play 模式

## 版本控制说明

本仓库通过 `.gitignore` 排除了 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等 Unity 自动生成目录，仅托管源码与工程配置。克隆后首次打开会自动重新生成这些目录。