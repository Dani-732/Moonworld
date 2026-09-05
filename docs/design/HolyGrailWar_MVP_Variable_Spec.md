# Holy Grail War MVP 变量规格草案

> 用途：供独立评估者审查 HolyGrailWar MVP 的数据模型、原版复用边界与变量完整性。
>
> 本文只定义当前已确认的 MVP 数据归属，不等于实现代码，不提前引入信赖、玩家控制、七职阶池、敌方据点生成等后续系统。

## 1. 已冻结的 MVP 规则

1. 从者按原版 Quest Lodger 契约完全自主，不开放玩家直接移动、工作分配、征召或信赖解锁后的控制权切换；不得按普通 `Faction.OfPlayer` 殖民者生成。
2. 圣杯战争唯一自定义全局进度为开战时刻；战争天数由当前游戏 Tick 派生。
3. 主动灵体化只能由契约御主解除。
4. 御主离开地图时，未湮灭的契约从者必须与御主一起离开；若从者未加入同一离图队伍，则阻止御主离图并给出原因，不传送或强制移动从者。
5. 普通伤害将从者推入原版倒地或死亡时，进入战败灵体化；直接调用 `Pawn.Kill` 进入原版死亡流程，不触发战败。
6. 战败时只修复使 Pawn 无法维持有效生命状态的致命部位损伤；普通伤口和非致命缺失肢体保留。
7. 从者不会患病，且不受寿命、衰老和老年死亡影响。
8. 从者保留原版 `Need_Food` 与原版进食 Job；原版自然饥饿下降关闭，只有进食转魔会降低饥饿值。
9. 仅实体化从者可进食或转魔；魔力未满且饥饿值高于原版寻食边界时，饥饿值缓慢转化为魔力；降至边界后，原版进食 AI 负责补充食物。灵体化期间 `Need_Food` 不变。
10. 御主魔术回路数量决定魔力上限，质量决定自然回复；独立的回路 Trait 通过 `DefModExtension` 引用 `MasterCircuitDef`；魔术师位阶 Trait 仅是身份标签。
11. 御主魔力高于供魔安全线时，仅向未满魔、实体化的契约从者均分溢出魔力；从者满魔即退出均分池。
12. MVP 中只有英灵施放的攻击可以伤害实体化英灵；其他普通伤害在应用前拦截。直接 `Kill` 不拦截。
13. 御主死亡时，其所有未湮灭的契约从者立即湮灭；实体化从者可以承受环境伤害，但免疫疾病、衰老和饥饿值归零的原版后果。

## 2. 数据归属原则

同一事实只能有一个权威存储位置：

| 事实类型 | 唯一归属 |
|---|---|
| 战争开局时间 | `GameComponent` |
| 从者与御主的契约关系、灵体状态、战败冷却 | `CompServantState` |
| 御主当前魔力 | `Need_MasterPrana` |
| 御主回路天赋 | 回路 Trait 的 `DefModExtension` -> `MasterCircuitDef` |
| 当前魔力 | `Need_Prana` |
| 魔力断供进度 | `Hediff_ServantPranaShortage` |
| 灵基受损阶段 | `Hediff_ServantSpiritDamage` |
| 剩余令咒 | `CompMasterCommandSpells` |
| 身份、数值、技能和种族规则 | `ServantIdentityDef` 或共享规则 Def |
| 当前 Job、目标、地图、特效、伤害上下文 | 原版运行时对象，不存档 |

禁止同一数值在多个组件中复制保存。例如不能同时保存 `currentPrana` 和 `Need_Prana.CurLevel`，也不能同时保存 `spiritDamageStage` 和灵基受损 Hediff 的严重度。

## 3. 全局战争组件

建议类型：`HolyGrailWarGameComponent : GameComponent`

| 字段 | 类型 | 存档 | 默认值 | 说明 |
|---|---|---:|---:|---|
| `warStartTick` | `int` | 是 | `-1` | 唯一自定义全局战争进度。`-1` 为未开战；开战时记录 `Find.TickManager.TicksGame`。 |

以下均为推导值，不作为字段存档：

```csharp
bool warIsStarted = warStartTick >= 0;
float warDays = warIsStarted
    ? (Find.TickManager.TicksGame - warStartTick) / 60000f
    : 0f;
```

禁止加入的全局字段：

```csharp
WarPhase warPhase;
int enemyGrowthLevel;
int eventCounter;
bool warWon;
bool warLost;
List<Pawn> allServants;
```

