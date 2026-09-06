# 圣杯战争模块边界

## 范围

本次玩家成员迁移的行为合同见 [Servant_Colony_Migration.md](design/Servant_Colony_Migration.md)。本文旧 Guest/驻点自治描述已被替代：从者使用玩家派系与原版工作/控制流程，`ServantColonyMembership` 只负责绑定与旧档成员转换，周期存在状态校正不再改变派系或旅行 Lord。游戏验收进度仍以 PROJECT_STATUS 为准。

本文档是 `MoonWorld` 内圣杯战争 MVP 的实现契约。它冻结模块依赖方向，但不会提前搭建第二阶段系统。按用户确认的临时接入方案，`HolyGrailWarTest` 作为内容依赖：它拥有种族、身份外观、装备和渲染初始化，MoonWorld 通过桥接 Def 与 XML 组件补丁接入契约、魔力和自治。原型源码与安装文件仍由原型项目维护；待内容开发结束后再评估正式迁移。

## 依赖规则

当前开战切片补充（优先于以下历史范围）：`ServantSummoningService` 拥有首召事务与最终开战提交；`EnemyWarPreparation` 负责敌方场外 Pawn、内容初始化、契约与初始 Site 的准备和回滚，并为已开战旧档提供一次性补齐。`EnemyWarPartyService` 只部署既有从者及处理离图保留。`HolyGrailWarContentBridge` 窄调用内容依赖自身的身份/装备初始化，不修改原型；`Site_WarWorkshop` 只保存位置和所属御主，不持有 Pawn 容器、魔力或胜负副本，暂不开放地图。最小胜负继续沿用已验收代码，未来多阵营 Quest 另行迁移。

新增范围仅为已批准的七职阶定义与一组敌方主从交战测试：目前 Saber/Archer 互为对席，其余为空；不扩展到据点、自动突袭、休整再袭或战争胜负。敌方通过独立调试入口部署，用户游戏验收前不记为完成。

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
| `Lifecycle` 离图 | 查询契约并验证实际离图队伍完整性 | `ServantDepartureService` | 保存离图队伍副本、传送或强制移动 Pawn |
| `Core` 生理策略 | 从者无寿命与疾病分类 | `ServantPhysiologyPolicy` | 添加或移除 Hediff |
| `Prana` | Need 变化、来源管线、维持、断供、自愈及可治疗状态策略 | `PranaCycleService`、`ServantHealingPolicy` | 除请求生命周期服务外直接改变存在状态 |
| `Combat` | 伤害许可与战败请求 | `IServantDamagePolicy` | 创建特效或直接修改 Need/Hediff |
| `Lifecycle` 成员身份 | 玩家派系加入、旧 Guest 迁移及角色身份保护 | `ServantColonyMembership` | 保存第二份契约或工作/能力数值 |
| `Core` 战争事件记录 | 开战 Tick、本届指定御主与一次常规召唤资格的存档 | `GameComponent_MoonWorld`、`HolyGrailWarEntry` | 角色终身召唤次数、阶段二战争状态机 |
| `Lifecycle` 事件接入与召唤 | 接受者验证、令咒授予、召唤生成与失败清理 | `HolyGrailWarEntryService`、`ServantSummoningService` | 以己方人口数量限制从者、添加额外取得途径 |
| `Core` 敌方身份 | 七职阶及当前对席选择、敌方契约/供魔资格查询 | `HolyGrailWarClassUtility`、`EnemyContractUtility` | 创建空派系注册表或复制契约状态 |
| `Lifecycle` 敌方部署 | 专用敌对派系、场外御主及单从者落地、共享绑定、失败清理及离图保留 | `EnemyWarPartyService` | 玩家成员转换、自动突袭、战争胜负 |
| `Autonomy` 敌方交战 | 从者优先目标策略、原版战斗 Job 与 Lord、测试宝具选点及灵体出口 Job | `EnemyTargetingPolicy`、`JobGiver_EnemyServantAssault`、`LordJob_EnemyWarParty`、`EnemyRetreatUtility` | 免费施法、直接修改魔力或存在状态、接管玩家指令 |
| `Autonomy` | 灵体跟随移动及灵体原版登舱 Job | `SpiritFollowJobPolicy`、`ServantTravelAutonomy` | 改变契约或伤害规则 |
| `Abilities` | 能力消耗、有效性与结算结果 | `Ability_NoblePhantasm`、`NoblePhantasmService` | 自写渲染或直接写入生命周期字段 |
| `Presentation` | Gizmo、声音、VFX 与渲染 | 只消费结果数据 | 充当玩法权威 |
| `Integration` | 接入原版方法的窄 Harmony 补丁，包括按身份过滤 Need | 不提供状态 API | 保存状态或实现跨模块业务流程 |

