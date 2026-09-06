using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Caravan_NeedsTracker), "TrySatisfyFoodNeed")]
    public static class Harmony_ServantCaravanFood
    {
        public static bool Prefix(Pawn pawn)
        {
            return !ServantQuery.Instance.IsSpirit(pawn);
        }
    }

    // Servant hunger is an explicit resource-conversion budget, not a vanilla decay timer.
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.NeedInterval))]
    public static class Harmony_ServantFoodNeed
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !ServantQuery.Instance.IsServant(___pawn);
        }
    }
}
