# 新 Session 交接提示词

将下面整段作为新 Session 的第一条消息：

```text
你将继续开发 RimWorld 1.6 Mod：MoonWorld。请始终用中文与我沟通。

工作路径：
- Git 仓库：D:\AGENT工作区\rimworld\MoonWorld
- RimWorld：G:\steam\steamapps\common\RimWorld
- 测试部署：G:\steam\steamapps\common\RimWorld\Mods\MoonWorld
- 旧视觉与角色原型：D:\AGENT工作区\rimworld\HolyGrailWarTest

先执行只读接管：
1. 完整阅读 docs/PROJECT_STATUS.md。
2. 阅读 docs/HolyGrailWar_Module_Boundaries.md、docs/design/HolyGrailWar_Gameplay_Design.md、docs/design/HolyGrailWar_MVP_Variable_Spec.md 和 docs/MVP_Smoke_Test.md 中与下一切片有关的部分。
3. 检查 git status、最近提交和现有源码，不要假定文档一定与代码一致。
4. 复述三件事：当前已经完成什么、本次绝对不做什么、下一切片如何验收。若仓库状态或代码与进度文档冲突，先报告冲突；没有冲突就继续实施，不要重新讨论已经验收的机制。

当前已验收基线：
- main 的已验收提交为 163a4a4 Regenerate chronic servant conditions。
- 机制内核冒烟测试已通过：御主/从者魔力、供魔安全线、Guest 自主驻留、灵体跟随与闪现、战败拦截、直接处决、无睡眠/疾病/寿命、伤口及背痛自愈。
- 当前 MoonWorld 仍只有测试从者和开发者召唤；阿尔托莉雅、卫宫及其视觉内容仍在 HolyGrailWarTest。

下一开发切片：正式召唤与战争启动。

实现要求：
1. 保持模块化，建立唯一召唤服务；正式 Gizmo 和开发者入口都调用它。
2. 召唤服务统一验证御主、地图、落点、当前是否已有未湮灭己方从者，并负责随机候选、Pawn 生成、契约绑定与失败回滚。
3. 保留 GameComponent_MoonWorld 的类型名和 warStartTick 存档键，将战争状态所有权从 PranaCycle.cs 分离；只在召唤和契约全部成功后幂等记录首次开战 Tick。
4. 只迁移阿尔托莉雅和卫宫需要的 Def、纹理、装备及最小初始化逻辑，不整包复制 HolyGrailWarTest，也不修改该原型。
5. 优先沿用 RimWorld 原版机制并减少自写代码。不要未经评估就引入 Humanoid Alien Races 或其他硬依赖；若外观无法在现有依赖范围内正确保留，先给出证据和最小替代方案，再等我确认。
6. 不在本切片实现令咒、宝具、敌方据点、三阶索敌、完整地图离开、信赖控制或阶段二战争状态。
7. 不为了形式提前实现 IPranaSource、自治注册表或其他没有第二个消费者的抽象。新增代码必须有当前调用者。

验收要求：
- 不使用开发者菜单即可从玩家界面发起召唤。
- 有效御主能在有效落点随机召唤阿尔托莉雅或卫宫，且外观、身份和初始装备正确。
- 已有未湮灭己方从者时召唤命令不可用。
- 任一步失败都不残留 Pawn、契约或 warStartTick。
- 第一次成功召唤记录 warStartTick，后续不会覆盖；保存读取后契约与开战时间正确。
- 开发者入口和正式入口使用同一召唤服务。
- 构建通过，部署到 G:\steam\steamapps\common\RimWorld\Mods\MoonWorld，并给出清晰的游戏内冒烟测试步骤。

开发纪律：
- 开工前先运行基线构建。
- 仔细保护用户已有改动，不做无关格式化或重构。
- 分成可独立回退的提交；每批检查 diff、编译并说明未验证边界。
- 游戏内验收由我确认后，更新 docs/PROJECT_STATUS.md 这个唯一进度文档，再推送 main。
```

