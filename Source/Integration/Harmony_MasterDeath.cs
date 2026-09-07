using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Harmony_MasterDeath
    {
        public static void Prefix(Pawn __instance)
        {
            ServantLifecycleService.Instance.PrepareForVanillaDeath(__instance);
        }

        public static void Postfix(Pawn __instance)
        {
            UnboundServantService.NotifyMasterUnavailable(__instance);
        }
    }
}
