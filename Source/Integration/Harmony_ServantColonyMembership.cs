using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ChangeKind))]
    public static class Harmony_ServantColonyMembership_KeepKind
    {
        public static bool Prefix(Pawn __instance)
        {
            return __instance != ServantColonyMembership.JoiningServant;
        }
    }
}
