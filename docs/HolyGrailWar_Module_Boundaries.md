# 圣杯战争模块边界

## 范围

本文档是 `MoonWorld` 内圣杯战争 MVP 的实现契约。它冻结模块依赖方向，但不会提前搭建第二阶段系统。`HolyGrailWarTest` 只是独立的视觉原型，不是本 Mod 的依赖项。

## 依赖规则

```text
Defs -> Core 查询/契约 -> Lifecycle 生命周期
                         -> Prana 魔力管线
                         -> Damage policy / autonomy / noble phantasm resolution
                         -> Presentation 与 Harmony 适配层
```

底层模块不得调用表现层。表现、Gizmo、VFX、AI Worker 或 Harmony 适配器不得直接写入从者状态、Need 或 Hediff。

## 模块划分

| 模块 | 负责内容 | 对外边界 | 禁止承担的职责 |
|---|---|---|---|
| `Core` | 身份查询、契约查询、不可变快照 | `IServantQuery`、`IContractLookup` | 保存可变状态或执行游戏 Tick 逻辑 |
| `Lifecycle` | 契约绑定、实体/灵体/湮灭状态转换及其派生效果 | `IServantLifecycle` | 魔力运算、索敌、VFX |
| `Core` 生理策略 | 从者无寿命与疾病分类 | `ServantPhysiologyPolicy` | 添加或移除 Hediff |
| `Prana` | Need 变化、来源管线、维持、断供、自愈 | `IPranaSource` | 除请求生命周期服务外直接改变存在状态 |
| `Combat` | 伤害许可与战败请求 | `IServantDamagePolicy` | 创建特效或直接修改 Need/Hediff |
| `Autonomy` | Quest Lodger 贵客初始化、原版访客 Lord/Duty、灵体跟随移动与索敌策略 | `IServantAutonomyPolicy` | 改变契约或伤害规则 |
| `Abilities` | 能力消耗、有效性与结算结果 | `INoblePhantasmResolver` | 渲染或直接写入生命周期字段 |
| `Presentation` | Gizmo、声音、VFX 与渲染 | 只消费结果数据 | 充当玩法权威 |
| `Integration` | 接入原版方法的窄 Harmony 补丁，包括按身份过滤 Need | 不提供状态 API | 保存状态或实现跨模块业务流程 |

## 持久数据

只保存以下自定义字段：

```text
GameComponent_MoonWorld.warStartTick
CompServantState.master
CompServantState.presenceState
CompServantState.rematerializationReadyTick
CompMasterPranaControl.supplyThresholdOverride
CompMasterCommandSpells.commandSpellCharges（未来 MVP 切片）
```

魔力数值继续由 `Need_MasterPrana` 和 `Need_Prana` 保存；断供时长使用原版 Hediff 的 `ageTicks`，灵基受损使用 Hediff 的 severity。运行时递归保护不存档。契约反向索引由查询服务即时重建，不存档。

灵体半透明表现、专用跟随 Job、能否工作、能否攻击等效果均由 `presenceState` 派生，不增加持久字段。

## 静态 Def

`ServantIdentityDef` 只负责角色身份、PawnKind 和引用。可调玩法参数拆分如下：

```text
ServantIdentityDef
  -> ServantResourceProfileDef
  -> ServantAutonomyProfileDef
  -> List<NoblePhantasmDef>

TraitDef + MasterCircuitExtension
  -> MasterCircuitDef
```

新增普通从者时通常只需添加 XML。新增魔力来源、索敌策略或宝具时，应实现对应接口并添加一项 Def 引用，而不修改生命周期或伤害代码。

## 权威调用路径

```text
GameComponent Tick
  1. 御主自然回魔
  2. 实体化从者进食转魔
  3. 分配御主安全线以上的魔力
  4. 结算维持消耗与断供
  5. 使用维持线以上的魔力自愈

Pawn.PreApplyDamage
  -> IServantDamagePolicy
  -> 放行或吸收

御主死亡 / 地图离开 / 灵体命令 / 战败
  -> IServantLifecycle
```

魔力管线是正常玩法中写入魔力 Need 的唯一入口。直接 `Pawn.Kill` 不经过战败处理。后续战斗切片只能在原版 `PreApplyDamage` 流程仍有效时请求战败转换。

## 已实现的开发切片

基础切片已经实现：独立 Mod 外壳、静态参数 Def、身份与契约查询、从者状态组件、魔力 Need 管线、从者自然饥饿抑制、伤害白名单、御主死亡清理、无寿命与疾病免疫、睡眠 Need 抑制、中立贵客测试召唤、不会自行离图的原版访客 Lord、御主与魔力调试按钮，以及可拖动的御主供魔安全线 Gizmo。

灵体状态切片已经实现：御主持有的状态命令、同图与实体化落点校验、全 PawnRenderTree 节点 30% 不透明表现、复用 Core 心理不可见机制阻止普通索敌、灵体止血、根据权威状态定期校正派生效果、只跟随御主的专用 Job，以及攻击和能力入口门控。`MW_SpiritForm` 使用原版 Hediff `MoveSpeed` 统计倍率将移动速度提高至 300%；灵体近距离移动调用原版 Pawn 寻路，距离超过自治 Def 阈值时才使用原版位置与传送通知闪现到御主附近，不修改普通 Pawn 寻路、地形或建筑。该切片没有增加持久状态或自定义移速代码。

仍未实现：战败拦截、顶部小人栏灵体灰显、LordJob 三阶索敌、令咒、宝具迁移。从者自治 LordJob 已阻止访客自行离图，但御主与从者的显式地图离开生命周期仍未接入。这些功能保持独立，不会以占位代码塞进无关模块。
