using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ChoiceLetter_Recontract : ChoiceLetter
    {
        private Pawn servant;
        private int offeredDeadline = -1;
        private bool CurrentEpisode => offeredDeadline >= 0
            && servant?.TryGetComp<CompServantState>()?.UnboundUntilTickAbs == offeredDeadline
            && servant.TryGetComp<CompServantState>().Master == null;
        internal static bool HasPending(Pawn pawn)
        {
            foreach (Letter letter in Find.LetterStack.LettersListForReading)
                if (letter is ChoiceLetter_Recontract offer && offer.servant == pawn && offer.CurrentEpisode && !offer.ArchivedOnly && !offer.TimeoutPassed) return true;
            return false;
        }
        internal static void Offer(Pawn pawn)
        {
            if (HasPending(pawn)) return;
            var letter = (ChoiceLetter_Recontract)LetterMaker.MakeLetter("落单从者：重新契约",
                pawn.LabelShortCap + " 已失去契约。新的御主可以继承这名从者的职阶席位。\n\n"
                + "保留原角色、装备、伤势、形态和魔力，不获得新的常规召唤资格。确认前不会改变归属，落单期限继续流逝。"
                + "若选择敌方御主接收己方从者，该从者将离开玩家阵营。",
                MW_DefOf.MW_RecontractOffer, pawn);
            letter.servant = pawn;
            letter.offeredDeadline = pawn.TryGetComp<CompServantState>().UnboundUntilTickAbs;
            letter.StartTimeout(System.Math.Max(1, pawn.TryGetComp<CompServantState>().UnboundUntilTickAbs - GenTicks.TicksAbs));
            Find.LetterStack.ReceiveLetter(letter);
        }
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!ArchivedOnly && !TimeoutPassed && CurrentEpisode)
                {
                    foreach (Pawn master in ServantRecontractService.Masters())
                    {
                        if (!ServantRecontractService.PlayerInvolved(master, servant) || ServantRecontractService.Rejection(master, servant) != null) continue;
                        Pawn candidate = master;
                        yield return new DiaOption("与 " + candidate.LabelShortCap + " 重签" + (candidate.Faction != Faction.OfPlayer ? "（转为敌方）" : "（归属玩家）"))
                        {
                            resolveTree = true,
                            action = delegate
                            {
                                if (ArchivedOnly || TimeoutPassed || !CurrentEpisode) return;
                                if (ServantRecontractService.TryRecontract(candidate, servant, true, out string reason)) Find.LetterStack.RemoveLetter(this);
                                else Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                            }
                        };
                    }
                    yield return Option_Postpone;
                    yield return Option_Reject;
                }
                else yield return Option_Close;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref servant, "servant");
            Scribe_Values.Look(ref offeredDeadline, "offeredDeadline", -1);
        }
    }

}
