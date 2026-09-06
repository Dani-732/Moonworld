using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class Harmony_ServantDeparture_ExitPawn
    {
        public static void Postfix(Pawn __instance)
        {
            EnemyWarPartyService.RetainDepartedPawn(__instance);
        }
    }
}