战争胜负和敌方据点生命周期在后续由原版 Quest / Site 生命周期承载，不在 MVP 中创建第二套全局状态机。

## 4. 从者持久状态组件

建议类型：`CompServantState : ThingComp`

### 4.1 持久字段

| 字段 | 类型 | 存档 | 默认值 | 说明 |
|---|---|---:|---:|---|
| `master` | `Pawn` | 是，`Scribe_References` | `null` | 契约御主。此字段是唯一持久的主从关系来源。 |
| `presenceState` | `ServantPresenceState` | 是，`Scribe_Values` | `Materialized` | 从者当前存在形式。 |
| `rematerializationReadyTick` | `int` | 是，`Scribe_Values` | `-1` | 战败灵体化的冷却结束绝对 Tick。只在战败灵体状态有意义。 |

```csharp
public enum ServantPresenceState
{
    Materialized,
    VoluntarySpirit,
    DefeatedSpirit,
    Annihilated
}
```

状态含义：

| 状态 | 可攻击/受攻击 | 行为 | 可由御主解除 | 冷却 |
|---|---|---|---|---|
| `Materialized` | 是 | 按自主 AI | 不适用 | 无 |
| `VoluntarySpirit` | 否 | 以专用 Job 无视地形跟随御主 | 是 | 无 |
| `DefeatedSpirit` | 否 | 以专用 Job 无视地形跟随御主 | 否 | 至 `rematerializationReadyTick` |
| `Annihilated` | 否 | 无 | 否 | 永久 |

“重新实体化中”不是持久枚举值。它只是将 `DefeatedSpirit` 转为 `Materialized` 的一次短暂运行时操作，避免存档中出现“已转换但冷却还在”之类的非法组合。

### 4.2 运行时字段，不存档

| 字段 | 类型 | 说明 |
|---|---|---|
| `defeatResolutionInProgress` | `bool` | 防止战败拦截、伤害修复与状态检查发生递归。 |

不应成为组件字段：

```csharp
Map masterMap;
IntVec3 lastPosition;
Pawn currentTarget;
Job currentJob;
Lord currentLord;
bool isSpirit;
bool isDowned;
bool isDead;
int defeatCount;
```

这些内容要么由 `presenceState` 表达，要么能从原版 Pawn、Job、Lord、Map 实时取得。

## 5. 御主魔力与供魔

### 5.1 当前魔力：`Need_MasterPrana`

建议类型：`Need_MasterPrana : Need`

| 字段/属性 | 类型 | 存档 | 说明 |
|---|---|---:|---|
| `CurLevel` | `float` | 是，由 Need 基类保存 | 御主当前魔力，唯一数值来源。 |
| `MaxLevel` | `float` 属性 | 否 | 从御主的 `MasterCircuitDef.maxPrana` 派生。 |

### 5.2 回路天赋：`MasterCircuitDef`

该 Def 是御主的静态天赋配置，不是魔术师位阶 Trait 的替代品。独立的回路 Trait 通过 `DefModExtension` 引用该 Def；每名御主在 MVP 中只保留一个有效回路 Trait，不将该引用复制到 Pawn 组件或存档字段。

| 配置 | 类型 | 说明 |
|---|---|---|
| `maxPrana` | `float` | 回路数量决定的御主魔力上限。 |
| `pranaRegenPerDay` | `float` | 回路质量决定的每日自然回复量；实际周期增量按 `pranaRegenPerDay × intervalTicks / 60000` 派生，不随结算频率改变每日总量。 |
| `supplyThresholdFraction` | `float` | 当前默认供魔安全线占最大魔力的比例，限制在 `0~1`；MVP 由 `MasterCircuitDef` 提供。 |

### 5.3 供魔派生规则

```text
每个统一低频魔力结算周期：
1. 御主自然回魔。
2. 实体化、未满魔且食物储备高于阈值的从者执行进食转魔。
3. 若 CurrentMasterPrana > SupplyThreshold，取得所有未满魔、实体化且 master 指向该御主的从者；将全部溢出魔力在它们之间均分，满魔者立即移出均分池。
4. 处理从者形态维持消耗与断供 Hediff。
5. 仅用高于当前形态维持线的魔力执行自愈。
```

不保存御主侧 `boundServants` 列表；由从者的 `master` 引用建立运行时索引。御主与从者共用一次统一低频魔力结算服务，不设置 `nextMasterPranaUpdateTick` 或独立供魔通道速率字段。

