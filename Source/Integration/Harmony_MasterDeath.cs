using System.Collections.Generic;
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
            if (!MasterCircuitUtility.HasCircuit(__instance))
            {
                return;
            }

            // Annihilation invokes Pawn.Kill recursively; each master needs its own enumeration.
            List<Pawn> boundServants = new List<Pawn>();
            ServantQuery.Instance.GetBoundServants(__instance, boundServants);
            foreach (Pawn servant in boundServants)
            {
                ServantLifecycleService.Instance.Annihilate(servant, ServantEndReason.MasterDeath);
            }
        }
    }
}
