# MoonWorld 战况与战报交接

## 本批范围

本批按扩展计划切片 6 的下一项，为已存在的敌方互攻、基地约战、直接突袭和第十天圣杯决战增加战报索引，并在“圣杯战争 -> 战况”页显示当前事件与历史记录。

战报只保存事件索引：事件类型、状态、开始/结束绝对 Tick、原 Pawn/Site 引用、回合数和结果标签。生命、魔力、伤势、契约、派系、休整和退场状态仍由原有组件、Need、Hediff、Participant 与服务提供，不在战报中复制。

## 已实现

- `Source/Core/WarReport.cs`：`WarReportRecord`、事件类型/状态、开始/结束、情报遮蔽、活动记录收口和最多 80 条历史裁剪。
- `Source/Core/WarState.cs`：深存 `reports` 与 `nextReportId`；旧档缺字段时初始化空列表，并从已有记录恢复 ID 游标。
- `Source/Core/EnemyBattleSession.cs`：保存 `reportId`；野外交战和工坊交战创建记录，结束时写入回合数与结果。
- `Source/Core/EnemyChallengeSession.cs`：保存 `reportId`；约战建立、过期、撤出、失效分别写入结果。
- `Source/Lifecycle/EnemyBattleService.cs`、`EnemyChallengeService.cs`：将会话生命周期与报告生命周期绑定，失败不会留下活动记录。
- `Source/Integration/IncidentWorker_EnemyServantRaid.cs`：直接突袭独立登记，不改变原入口或部署流程。
- `Source/Integration/IncidentWorker_WarFinalBattle.cs`：圣杯决战登记为一条“所有敌方从者 VS 玩家基地”的总事件。
- `Source/Lifecycle/EnemyWarPartyService.cs`、`WarState.cs`：真实离场、死亡/湮灭和战争结束时收口活动记录。
- `Source/Presentation/HolyGrailWarWindow.cs`：战况页显示活动事件、历史战报、地点、阶段时间、回合数和结果；敌方名称继续遵守从者/御主独立情报解锁。

## 验证结果

- `Source/build.ps1`：RimWorld 1.6 编译通过。
- `Tests/run-summoning-tests.ps1`：259 项场景通过；新增互攻战报参与者/结果、约战过期状态、直接突袭登记检查。
- `Tests/run-runtime-contract-checks.ps1`：28 个 Harmony 目标和注入参数通过；24 份 XML 通过。
- `Source/build.ps1 -Deploy`：已部署到 `G:\steam\steamapps\common\RimWorld\Mods\MoonWorld`。
- 仓库与部署 DLL SHA256 均为 `F4871945A0D290F10E7C0EF76A0F191790F87A4E174A3E5C7E8AC8ECD539BB79`。

## 尚未由本批自动检查覆盖

真实 RimWorld UI 排版、真实 Scribe 读档往返、Unity 地图生成/卸载时序、长时间战报裁剪、第三方 Mod 组合以及游戏内战报文字仍需用户在部署版本中验收。替身测试和运行时合同检查不等同于这些验收。

## 建议的游戏内验收

1. 开启一届战争，打开“圣杯战争 -> 战况”，确认空闲时显示“战争记录”区域。
2. 触发一次野外交战或工坊交战，确认活动记录显示事件类型、双方（未解锁时仍为“未知从者”）、地点和回合数。
3. 让地图交战结束或离图续战，重新打开页面，确认记录转为结束状态并保留结果，不重新生成 Pawn。
4. 触发约战并等待一天，确认历史显示“期限结束”，且没有额外基地突袭。
5. 触发直接突袭和第十天圣杯决战，确认它们各自为独立记录；圣杯决战显示“所有敌方从者 VS 玩家基地”。
6. 在从者/御主实际可见后分别检查情报解锁，确认战报不会因为看到其中一方而自动泄露另一方。

2026-09-10：用户已完成本批游戏内验收并明确反馈“验收通过”。项目状态已同步；自动化检查的边界说明仍适用。
