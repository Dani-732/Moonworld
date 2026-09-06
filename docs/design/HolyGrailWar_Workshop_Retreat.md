# 工坊撤退与重建

本文规定切片 3 的行为与扩展接口；实际验收状态只记录于 `../PROJECT_STATUS.md`。

## 默认失守判定

当前御主没有独立的魔术战斗系统。在己方工坊内，所属从者进入战败灵体，且御主仍在该图时，默认命令主从撤退。主动灵体化、在别的地图战败、别的阵营从者战败均不触发。本判定使用既有战败结算入口，既有断供最终触发战败的路径同样有效；不增加第二套伤害或魔力判定。

“下令撤退”“实际逃脱”“工坊移除”“阵营淘汰”是不同事实。御主仍可能受伤、倒地、被堵、被俘或死亡，不能因为下令撤退就自动获救。建筑损毁本身不触发阵营淘汰，也不单独作为本轮失守判据。

默认御主使用原版 `LordToil_ExitMap` 寻路离场；从者通过现有敌方 Lord 转入撤退。工坊已下达撤退命令时，灵体改走出口，不再跟随或闪现回仍在地图内的御主。不恢复玩家主从同队或同图离开限制。

## 魔术模块的扩展点

`Autonomy/WorkshopRetreatPolicy.cs` 提供公开抽象类：

```csharp
public abstract bool ShouldRetreat(Site_WarWorkshop workshop, Pawn master, Pawn servant);
```

默认实现 `RetreatAfterServantDefeat` 返回 `workshop.ServantDefeatedHere`。职阶 Def 的 `workshopRetreatPolicy` 配置策略类型，类型必须继承该类并有公开无参构造。未来魔术模块可以读取该御主自己的能力/资源，决定是否仍可守住工坊；MoonWorld 不实现魔术伤害、技能、资源或“魔术师战力”评分。

例如外部模块定义 `YourMagic.WorkshopRetreatPolicy` 后，可对目标职阶使用原版 XML PatchOperationAdd（已有字段时用 Replace）：

```xml
<Operation Class="PatchOperationAdd">
  <xpath>Defs/MoonWorld.HolyGrailWarClassDef[defName="MW_Class_Caster"]</xpath>
  <value>
    <workshopRetreatPolicy>YourMagic.WorkshopRetreatPolicy</workshopRetreatPolicy>
  </value>
</Operation>
```

`xpath` 中的职阶 defName 应以目标 Mod 的实际配置为准。策略实例按职阶缓存，必须无状态；具体御主的魔术状态应由魔术模块自身保存，不放在共享策略字段中。函数只回答是否撤退，不生成/移动 Pawn，不写魔力、契约或战争结果。拒绝撤退时保留当前原版守军职责，后续 Site Tick 会重新询问；一旦批准，本次撤退命令锁定，避免沿途反复切换战斗与逃跑。新建工坊重新开始判定。

本接口有当前实际调用者与默认实现，不提供空注册表。多个工坊、跨工坊撤退目的地及友军救援仍需独立切片。

## 逃脱、地图与重建

1. 窄 Harmony 入口记录原版 `Pawn.ExitMap` 的原地图，执行完原版离图后再核对该 Pawn 已自由进入 WorldPawns，分别记录原御主/从者逃脱。普通 DeSpawn、地图卸载、被俘或被装入容器不计成功逃脱。
2. 已下令撤退而地图仍有本方活着、自由的主从时，保留运行中的地图，供原版寻路继续执行。倒地或无出口者也保留，直到实际离场、被俘或死亡；不会因为玩家先撤走而直接放回场外。测试时需预留可达出口。
3. 主从均成功逃脱，玩家仍在场时保留旧 Site；玩家离开、原版允许卸载地图后移除旧 Site。只有此时仍自由存活、契约有效且该阵营没有其他工坊，才安排重建。直接删除一个普通 Site 不伪造逃脱证明。
4. 等待时间从旧 Site 实际移除时起算。`HolyGrailWarClassDef.workshopRebuildDelayTicks` 默认 `180000`，即 3 天，可由 XML 配置非负值；存档保留已确定的绝对期限，不因读档重新计时。
5. 重建还要求原主从均是自由场外 Pawn，且从者达到既有再战标准：最短休整完成、伤势治愈、魔力达到出战比例且健康允许。没有新治愈、补满、替换或契约绑定。重建检查每 2500 Tick 进行一次，到期不保证立即发生。
6. 在旧位置附近、同一世界层寻找新的有效地块，复用原版简易 Outpost 配置建立一个 Site。保留原主从、装备、伤势、魔力及休整起点。选址无结果或创建失败时保留待重建记录重试，清理部分创建的 Site。
7. 从下令撤退至新 Site 建成期间该方不能发动突袭；其余阵营按原规则活动。任务界面显示“工坊失守，等待休整并重建”。御主死亡或从者死亡/湮灭仍淘汰该方，不能重建复活。

本轮不模拟世界地图上的建筑材料、迁移路程和魔术研究。重建后的工坊仍是原版简易敌对据点；不制作复杂布局、专属机关或敌方魔术能力。

## 状态所有权与兼容

- Site 保存所属御主、守军落地标记、本地战败事件、撤退命令及两个逃脱标记；原版 Lord 保存正在执行的撤退职责。
- `EnemyWarParticipant` 保存 `workshopRebuildAtTickAbs` 和 `lostWorkshopTile`；仍仅引用原 Pawn，既有 Need、契约和休整记录保持权威。
- Quest 只读取状态展示。保留 `GameComponent_MoonWorld` 类型及 `warStartTick` 等旧存档键，不补生旧战争缺少的阵营。
- 旧 Site 缺少本轮标记时按未触发处理，不追溯更新前已经发生的战败。使用新的战败事件验证本功能。
- 宿主测试覆盖事件、策略拒绝/延后批准、撤退职责、灵体退出、逃脱记录、地图保留、读写字段、重建重试与多阵营隔离；原版寻路、WorldPawns 实际交接、Unity 与真实 Scribe 仍需游戏内验收。
