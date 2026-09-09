using System;
using System.Collections.Generic;
using LudeonTK;
using Verse;

namespace MoonWorld
{
    public static class EnemyChallengeDebugActions
    {
        [DebugAction("MoonWorld", "从者约战：调试与定位", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Open()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (war == null) return;
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("在当前基地附近尝试约战", () => Show(war,
                    EnemyChallengeService.TryStart(Find.CurrentMap, out string reason) ? "约战已发出。" : reason)),
                new FloatMenuOption("查看当前约战", () => Show(war)),
                new FloatMenuOption("定位约战地点", () =>
                {
                    if (war.enemyChallenge?.site != null) CameraJumper.TryJumpAndSelect(war.enemyChallenge.site);
                    else Show(war);
                }),
                new FloatMenuOption("跳过剩余等待并检查到期", () =>
                {
                    if (war.enemyChallenge != null && !war.enemyChallenge.onMap)
                    {
                        war.enemyChallenge.expiresAtTickAbs = GenTicks.TicksAbs;
                        EnemyChallengeService.Tick(war);
                    }
                    Show(war);
                })
            }));
        }

        private static void Show(GameComponent_MoonWorld war, string prefix = null)
        {
            var offer = war.enemyChallenge;
            string text = offer == null ? "当前没有约战。直接突袭事件独立保留。" :
                "约战从者：" + offer.servant?.LabelShortCap + " #" + offer.servant?.thingIDNumber
                + "\n御主：" + offer.master?.LabelShortCap + "\n地点 #" + offer.site?.ID + "，地块 " + offer.site?.Tile
                + "\n状态：" + (offer.onMap ? "赴约交战中" : "等待赴约")
                + "\n原到期时间：" + offer.expiresAtTickAbs + "；剩余 " + Math.Max(0, offer.expiresAtTickAbs - GenTicks.TicksAbs) + " Tick"
                + "\n魔力：" + offer.servant?.needs?.TryGetNeed<Need_Prana>()?.CurLevel.ToString("0.##")
                + "\n逾期不固定突袭；到场或调试不补满资源。";
            Find.WindowStack.Add(new Dialog_MessageBox((prefix == null ? "" : prefix + "\n\n") + text));
        }
    }
}