供魔安全线的计算由独立阈值策略提供：`SupplyThreshold = MasterPrana.MaxLevel × Clamp01(GetThresholdFraction(master))`。MVP 只实现读取 `MasterCircuitDef.supplyThresholdFraction` 的默认策略，不在结算服务中写入普通/战斗状态判定。后续可替换策略以支持战斗时 50%、平时 80% 等模式，而不改变魔力结算和均分算法。

### 5.4 御主供魔控制：`CompMasterPranaControl`

该组件只保存玩家对供魔安全线的个人覆盖值，并提供选中御主时的魔力槽 Gizmo；不保存当前魔力，也不执行供魔结算。没有个人覆盖值时实时读取回路 Def 的默认比例，因此调整静态配置可以作用于所有未手动覆盖的御主。

| 字段 | 类型 | 存档 | 说明 |
|---|---|---:|---|
| `supplyThresholdOverride` | `float` | 是 | `-1` 表示没有覆盖值；否则限制在 `0~1`，由槽位指针按 5% 步进调整。 |

供魔槽显示 `Need_MasterPrana` 的当前值，指针显示有效安全线；显示层不接管魔力数值，不依赖 Royalty 的 `Pawn_PsychicEntropyTracker`。当前策略优先读取个人覆盖值，否则读取 `MasterCircuitDef.supplyThresholdFraction`。普通/战斗自动切换仍留给后续策略，不写入组件或结算服务。

## 6. 从者魔力、断供与灵基受损

### 6.1 当前魔力：`Need_Prana`

建议类型：`Need_Prana : Need`

| 字段/属性 | 类型 | 存档 | 说明 |
|---|---|---:|---|
| `CurLevel` | `float` | 是，由 Need 基类保存 | 当前魔力，唯一数值来源。 |
| `MaxLevel` | `float` 属性 | 否 | 从 `ServantIdentityDef.maxPrana` 推导。 |

禁止重复字段：

```csharp
float currentPrana;
float maxPrana;
float pranaPercent;
```

### 6.2 魔力断供：`Hediff_ServantPranaShortage`

| 字段/属性 | 类型 | 存档 | 说明 |
|---|---|---:|---|
| Hediff 是否存在 | 原版状态 | 是，由 HediffSet 保存 | 表示从者当前正处于断供。 |
| `ageTicks` | `int` | 是，由 Hediff 保存 | 记录本次连续断供的持续时间；达到 `shortageDurationTicks` 时结算一次战败。 |

MVP 直接复用原版 Hediff 的 `ageTicks`，不在 `CompServantState` 中另存开始 Tick、经过 Tick 或活动标记；魔力恢复到维持线后移除 Hediff，下一次断供由新 Hediff 从零计时。

断供 Hediff 是否存在由实时条件决定：

```text
Need_Prana.CurLevel < 当前形态的维持线 => 添加/推进断供 Hediff
Need_Prana.CurLevel >= 当前形态的维持线 => 立即移除断供 Hediff
```

不要保存 `shortageElapsedTicks`、`shortageActive` 或第二个倒计时。

### 6.3 灵基受损：`Hediff_ServantSpiritDamage`

| 字段/属性 | 类型 | 存档 | 说明 |
|---|---|---:|---|
| `Severity` | `float` | 是，由 Hediff 保存 | 灵基受损阶段，范围 0 到 4。 |

规则：

```text
Severity 1-3：按 Hediff stage 施加能力惩罚
Severity 4：从者进入 Annihilated，按正式死亡/退场规则移除
```

不保存 `int spiritDamageStage` 或 `bool permanentlyDamaged`。

## 7. 进食转魔

### 7.1 复用的原版状态

| 来源 | 字段/属性 | 存档 | 用途 |
|---|---|---:|---|
| 原版 `Need_Food` | `CurLevel` | 是 | 食物营养储备。 |
| 原版 `Need_Food` / 原版寻食逻辑 | 饥饿边界 | 原版 | 转魔停止并让原版寻食 AI 接管的边界。 |
| 原版 Pawn 食物限制 | `FoodRestriction` | 原版 | 决定允许吃什么或是否禁食。 |

### 7.2 新增静态配置

