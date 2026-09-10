# MoonWorld 圣杯战争 UI 交接

日期：2026-09-10

## 本批结果

用户确认了静态预览的方向：红底、金框、魔术师仪式感。已按该预览实现阶段 B 的 RimWorld 原生只读窗口：

- 新增 `Source/Presentation/HolyGrailWarWindow.cs`。
- 在玩家御主的现有 gizmo 区新增“圣杯战争”入口。
- 窗口包含：总览、情报、战况、主从、据点五页。
- 采用原生 `Window`、`Widgets`、`GUIStyle`、色块和细线绘制，不依赖 HTML/CSS 运行时。
- 红底金框视觉使用暗红、金色、象牙、敌对红、未知琥珀、存续青绿和退场灰分层。
- 根据实机截图修正总览页文字错位：标题副行独立布局，席位卡提高行高并重新分配四行信息，滚动视图收窄避免横向溢出。
- 根据卫宫红A立绘的披风色样提高主体为更鲜艳的绯红/猩红色，危险状态改为高饱和魔术师红。
- 情报页继续使用 `WarReconnaissanceService` 的三条独立门槛：从者、御主、据点。
- 敌方未知信息经过 `KnowsServant`、`KnowsMaster`、`KnowsSite` 过滤；窗口不会因为打开详情而绕过侦察门槛。
- 已知从者可打开“英灵档案”，显示职阶、称号、真名/显示名、出典、属性、参数、职阶能力、宝具、人物记录和当前动态状态；玩家自己的从者显示当前魔力，敌方不泄露未解锁身份。
- 新增“英灵图鉴”入口，按七个职阶筛选已安装的七名英灵资料。图鉴是静态资料库，不会因为浏览而解锁战争情报。
- 新增 `Source/Core/ServantLoreDef.cs`、`Source/Presentation/ServantDetailWindows.cs` 与 `1.6/Defs/MW_ServantLore.xml`，静态设定与战斗 Pawn 状态分离。
- 战况页只读取当前 `EnemyBattleSession`、`EnemyChallengeSession` 和终局标记，不伪造历史事件。
- UI 不创建新的战争、Pawn、契约、生命、魔力或存档状态。

静态视觉预览仍保留在：

`docs/design/ui-preview/index.html`

英灵静态设定以官方《Fate/stay night》角色页为依据，参考了 [Fate/stay night 官方角色页](https://www.fatestaynightusa.com/1st/story-chara/chara.html) 与 [Unlimited Blade Works 官方角色页](https://www.fatestaynightusa.com/ubw/chara/)。这些资料只用于图鉴文本，战斗数值和当前状态仍来自 MoonWorld 运行时。

## 已完成验证

- `Tests/run-summoning-tests.ps1`：259 项替身场景通过。
- `Tests/run-runtime-contract-checks.ps1`：28 项 Harmony、类型和运行时契约检查通过。
- 当前 25 个 XML 文件随 RimWorld 1.6 构建和部署处理成功；附带 PowerShell XML 读取在当前环境对既有中文 XML 出现编码解析异常，未将该异常记为源码 XML 通过证据。
- `Source/build.ps1`：RimWorld 1.6 实际编译通过。
- `Source/build.ps1 -Deploy`：部署成功。
- 仓库与 `G:\steam\steamapps\common\RimWorld\Mods\MoonWorld` 的 25 个 DLL/XML 运行文件一致。
- 当前 DLL SHA256：`114B483458BCF54C839DCEEAD8991E0A603F01338E794F56F12E72017F0176D6`。
- `git diff --check` 通过。本批没有修改内容依赖 `HolyGrailWarTest`。

## 真实游戏验收步骤

1. 启动 RimWorld 1.6，加载已有圣杯战争存档，选择拥有令咒的玩家御主。
2. 在御主的 gizmo 区点击“圣杯战争”，确认窗口尺寸、红底金框、金色标题线和左侧页签可读。
3. 依次点击五个页签：总览、情报、战况、主从、据点。
4. 在总览或主从页点击已知英灵的“查看英灵档案”，确认参数、能力、宝具和动态状态排版；自己的从者魔力应为当前值。
5. 点击左侧“图鉴”，切换职阶筛选并打开英灵详情；确认浏览资料不会点亮情报页中的敌方席位。
6. 使用仍未侦察的敌方席位确认：未发现的从者、御主和据点分别显示为未知；打开详情不能泄露 Pawn 名称或地点。
7. 用已确认的从者或御主目击场景重新打开窗口，确认对应单独字段解锁，另一字段仍保持未知。
8. 触发或使用现有调试入口创建野外交战和约战，确认战况页显示当前会话、地点状态和约战期限；约战不会显示为固定突袭。
9. 在有已知工坊和临时约战地点时查看据点页，确认临时地点不显示工坊所有权、重建按钮、额外守军或战利品。
10. 保存、读档后重新打开窗口，确认阶段、侦察进度和身份解锁保持。
11. 在战争结束后重新打开窗口，确认最终席位和结果仍可读，失效会话不会导致窗口报错。

## 尚未由自动化覆盖的边界

- Unity IMGUI 的实际字体、窗口缩放、低分辨率换行和不同语言长度。
- 玩家御主 gizmo 区在所有原版 UI 缩放设置下的入口位置。
- RimWorld 实际窗口中的鼠标点击、滚动、选择 Pawn 和跳转地图体验。
- 真实存档在多个临时地点、旧两阵营战争和终局决战同时存在时的视觉密度。
- 用户是否认可详情页的信息量、颜色对比和红金氛围。

自动化结果不能替代 Unity、真实 Scribe、实际窗口交互或用户验收。新功能目前不能记录为“已验收”。

## 后续闸门

用户完成上述集中游戏验收后，才能记录阶段 B UI 已验收。若验收发现布局或信息门槛问题，优先修正窗口显示层，不改变已经验收的战争业务。战况历史和图片资源属于后续阶段；本批已加入文本宝具与人物设定字段，尚未加入英灵立绘资源。
