using System.Collections.Generic;
using LudeonTK;
using Verse;

namespace MoonWorld
{
    public static class EnemyBattleDebugActions
    {
        [DebugAction("MoonWorld", "敌方互攻：调试与定位", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Open()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (war == null) return;
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("查看当前交战", () => Show(war)),
                new FloatMenuOption("跳过开战等待并尝试匹配", () =>
                {
                    bool started = EnemyBattleService.TryStart(war);
                    Show(war, started ? "已发起一场交战。" : "未发起：已有会话、没有合格的敌对从者或没有可用地点。");
                }),
                new FloatMenuOption("尝试发起野外交战", () => Show(war,
                    EnemyBattleService.TryStart(war, true) ? "已发起野外交战。" : "没有可出战双方或可用野外地点。")),
                new FloatMenuOption("尝试发起工坊进攻", () => Show(war,
                    EnemyBattleService.TryStart(war, false) ? "已发起工坊进攻。" : "没有可出战双方、工坊或足够攻坚魔力。")),
                new FloatMenuOption("跳过本回合等待并结算", () =>
                {
                    var battle = war.enemyBattle;
                    if (battle != null && !battle.onMap)
                    {
                        battle.nextRoundTickAbs = GenTicks.TicksAbs;
                        EnemyBattleService.Advance(war);
                    }
                    Show(war);
                }),
                new FloatMenuOption("定位当前交战地点", () =>
                {
                    if (war.enemyBattle?.site != null) CameraJumper.TryJumpAndSelect(war.enemyBattle.site);
                    else Show(war);
                })
            }));
        }

        private static void Show(GameComponent_MoonWorld war, string prefix = null)
        {
            var battle = war.enemyBattle;
            string text = battle == null ? "当前没有敌方互攻会话。" :
                "进攻从者：" + battle.attacker?.LabelShortCap + " #" + battle.attacker?.thingIDNumber
                + "\n防守从者：" + battle.defender?.LabelShortCap + " #" + battle.defender?.thingIDNumber
                + "\n地点：" + (battle.site is Site_WarWorkshop ? "工坊" : "野外") + " #" + battle.site?.ID + "，地块 " + battle.site?.Tile
                + "\n状态：" + (battle.onMap ? "地图真实战斗（场外回合暂停）" : "场外分段交战")
                + "\n已结算回合：" + battle.rounds + " / " + EnemyBattleService.MaximumRounds
                + "\n下次场外回合：" + System.Math.Max(0, battle.nextRoundTickAbs - GenTicks.TicksAbs) + " Tick"
                + "\n进攻方魔力：" + battle.attacker?.needs?.TryGetNeed<Need_Prana>()?.CurLevel.ToString("0.##")
                + "\n防守方魔力：" + battle.defender?.needs?.TryGetNeed<Need_Prana>()?.CurLevel.ToString("0.##");
            Find.WindowStack.Add(new Dialog_MessageBox((prefix == null ? "" : prefix + "\n\n") + text));
        }
    }
}
