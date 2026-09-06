# 职阶与阵营扩展接口

本文定义当前多阵营切片的数据接口与验收边界，不记录游戏验收进度。进度以 `../PROJECT_STATUS.md` 为准。

## 战争运行规则

默认七个 `HolyGrailWarClassDef`，配置见 `1.6/Defs/MW_WarClasses.xml`。首召先等概率抽取有效职阶，再在该职阶池中等概率抽角色。玩家占据抽中席位，其余六席各自从自己的池抽一名英灵，生成独立御主、原版 Faction 和一个简易工坊。

所有敌方准备在同一召唤事务内完成；任一主从或工坊失败则清理本次全部 Pawn、契约、Site、新建派系，不消费资格或写入开战时间。六席的 Pawn 与 Site 均有效才提交。原版 Quest 的显示若单独失败，不再倒销已经提交的战争，而在读档时重试任务接入。

每个敌方阵营拥有独立的准备标记、出击标记和休整起点。一次正式突袭从所有就绪阵营中等概率选一个，只部署该方从者；不同阵营可以同时在地图交战。敌方各派系互相敌对，也敌对玩家。地图上继续复用原版 Lord/Job 和现有从者优先索敌；没有新增场外自动模拟战斗或主动进攻别人工坊的世界 AI。

御主死亡/销毁，或该方唯一从者死亡/销毁/湮灭，会淘汰该方。全部敌方淘汰才胜利，指定玩家御主死亡仍失败。战败灵体、撤离和失去建筑本身不淘汰阵营。未来令咒保留资格、落单从者重签和多契约集合需要单独修改资格规则，本轮不提前启用。

## 最小配置示例

增加一个职阶需要四项 Def：独立派系、职阶、MoonWorld 英灵身份、召唤池。放在后加载扩展 Mod 的 `Defs` 中，不需 C# 注册、不修改枚举、不增加 `MW_DefOf` 字段。示例中的 `YourMod_AvengerPawnKind` 必须替换为实际存在且已接通内容初始化的 PawnKind。

```xml
<Defs>
  <FactionDef ParentName="MW_WarOpposition">
    <defName>YourMod_AvengerFaction</defName>
    <label>复仇者阵营</label>
    <fixedName>复仇者阵营</fixedName>
  </FactionDef>
  <MoonWorld.HolyGrailWarClassDef>
    <defName>YourMod_AvengerClass</defName>
    <label>复仇者</label>
    <oppositionFaction>YourMod_AvengerFaction</oppositionFaction>
    <participatesInWar>true</participatesInWar>
  </MoonWorld.HolyGrailWarClassDef>
  <Def Class="MoonWorld.ServantIdentityDef">
    <defName>YourMod_AvengerIdentity</defName>
    <servantKind>YourMod_AvengerPawnKind</servantKind>
    <classDef>YourMod_AvengerClass</classDef>
    <resourceProfile>MW_TestServantResources</resourceProfile>
    <autonomyProfile>MW_QuestLodgerAutonomy</autonomyProfile>
    <summonable>true</summonable>
  </Def>
  <MoonWorld.ServantSummonPoolDef>
    <defName>YourMod_AvengerPool</defName>
    <classDef>YourMod_AvengerClass</classDef>
    <servants><li>YourMod_AvengerIdentity</li></servants>
  </MoonWorld.ServantSummonPoolDef>
</Defs>
```

- `defName` 是稳定职阶标识；显示名可改，发布后不要随意改 Def 名称。`classDef` 优先于旧 `warClass` 字段。
- `legacyClass` 仅映射原七职阶与旧档。新增职阶省略，默认 `None`，不要给额外职阶复用已有枚举值。
- 每个职阶的 `oppositionFaction` 必须唯一，继承已命名的 `MW_WarOpposition` 父节点以获得隐藏、永久敌对和关闭普通随机突袭等配置。不同职阶不能共用一个 FactionDef。
- `participatesInWar=false` 将该职阶排除于下一次新战争的召唤与敌方准备；不会删除进行中战争的已存参与者。
- 同职阶增加角色：追加身份并加入原池，或新增同 `classDef` 的扩展池。运行时合并、去重；人数增加不提高该职阶的整体抽中概率。
- 每个启用职阶都必须有有效、`summonable=true` 的角色且 PawnKind 有 race。空池不参与玩家随机；任何启用敌方席位缺池则拒绝开战，不能悄悄少生成一个阵营。
- 该接口负责职阶、阵营和召唤池。当前内容生成仍调用 Holy Grail War 的 `GetIdentity/Enforce`，所以新增英灵需先在依赖中实现身份/外观/装备和初始化；任意无桥接的第三方 PawnKind 不会自动获得兼容。仍不迁移依赖资源，不替代其正式宝具结算。

## 状态所有权与旧档

`GameComponent_MoonWorld` 保留类型名、`warStartTick`、`warOutcome` 与当前事件记录。`HolyGrailWarEntry.enemies` 深存 `EnemyWarParticipant` 列表，每项保存稳定职阶 Def、最初身份、原御主/从者引用和该方休整状态。魔力仍在 Need、契约仍在 CompServantState、工坊仍以 OwnerMaster 找所属阵营，未复制角色数值。

Quest 继续提供原版任务 UI 与历史；进行中从 Entry 重建阵营展示，结束时留下最终快照，不成为另一套战争权威。未来所有权迁移须单独设计，不能一部分写 Quest、一部分写 Entry。

旧单敌方键保留读取并转换为一项列表。旧战争不因装入新职阶而增补阵营，不重抽原从者、不补满魔力、不重置休整或开战时间；完全没有敌方记录的旧战争仍走既有单敌方修复路径。完整七阵营需使用本次首召前的存档或新局，八职阶等扩展同样在新战争中生效。

工坊撤退与重建的后续切片见 [工坊撤退与重建](HolyGrailWar_Workshop_Retreat.md)。职阶新增 `workshopRetreatPolicy`（默认战败撤退策略）及 `workshopRebuildDelayTicks`（默认 180000 Tick），允许后续魔术模块决定御主是否继续抵抗；魔术能力本身不由本 Mod 开发。

仍不含：同阵营多个工坊、额外召唤资格、重新签约、多届战争、内容迁移和正式宝具结算迁移。
