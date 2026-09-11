# MoonWorld 敌方三阶索敌交接

## 本批实现

敌方从者的自动战斗目标优先级调整为：

1. 同图、敌对、可攻击且实体化的从者。
2. 没有第一类目标时，本届当前有效契约的敌对御主。
3. 两类优先目标均不存在时，回退 RimWorld 原版目标选择。

“御主”只认 `HolyGrailWarEntry` 中当前契约关系对应的御主；历史原御主、失契后的旧御主、仅有魔术回路的普通角色均不获得御主优先级。所有优先目标仍需通过原版同图、敌对、未死亡、未倒地、可自动选中、非心理隐形、未禁用威胁和可抵达或可命中的检查。

该策略仅应用于 `LordJob_EnemyWarParty` 的敌方从者和其测试宝具选点。玩家手动命令、伤害、魔力、契约、派系和撤退规则没有改动。

## 修改位置

- `Source/Core/HolyGrailWarEntry.cs`：增加 `IsCurrentMaster`，区分当前契约御主与历史引用。
- `Source/Autonomy/EnemyTargetingPolicy.cs`：拆分公共战斗有效性、从者目标、御主目标和按阶筛选。
- `Source/Autonomy/LordJob_EnemyWarParty.cs`：缓存失效、原版自动开火过滤和测试宝具选点都应用同一优先级。
- `Tests/EnemyRetreatTests.cs`：补充从者压过御主、御主压过普通 Pawn、无效御主回退原版和测试宝具选点场景。

## 验证结果

- `Tests/run-enemy-retreat-tests.ps1`：36 项敌方 Lord、索敌、撤退和灵体离场场景通过。
- `Source/build.ps1`：RimWorld 1.6 编译通过。
- `Tests/run-runtime-contract-checks.ps1`：28 个 Harmony 目标和注入参数通过，24 份 XML 通过。
- `Tests/run-summoning-tests.ps1`：259 项战争、约战、重签、存档替身与既有回归通过。
- `Source/build.ps1 -Deploy`：已部署到 `G:\steam\steamapps\common\RimWorld\Mods\MoonWorld`；仓库与部署目录 42 个运行文件一致，DLL SHA256 均为 `5FA46C1535BADF47F43F2920DC570D2B63DCF72DC2CAD003EF3405D31904D90D`。

## 游戏内验收

1. 在玩家基地放置一名本届玩家御主、一名普通殖民者和一名实体化玩家从者。
2. 触发敌方从者突袭。预期：敌方从者先追击玩家从者，即使御主或普通殖民者更近。
3. 让玩家从者离场、战败灵体化或无法成为有效目标，保留御主和普通殖民者。预期：敌方从者转而攻击御主。
4. 让御主倒地、离场、失契或无法成为有效目标。预期：敌方从者恢复原版目标选择，可攻击普通殖民者。
5. 为敌方从者提供测试宝具并重复上述三种目标组合。预期：宝具选点遵守相同优先级。
6. 在敌方互攻和圣杯决战各检查一次。预期：不同敌方席位的实体从者优先互相攻击；没有可攻击从者时，才攻击本届有效敌对御主。

用户于 2026-09-11 确认当前合并版本验收通过。本次按整体反馈记录，不追认所有实际寻路、目标竞争与第三方战斗组合逐项通过；当前合并部署 DLL SHA256 为 `0C386F9F1F7BD8A8EFCA09D556146287567ACC831F8EFA4869D557E1033E38ED`。