| 配置 | 类型 | 归属 | 说明 |
|---|---|---|---|
| `foodConversionThreshold` | `float` | `ServantIdentityDef` | 与原版进食 Job 的可用边界同源或严格对齐的转魔停止点。 |
| `foodToPranaRate` | `float` | `ServantIdentityDef` | 每个低频结算周期中，饥饿值转化为魔力的比例或速率。 |

`foodConversionThreshold` 必须与原版进食 Job 的可用边界同源或严格对齐，以免自定义系统与原版进食 Job 使用两套不同阈值；它是静态配置，不保存为运行时状态。

进食转魔条件：

```text
从者处于 Materialized
且 CurrentPrana < MaxPrana
且 Need_Food 高于原版寻食边界
=> 扣减 Need_Food.CurLevel，增加 Need_Prana.CurLevel
```

必须保证：

```text
关闭原版自然饥饿下降
仅进食转魔会自然降低 Need_Food
魔力满额时停止转魔
食物限制为禁食时不创建自定义寻食 Job
```

## 8. 神秘度白名单

MVP 不创建 `hasMystery`、神秘度等级或攻击源注册表。判定只依赖原版伤害上下文：

```text
目标是实体化从者
且 DamageInfo.Instigator 是实体化从者
=> 正常承伤

否则
=> 在原版伤害应用前拦截
```

直接 `Kill` 不进入该拦截。未来其他体系的攻击白名单通过独立 Def 扩展添加，不修改当前 MVP 的存档数据。

## 9. 御主与令咒

建议类型：`CompMasterCommandSpells : ThingComp`

| 字段 | 类型 | 存档 | 默认值 | 说明 |
|---|---|---:|---:|---|
| `commandSpellCharges` | `int` | 是 | `3` | 御主剩余令咒。 |
| `noblePhantasmOverchargePending` | `bool` | 是 | `false` | “宝具过载”已生效，等待目标从者下一次成功释放宝具；MVP 不设超时。 |

令咒 Trait 只表示资格。不要把计数存入 Trait，也不要在御主侧再保存 `boundServant` 引用；从者的 `master` 字段是唯一契约关系来源。

令咒动作必须先验证目标和效果，再扣除计数：

```text
御主有令咒 > 目标是该御主的有效从者 > 效果可以执行 > 扣除 1 划
```

## 10. 身份与系统静态配置

### 10.1 `ServantIdentityDef`

现有身份、外观和初始装备字段继续保留。新增玩法配置建议如下：

| 配置 | 类型 | 说明 |
|---|---|---|
| `maxPrana` | `float` | 最大魔力。 |
| `materializedUpkeepPerDay` | `float` | 实体化每日维持消耗。 |
| `spiritUpkeepMultiplier` | `float` | 灵体化消耗倍率，当前规则为 `0.25`。 |
| `materializedSustainThreshold` | `float` | 实体化维持线。 |
| `spiritSustainThreshold` | `float` | 灵体化维持线。 |
| `shortageDurationTicks` | `int` | 断供到灵基受损的时长。 |
| `foodToPranaRate` | `float` | 食物转魔速率。 |
| `foodConversionThreshold` | `float` | 转魔停止点；必须与原版进食 Job 的可用边界同源或严格对齐。 |
| `healingMaxPerInterval` | `float` | 单次自愈最大恢复量。 |
| `pranaPerHealingPoint` | `float` | 每单位自愈消耗魔力。 |
| `rematerializationCooldownTicks` | `int` | 战败后冷却时长。 |
| `noblePhantasmDefs` | `List<AbilityDef>` | 从者可用宝具定义。 |
| `targetPriorityPolicy` | 枚举/Def | 英灵 > 御主 > 普通敌人/建筑的索敌策略。 |

### 10.2 共享系统配置

不因英灵不同而变化的值应放在单独的共享配置或代码常量中，而不是复制到每个英灵 Def：

| 配置 | 类型 | 说明 |
|---|---|---|
| `pranaUpdateIntervalTicks` | `int` | 统一低频结算周期，覆盖魔力、进食转魔、维持、断供与自愈，建议 60 至 250 Tick。 |
| `spiritDamageMaxStage` | `int` | 固定为 4。 |

### 10.3 英灵共通种族规则

以下是所有英灵的固定规则，不应为每个 Pawn 保存布尔字段：

```text
不会获得疾病 Hediff
不受年龄增长、寿命、老年疾病和老年死亡影响
原版自然饥饿下降关闭
原版饥饿导致的营养不良与死亡后果失效
```

实现应以“该 Pawn 是否为从者”的身份判定统一过滤，不保存：

