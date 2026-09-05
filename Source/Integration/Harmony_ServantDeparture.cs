using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Dialog_FormCaravan), "CheckForErrors")]
    public static class Harmony_ServantDeparture_FormCheck
    {
        public static bool Prefix(List<Pawn> pawns, ref bool __result)
        {
            string rejection;
            if (ServantDepartureService.CanDepartTogether(pawns, out rejection)) return true;
            Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "CheckForErrors")]
    public static class Harmony_ServantDeparture_LoadCheck
    {
        public static bool Prefix(List<Pawn> pawns, ref bool __result)
        {
            return Harmony_ServantDeparture_FormCheck.Prefix(pawns, ref __result);
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "DebugTryFormCaravanInstantly")]
    public static class Harmony_ServantDeparture_DebugCaravan
    {
        public static bool Prefix(List<TransferableOneWay> ___transferables, ref bool __result)
        {
            return Harmony_ServantDeparture_FormCheck.Prefix(
                TransferableUtility.GetPawnsFromTransferables(___transferables), ref __result);
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "DebugTryLoadInstantly")]
    public static class Harmony_ServantDeparture_DebugTransporters
    {
        public static bool Prefix(List<TransferableOneWay> ___transferables, ref bool __result)
        {
            return Harmony_ServantDeparture_DebugCaravan.Prefix(___transferables, ref __result);
        }
    }

    [HarmonyPatch(typeof(Lord), nameof(Lord.ReceiveMemo))]
    public static class Harmony_ServantDeparture_SendCheck
    {
        public static bool Prefix(Lord __instance, string memo)
        {
            if (memo != "ReadyToExitMap" || !(__instance.LordJob is LordJob_FormAndSendCaravan forming)) return true;
            List<Pawn> party = new List<Pawn>(__instance.ownedPawns);
            foreach (Pawn pawn in forming.downedPawns)
                if (JobGiver_PrepareCaravan_GatherDownedPawns.IsDownedPawnNearExitPoint(pawn, forming.ExitSpot))
                    party.Add(pawn);
            string rejection;
            if (ServantDepartureService.CanDepartTogether(party, out rejection)) return true;
            Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
            CaravanFormingUtility.StopFormingCaravan(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
        new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(PlanetTile), typeof(PlanetTile), typeof(bool) })]
    public static class Harmony_ServantDeparture_ExitCaravan
    {
        public static bool Prefix(ref IEnumerable<Pawn> pawns, ref Caravan __result)
        {
            // Materialize once: vanilla callers may pass lazy lists that change during departure.
            List<Pawn> party = new List<Pawn>(pawns);
            pawns = party;
            string rejection;
            if (ServantDepartureService.CanDepartTogether(party, out rejection)) return true;
            Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompLaunchable), nameof(CompLaunchable.CanLaunch))]
    public static class Harmony_ServantDeparture_LaunchCheck
    {
        public static void Postfix(CompLaunchable __instance, ref AcceptanceReport __result)
        {
            string rejection;
            CompTransporter transporter = __instance.parent.GetComp<CompTransporter>();
            if (__result.Accepted && transporter != null
                && !ServantDepartureService.CanLaunchTogether(transporter.TransportersInGroup(__instance.parent.Map), out rejection))
                __result = new AcceptanceReport(rejection);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class Harmony_ServantDeparture_ExitPawn
    {
        public static void Postfix(Pawn __instance)
        {
            EnemyWarPartyService.RetainDepartedPawn(__instance);
        }

        public static bool Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan)
        {
            // Caravans and transporters have already validated the whole party before taking ownership.
            if (!__instance.Spawned || __instance.Dead || __instance.Destroyed) return true;
            string rejection;
            if (ServantDepartureService.CanExitIndividually(__instance, allowedToJoinOrCreateCaravan, out rejection)) return true;
            Messages.Message(rejection, __instance, MessageTypeDefOf.RejectInput, false);
            __instance.pather?.StopDead();
            __instance.jobs?.StopAll();
            return false;
        }
    }
}
