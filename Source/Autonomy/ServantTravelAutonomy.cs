using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class ServantTravelAutonomy
    {
        private sealed class ExitJobGiver : JobGiver_ExitMapBest
        {
            public ExitJobGiver() { failIfCantJoinOrCreateCaravan = true; }
            public Job For(Pawn pawn) => TryGiveJob(pawn);
        }

        private static readonly ExitJobGiver ExitGiver = new ExitJobGiver();

        public static bool CanExitAsPlayer(Pawn servant)
        {
            return ServantDepartureService.IsContractServant(servant) && servant.Spawned
                && CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(servant);
        }

        public static Job GetPlayerExitJob(Pawn servant)
        {
            if (!CanExitAsPlayer(servant)) return null;
            Job exit = ExitGiver.For(servant);
            if (exit != null) exit.playerForced = true;
            return exit;
        }

        public static bool HasTravelAssignment(Pawn servant)
        {
            LordJob job = servant.GetLord()?.LordJob;
            return job is LordJob_FormAndSendCaravan || job is LordJob_LoadAndEnterTransporters;
        }

        public static bool ShouldFollowMasterCaravan(Pawn servant)
        {
            if (!ServantDepartureService.IsContractServant(servant) || !servant.Spawned
                || HasTravelAssignment(servant)
                || !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(servant)) return false;
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            Caravan caravan = master?.GetCaravan();
            // Never follow stale coordinates, another map, or an unrelated caravan.
            return master != null && !master.Spawned && caravan != null
                && CaravanExitMapUtility.FindCaravanToJoinFor(servant) == caravan;
        }

        public static Job GetSpiritTravelJob(Pawn servant)
        {
            Job current = servant.CurJob;
            if (current != null && current.playerForced && current.exitMapOnArrival
                && IsSpiritTravelJobAllowed(servant, current)) return current;
            if (servant.GetLord()?.LordJob is LordJob_FormAndSendCaravan)
            {
                PawnDuty duty = servant.mindState.duty;
                if (duty?.def == DutyDefOf.TravelOrWait && duty.focus.Cell.InBounds(servant.Map))
                {
                    Job travel = JobMaker.MakeJob(JobDefOf.Goto, duty.focus.Cell);
                    travel.expiryInterval = 250;
                    return travel;
                }
            }
            return ShouldFollowMasterCaravan(servant) ? ExitGiver.For(servant) : null;
        }

        public static bool IsSpiritTravelJobAllowed(Pawn servant, Job job)
        {
            if (job.def != JobDefOf.Goto || !job.targetA.Cell.InBounds(servant.Map)) return false;
            if (job.exitMapOnArrival)
                return job.targetA.Cell.OnEdge(servant.Map)
                    && (ShouldFollowMasterCaravan(servant) || (job.playerForced && CanExitAsPlayer(servant)));
            PawnDuty duty = servant.mindState.duty;
            return servant.GetLord()?.LordJob is LordJob_FormAndSendCaravan
                && duty?.def == DutyDefOf.TravelOrWait && job.targetA == duty.focus;
        }

        public static Job GetSpiritBoardingJob(Pawn servant)
        {
            Lord lord = servant.GetLord();
            if (!(lord?.LordJob is LordJob_LoadAndEnterTransporters loading)) return null;
            List<CompTransporter> group = new List<CompTransporter>();
            TransporterUtility.GetTransportersInGroup(loading.transportersGroup, servant.Map, group);
            CompTransporter transporter = JobGiver_EnterTransporter.FindMyTransporter(group, servant);
            if (transporter == null || !servant.CanReach(transporter.parent, PathEndMode.Touch, Danger.Deadly)) return null;
            return JobMaker.MakeJob(JobDefOf.EnterTransporter, transporter.parent);
        }

    }
}
