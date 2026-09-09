using System.Collections.Generic;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class WarReconnaissanceDebugActions
    {
        [DebugAction("MoonWorld", "使魔侦察（简化）", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Open()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (war?.CurrentWarEntry == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var participant in war.CurrentWarEntry.Participants)
            {
                var selected = participant;
                options.Add(new FloatMenuOption((participant.Seat?.label ?? "未知阵营") + "：推进一次侦察",
                    () => { WarReconnaissanceService.Advance(war, selected, WarReconnaissanceService.ServantReveal); Show(war); }));
            }
            options.Add(new FloatMenuOption("查看侦察进度面板", () => Show(war)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Show(GameComponent_MoonWorld war)
        {
            var text = new StringBuilder("战争阶段：").Append(WarRhythmPolicy.PhaseLabel(war));
            foreach (var participant in war.CurrentWarEntry.Participants)
            {
                int progress = WarReconnaissanceService.Progress(war, participant);
                text.Append("\n\n").Append(participant.Seat?.label ?? "未知阵营").Append("：侦察 ")
                    .Append(progress).Append("%");
                if (WarReconnaissanceService.KnowsServant(war, participant))
                    text.Append("\n从者：").Append(participant.EnemyServant?.LabelShortCap ?? "已退场");
                else text.Append("\n从者：未知");
                if (WarReconnaissanceService.KnowsMaster(war, participant))
                    text.Append("\n御主：").Append(participant.EnemyMaster?.LabelShortCap ?? "未知");
                else text.Append("\n御主：未知");
                if (WarReconnaissanceService.KnowsSite(war, participant))
                    text.Append("\n据点：已解锁");
                else text.Append("\n据点：未解锁");
            }
            Find.WindowStack.Add(new Dialog_MessageBox(text.ToString()));
        }
    }
}
