using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ChoiceLetter_HolyGrailWar : ChoiceLetter
    {
        public static bool HasPendingInvitation()
        {
            foreach (Letter letter in Find.LetterStack.LettersListForReading)
                if (letter is ChoiceLetter_HolyGrailWar invitation
                    && !invitation.ArchivedOnly && !invitation.TimeoutPassed)
                    return true;
            return false;
        }

        public static bool Offer(bool expires)
        {
            if (Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CanAcceptInvitation != true
                || HasPendingInvitation()) return false;
            ChoiceLetter_HolyGrailWar letter = (ChoiceLetter_HolyGrailWar)LetterMaker.MakeLetter(
                "降灵之兆：圣杯战争",
                "遥远的呼唤沿着魔术回路抵达了这片边缘世界。圣杯正在等待一名回应者。\n\n"
                + "你可以指定一名身在基地、拥有魔力回路的自由殖民者接受邀请。只有被指定的人会获得三划令咒，"
                + "以及本届圣杯战争的一次常规召唤资格。其他殖民者不会因此获得召唤资格。\n\n"
                + "接受邀请后仍可自行决定何时召唤。首次成功召唤才会记录战争开幕；召唤失败不会用掉资格。"
                + "成功召唤后，即使英灵湮灭，也不能再次常规召唤。",
                MW_DefOf.MW_HolyGrailWarInvitation, LookTargets.Invalid);
            if (expires) letter.StartTimeout(3 * GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || TimeoutPassed
                    || Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CanAcceptInvitation != true)
                {
                    yield return Option_Close;
                    yield break;
                }
                bool any = false;
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    if (!HolyGrailWarEntryService.CanDesignate(pawn)) continue;
                    any = true;
                    Pawn candidate = pawn;
                    yield return new DiaOption("指定 " + candidate.LabelShortCap + " 接受")
                    {
                        resolveTree = true,
                        action = delegate
                        {
                            if (ArchivedOnly || TimeoutPassed) return;
                            string rejection;
                            if (!HolyGrailWarEntryService.TryAccept(candidate, out rejection))
                            {
                                Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Find.LetterStack.RemoveLetter(this);
                            Messages.Message(candidate.LabelShortCap + " 已接受本届圣杯战争邀请，获得三划令咒与一次常规召唤资格。",
                                candidate, MessageTypeDefOf.PositiveEvent, false);
                        }
                    };
                }
                if (!any)
                {
                    DiaOption unavailable = new DiaOption("当前没有可指定的回路持有者");
                    unavailable.Disable("需要一名身在基地、拥有魔力回路的自由殖民者。");
                    yield return unavailable;
                }
                yield return Option_Postpone;
                yield return Option_Reject;
            }
        }
    }
}
