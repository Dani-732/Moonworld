using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(TransporterUtility), nameof(TransporterUtility.MakeLordsAsAppropriate))]
    public static class Harmony_ServantTravelAutonomy
    {
        public static void Postfix(List<Pawn> pawns, List<CompTransporter> transporters, Map map)
        {
            ServantTravelAutonomy.JoinTransporterLord(pawns, transporters, map);
        }
    }

    [HarmonyPatch(typeof(LordToil_LoadAndEnterTransporters), nameof(LordToil_LoadAndEnterTransporters.UpdateAllDuties))]
    public static class Harmony_ServantTravelBoardingDuty
    {
        public static void Postfix(LordToil_LoadAndEnterTransporters __instance)
        {
            ServantTravelAutonomy.ApplyBoardingDuties(__instance.lord, __instance.transportersGroup);
        }
    }

    [HarmonyPatch(typeof(CaravanUIUtility), nameof(CaravanUIUtility.AddPawnsSections))]
    public static class Harmony_ServantTravelSection
    {
        public static void Postfix(TransferableOneWayWidget widget, List<TransferableOneWay> transferables)
        {
            ServantTravelSection.Add(widget, transferables);
        }
    }

    [HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.FindThingToLoad))]
    public static class Harmony_ServantTravel_NoHaulingStandingGuest
    {
        public static void Postfix(ref ThingCount __result)
        {
            if (__result.Thing is Pawn servant && !servant.Downed
                && ServantDepartureService.IsContractGuest(servant))
                __result = default(ThingCount);
        }
    }
}