## 持久数据

只保存以下自定义字段：

```text
GameComponent_MoonWorld.warStartTick
GameComponent_MoonWorld.currentWarEntry -> HolyGrailWarEntry.designatedMaster / regularSummonUsed
HolyGrailWarEntry.playerIdentity / enemyIdentity
HolyGrailWarEntry.enemyMaster / enemyServant / enemyDeployed
CompServantState.master
CompServantState.presenceState
CompMasterPranaControl.supplyThresholdOverride
CompMasterCommandSpells.commandSpellCharges
```

魔力数值继续由 `Need_MasterPrana` 和 `Need_Prana` 保存；断供时长使用原版 Hediff 的 `ageTicks`，灵基受损使用 Hediff 的 severity。运行时递归保护不存档。契约反向索引由查询服务即时重建，不存档。

本届事件资格属于战争接受记录，不附着在 Pawn 生命周期上。原版 Incident / ScenPart 发出同一种 ChoiceLetter；玩家指定回路持有者后调用 `HolyGrailWarEntryService.TryAccept`。原版信件负责拒绝、延后、到期和归档；资格记录独立于信件的删除。正式 `Command_Target` 与调试入口调用同一召唤服务，只有全部成功才消费资格并记录 `warStartTick`。不新增注册表、通用事件框架或多届战争生命周期。参战记录中的 Pawn 引用标明资格接受者及实际敌方参与者，契约仍仅由从者组件保存。

敌方供魔在现有 `PranaCycleService` 内按共享配置执行固定补给；敌方御主不进入玩家魔力库存与分配阶段，敌方从者不转化食物。维持、断供、自愈及宝具费用继续共用既有规则。撤退状态交由原版 Lord 保存，离图原 Pawn 交由 WorldPawns 保留；没有第二套进度、血量或魔力快照。

敌方御主始终以场外 WorldPawn 保留，代表留守工坊，不生成基地地图或加入突袭 Lord。场外敌方契约允许供魔与宝具施法；玩家主从同行和同图施法不因此放宽。敌方从者优先目标通过原版 `LordJob.ValidateAttackTarget` 约束自动开火，自定义 JobGiver 仅选目标和补充接近 Job，射击站位与近战仍调用原版；运行时目标缓存不存档，也不增加 Harmony 补丁。

宝具能力与冷却使用原版 `Pawn_AbilityTracker` / `Ability` 存档。过载使用目标从者身上的 `MW_NoblePhantasmOvercharge` Hediff，不另存御主侧 pending 布尔值或目标引用；无超时、不可叠加，只在下一次成功释放 MoonWorld 宝具时消费。

灵体半透明表现、专用跟随 Job、能否工作、能否攻击等效果均由 `presenceState` 派生，不增加持久字段。

## 静态 Def

`ServantIdentityDef` 只负责角色身份、PawnKind 和引用。可调玩法参数拆分如下：

```text
ServantIdentityDef
  -> ServantResourceProfileDef
  -> ServantAutonomyProfileDef
  -> List<AbilityDef> noblePhantasms
       -> NoblePhantasmExtension（魔力费用、伤害、护甲穿透、过载倍率）

TraitDef + MasterCircuitExtension
  -> MasterCircuitDef
```

新增普通从者时通常只需添加 XML。当前魔力与实体自治各只有一条实现路径，不建立空注册表；出现新的魔力来源、自治类型、索敌策略或宝具解析器时，再为该变化点建立真实接口与 Def 引用，不修改生命周期或伤害代码。

## 权威调用路径

```text
GameComponent Tick
  1. 御主自然回魔
  2. 实体化从者进食转魔
  3. 向所有未湮灭且未满魔的契约从者分配御主安全线以上的魔力
  4. 结算维持消耗与断供
  5. 使用维持线以上的魔力自愈

Pawn.PreApplyDamage
  -> IServantDamagePolicy
  -> 放行或吸收

Pawn_HealthTracker.CheckForStateChange（普通伤害及其延迟健康后果）
  -> IServantDefeatPolicy
  -> IServantLifecycle.TryResolveDefeat

御主死亡 / 地图离开 / 灵体命令 / 战败
  -> IServantLifecycle
```

