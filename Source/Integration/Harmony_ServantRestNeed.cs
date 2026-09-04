using HarmonyLib;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // Servants do not use the vanilla rest need; keeping it full also prevents sleep jobs.
    [HarmonyPatch(typeof(Need_Rest), nameof(Need_Rest.NeedInterval))]
    public static class Harmony_ServantRestNeed
    {
        public static bool Prefix(Need_Rest __instance, Pawn ___pawn)
        {
            if (!ServantQuery.Instance.IsServant(___pawn))
            {
                return true;
            }

            __instance.CurLevel = __instance.MaxLevel;
            return false;
        }
    }
}
