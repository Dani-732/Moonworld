using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Harmony_MasterDeath
    {
        private static readonly List<Pawn> boundServants = new List<Pawn>();

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

            boundServants.Clear();
            ServantQuery.Instance.GetBoundServants(__instance, boundServants);
            foreach (Pawn servant in boundServants)
            {
                ServantLifecycleService.Instance.Annihilate(servant, ServantEndReason.MasterDeath);
            }
        }
    }
}