日常魔力变化由魔力管线负责；宝具单次费用由 `NoblePhantasmService` 写入同一 `Need_Prana`。直接 `Pawn.Kill` 不经过战败处理。战败适配器只在伤害已经通过 `PreApplyDamage` 门控、且原版健康检查将触发倒地或死亡时请求战败转换；生命周期服务负责最低限度修复和状态结算。

测试宝具调用路径：从者自身原版 Ability Gizmo -> 原版选点、施法 Job / Verb -> `Ability_NoblePhantasm.Activate` -> `NoblePhantasmService` 复核、扣费、生成原版 Bomb Explosion、完成原版冷却。成功标志为爆炸初始化与 Ability 记账完成；伤害在后续原版 Tick 中执行，施法者是伤害 instigator。初始化失败销毁本次爆炸并恢复费用、过载和冷却；已播放的瞬时表现不做撤销。御主的过载命令在目标 Hediff 成功添加后，调用御主组件的共同扣令咒方法。测试宝具不桥接 Excalibur。

## 已实现的开发切片

基础切片已经实现：独立 Mod 外壳、静态参数 Def、身份与契约查询、从者状态组件、魔力 Need 管线、从者自然饥饿抑制、伤害白名单、御主死亡清理、无寿命与疾病免疫、睡眠 Need 抑制、中立贵客测试召唤、不会自行离图的 Guest 关系与原版驻点 Lord、御主与魔力调试按钮，以及可拖动的御主供魔安全线 Gizmo。

灵体状态切片已经实现：御主持有的状态命令、同图与实体化落点校验、全 PawnRenderTree 节点 30% 不透明表现、复用 Core 心理不可见机制阻止普通索敌、灵体止血、根据权威状态定期校正派生效果、只跟随御主的专用 Job，以及攻击和能力入口门控。`MW_SpiritForm` 使用原版 Hediff `MoveSpeed` 统计倍率将移动速度提高至 300%；灵体近距离移动调用原版 Pawn 寻路，距离超过自治 Def 阈值时才使用原版位置与传送通知闪现到御主附近，不修改普通 Pawn 寻路、地形或建筑。该切片没有增加持久状态或自定义移速代码。

战败切片已经实现：原版 `CheckForStateChange` 前置适配、普通伤害及失血等延迟后果的倒地/死亡判定、本次致命伤的最低限度修复、战败资源与状态结算，以及灵体状态下的原版倒地/死亡状态检查抑制。若同一次伤害先满足倒地、随后才升级为致死，生命周期服务会继续稳定后续致命变化。致命部位缺失仅移除造成死亡的缺失状态；未缺失的致命伤或失血只降低到刚好不再致死，其他伤口与非致命缺失部位保留。重新实体化同时要求原版 `ShouldBeDead` 和 `ShouldBeDowned` 均为否。直接 `Pawn.Kill` 不经过战败转换，但会先清除灵体派生效果，避免原版尸体持有隐形组件。

战败灵体化不设置独立实体化冷却。御主供魔不受从者实体或灵体形态限制；战败灵体可持续获得供魔并执行自愈，伤势恢复到不会被原版立即判定为倒地或死亡、且当前格可站立后即可实体化。肉体自愈的目标筛选由 `ServantHealingPolicy` 独立负责：普通伤口逐点治疗，背痛等非伤口有害状态按固定魔力消耗调用原版治愈机制，缺失部位及 MoonWorld 系统状态不进入候选。

离图调用边界：取消全部主从强制配对校验，商队、运输舱与边界离图沿用原版条件。`Pawn.ExitMap` 仅保留敌方离场登记 Postfix，不否决原版离图。`ServantDepartureService` 暂保留表现层正在使用的契约身份查询；`ServantTravelAutonomy` 接入原版登舱、集合出口 Duty 与出口 JobGiver，供灵体旅行和“撤离地图”命令调用。灵体跟随/闪现只接受存活、已生成、同地图的御主，且服从从者自己的旅行安排；御主边界离图后，只通过原版出口汇合附近可加入的御主商队，不跨图传送。契约查询通过原版 `PawnsFinder` 覆盖地图容器和世界 Pawn，不新增存档索引；选择名单沿用原版殖民者分组。

顶部小人栏灵体灰显、LordJob 三阶索敌、令咒、宝具迁移及完整跨地图机制继续保持独立边界。实际进度与未验收状态仍以 PROJECT_STATUS 为准。

当前进度、已验收提交、结构债务和下一开发切片统一记录在 [PROJECT_STATUS.md](PROJECT_STATUS.md)。