```csharp
bool diseaseImmune;
bool ageless;
bool malnutritionImmune;
```

## 11. 运行时派生规则，不产生新变量

| 规则 | 派生条件 |
|---|---|
| 能否由御主解除主动灵体化 | `presenceState == VoluntarySpirit`，御主有效，双方当前同图，且从者当前格满足原版 `Standable`。 |
| 战败灵体能否实体化 | `presenceState == DefeatedSpirit`，当前 Tick 到达 `rematerializationReadyTick`，当前格满足原版 `Standable`，且保留伤势不会立即导致倒地或死亡。 |
| 御主能否离开地图 | 契约从者已湮灭，或从者已加入同一离图队伍；否则阻止离图并提示原因。 |
| 普通伤害是否触发战败 | 从者非湮灭、非灵体，且该伤害将导致原版倒地或死亡。 |
| 直接 `Pawn.Kill` 是否触发战败 | 否，直接进入原版死亡流程。 |
| 普通伤害能否伤及实体化从者 | 仅当 `DamageInfo.Instigator` 为实体化从者；否则在原版伤害应用前拦截。 |
| 御主死亡后的从者状态 | 所有 `master` 指向该御主且未湮灭的从者立即湮灭。 |
| 是否可被敌方选定/攻击 | 仅实体化从者可被正常选定；灵体状态由从者状态模块拦截。 |
| 是否可工作、攻击、施放宝具 | 仅实体化从者，且由自主 AI / 当前 Job 决定。 |
| 灵体状态执行何种 Job | 有效御主同图时只执行专用跟随 Job；约 4 格外按原版移动速度逐格接近御主，不检查地形、门或建筑的可通行性，也不改变沿途物体；御主暂时无有效地图目标时原地等待。 |
| 灵体状态如何显示 | 全部 PawnRenderTree 渲染层使用统一 `30%` 不透明度；该值不保存为运行时状态。 |

## 12. 明确不进入 MVP 的字段

下列字段会引入未冻结系统，当前不得创建：

```csharp
float trust;
bool playerControlUnlocked;
bool isPlayerControlled;
Dictionary<SkillDef, float> servantWorkPriority;
List<Pawn> enemyServants;
int enemyStrengthLevel;
int enemyRaidCount;
Site enemyBase;
bool trueNameRevealed;
float leylinePower;
float mysteryLevel;
bool hasMystery;
```

## 13. 后续阶段再评估的设计问题

这些不是遗漏字段，而是尚未冻结的规则；在做对应功能前必须先定案：

1. 战争胜利或失败后，`warStartTick` 保留用于历史显示，还是由 Quest 结束后停用所有战争事件？
2. 从者在 caravan、运输舱、传送、地图撤离时的灵体状态如何表现；尤其是战败冷却中的从者能否随御主同行？
3. 六维参数、额外具备神秘度的攻击来源及其他魔力来源如何扩展，而不改变当前 MVP 的存档模型？
4. 敌对御主被击杀后，其从者的具体退场状态和装备处理规则是什么。

## 14. 审查目标

独立评估时应重点检查：

1. 是否仍有同一事实被多个字段重复保存。
2. 是否有应当交给原版 Need、Hediff、Job、Quest 或 Pawn 的状态被错误自建。
3. 灵体状态、战败修复、处决直死、无病无寿命与原版死亡链是否存在遗漏路径。
4. 原版食物 Job 能否在关闭自然饥饿下降后，仍按指定边界可靠触发。
5. 单从者、单御主关系是否足以满足 MVP，且不会为后续多从者系统造成不可迁移的数据结构。

## 15. 模块调用约束

变量归属之外，调用权同样必须唯一：

```text
CompServantState 只由 ServantLifecycleService 写入
Need_Prana / Need_MasterPrana 只由 PranaCycleService 与宝具消耗服务修改
Hediff_ServantPranaShortage / Hediff_ServantSpiritDamage 只由生命周期或魔力结算服务修改
表现层、Gizmo、VFX、AI 和 Harmony Patch 不得直接改写上述字段
```

跨模块只能通过 `IServantQuery`、`IContractLookup`、`IServantLifecycle`、`IPranaSource`、`IServantDamagePolicy` 等稳定接口协作。完整目录、接口和 Harmony 入口见 [HolyGrailWar_Module_Boundaries.md](../HolyGrailWar_Module_Boundaries.md)。
