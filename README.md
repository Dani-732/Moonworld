# MoonWorld

`MoonWorld` 是独立的 RimWorld 1.6 机制 Mod。当前通过依赖 `Holy Grail War - Servant Test` 使用其从者内容，不修改该 Mod 的源码或安装文件。

加载顺序：Harmony → Humanoid Alien Races → Holy Grail War - Servant Test → MoonWorld。原型已有的 Facial Animation 兼容仍由原型负责；MoonWorld 不增加该项硬依赖。

新召唤使用原型 `GWW_Artoria` 和 `GWW_Emiya`，由原型的生成组件初始化身份、外观和装备，身体隐藏也沿用原型渲染补丁。MoonWorld 通过 `1.6/Defs/MW_HolyGrailWarBridge.xml` 配置魔力及自治，通过 XML 补丁附加契约组件。后续原型外观更新只需更新原型；新增候选须在桥接 Def 中明确配置，不会自动纳入尚在开发中的角色。

此前迁移的 `MW_Artoria`、`MW_Emiya` 及其资源仅保留用于旧存档解析，不再进入召唤池。旧 Pawn 不会自动变成原型种族；验证本次接入请使用新召唤的从者。

目前已实现的圣杯战争 MVP 基础包括：

* 从者身份、契约关系与生命周期状态；
* 御主和从者各自独立的魔力 Need，以及统一的低频结算管线；
* 仅允许英灵攻击伤害从者的白名单门控，并保留实体化时的环境伤害；
* 复用原版任务贵客逻辑的自主从者；
* 由御主控制的灵体化，以全层 30% 不透明、原版移动速度 300% 的形态按原版寻路跟随御主，距离过远时闪现回御主附近，并禁止攻击和能力施放；
* 普通伤害导致倒地或死亡时转入战败灵体化，并只修复维持生命所需的致命损伤；直接处决仍按原版死亡；
* 将殖民者设为测试御主、召唤测试从者和调整魔力的开发者按钮。

编译：

```powershell
./Source/build.ps1
```

模块实现边界见 `docs/HolyGrailWar_Module_Boundaries.md`。

审定后的玩法设计和 MVP 数据契约存放在 `docs/design/`，并纳入 Git 版本管理。

首轮游戏内验证见 `docs/MVP_Smoke_Test.md`。构建脚本不会修改游戏当前启用的 Mod 列表。

编译并同步到 `G:\steam\steamapps\common\RimWorld\Mods\MoonWorld`：

```powershell
./Source/build.ps1 -Deploy
```

该命令只复制 `MoonWorld` 目录，不会改动游戏的启用 Mod 配置。
