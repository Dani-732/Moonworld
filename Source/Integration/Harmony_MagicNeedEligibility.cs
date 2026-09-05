using HarmonyLib;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // Vanilla evaluates every NeedDef for every pawn. These two needs are role-specific,
    // so eligibility must be decided here instead of relying on XML defaults.
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Harmony_MagicNeedEligibility
    {
        public static bool Prefix(Pawn ___pawn, NeedDef nd, ref bool __result)
        {
            if (nd == MW_DefOf.MW_Prana)
            {
                __result = ServantQuery.Instance.IsServant(___pawn);
                return false;
            }

            if (nd == MW_DefOf.MW_MasterPrana)
            {
                __result = !ServantQuery.Instance.IsServant(___pawn)
                    && MasterCircuitUtility.HasCircuit(___pawn);
                return false;
            }

            return true;
        }
    }
}
