using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    public static class Harmony_CommandSpellHealth
    {
        public static void Postfix(Pawn ___pawn, Hediff hediff)
        {
            if (hediff is Hediff_MissingPart || hediff is Hediff_AddedPart || hediff is Hediff_CommandSpell)
                CommandSpellService.ReconcileHealth(___pawn);
        }
    }
}
