using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class Harmony_ServantDeparture_ExitPawn
    {
        public static void Prefix(Pawn __instance, out Map __state)
        {
            __state = __instance.Map;
            WorkshopDebugActions.TraceExit(__state, __instance, "离图调用前");
        }

        public static void Postfix(Pawn __instance, Map __state)
        {
            EnemyWarPartyService.RetainDepartedPawn(__instance);
            (__state?.Parent as Site_WarWorkshop)?.NotifyPawnExited(__instance);
            WorkshopDebugActions.TraceExit(__state, __instance, "离图调用后");
        }
    }
}
