using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    public static class Harmony_CommandSpellHealth
    {
        public static void Prefix(Pawn ___pawn, out bool __state)
        {
            __state = CommandSpellService.Mark(___pawn) != null;
        }

        public static void Postfix(Pawn ___pawn, Hediff hediff, bool __state)
        {
            if (hediff is Hediff_MissingPart || hediff is Hediff_AddedPart || hediff is Hediff_CommandSpell)
            {
                CommandSpellService.ReconcileHealth(___pawn);
                if (__state) UnboundServantService.NotifyMasterUnavailable(___pawn);
            }
        }
    }
}
