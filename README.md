# MoonWorld

`MoonWorld` 是一个全新的 RimWorld 1.6 Mod 根目录，不会覆盖或加载 `HolyGrailWarTest` 的代码。

目前已实现的圣杯战争 MVP 基础包括：

* 从者身份、契约关系与生命周期状态；
* 御主和从者各自独立的魔力 Need，以及统一的低频结算管线；
* 仅允许英灵攻击伤害从者的白名单门控，并保留实体化时的环境伤害；
* 复用原版任务贵客逻辑的自主从者；
* 由御主控制的灵体化，以全层 30% 不透明形态按原版寻路跟随御主，距离过远时闪现回御主附近，并禁止攻击和能力施放；
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
