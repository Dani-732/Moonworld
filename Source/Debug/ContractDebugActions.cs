using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class ContractDebugActions
    {
        [DebugAction("MoonWorld", "契约调试：失格与重签", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Open()
        {
            var entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            if (entry == null) return;
            var choices = new List<FloatMenuOption>();
            foreach (var participant in entry.Participants)
            {
                var selected = participant;
                choices.Add(new FloatMenuOption(participant.Seat?.label + " / " + participant.EnemyServant?.LabelShortCap
                    + " / 御主 " + participant.EnemyMaster?.LabelShortCap, () => OpenParticipant(selected)));
            }
            choices.Add(new FloatMenuOption("立即检查重签候选（保留玩家确认）", () =>
            {
                UnboundServantService.Tick();
                ServantRecontractService.Tick();
            }));
            Find.WindowStack.Add(new FloatMenu(choices));
        }

        private static void OpenParticipant(EnemyWarParticipant participant)
        {
            var choices = new List<FloatMenuOption>
            {
                new FloatMenuOption("移除该御主令咒，触发正式失契", () =>
                {
                    Pawn master = participant.EnemyMaster;
                    Hediff mark = CommandSpellService.Mark(master);
                    if (mark != null) master.health.RemoveHediff(mark);
                    Messages.Message("已处理令咒移除；未产出可移植令咒。", MessageTypeDefOf.NeutralEvent, false);
                }),
                new FloatMenuOption("该从者真正退场，保留御主现有令咒", () =>
                    ServantLifecycleService.Instance.Annihilate(participant.EnemyServant, ServantEndReason.ExplicitKill)),
                new FloatMenuOption("查看契约与期限", () =>
                {
                    Pawn servant = participant.EnemyServant;
                    var state = servant?.TryGetComp<CompServantState>();
                    Find.WindowStack.Add(new Dialog_MessageBox("职阶：" + participant.Seat?.label
                        + "\n开战御主：" + participant.OriginalMaster?.LabelShortCap
                        + "\n当前御主：" + (state?.Master?.LabelShortCap.ToString() ?? "无")
                        + "\n最近御主资格：" + CommandSpellService.HasQualification(participant.EnemyMaster)
                        + "\n从者 ID：" + servant?.thingIDNumber + "；存在：" + UnboundServantService.Exists(servant)
                        + "\n落单到期 Tick：" + state?.UnboundUntilTickAbs
                        + "；当前 Tick：" + GenTicks.TicksAbs));
                })
            };
            Find.WindowStack.Add(new FloatMenu(choices));
        }
    }
}
