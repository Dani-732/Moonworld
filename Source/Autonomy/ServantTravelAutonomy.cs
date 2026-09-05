using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class ServantTravelAutonomy
    {
        public static void JoinTransporterLord(List<Pawn> selected, List<CompTransporter> transporters, Map map)
        {
            if (transporters.Count == 0) return;
            int group = transporters[0].groupID;
            Lord lord = map.lordManager.lords.Find(candidate =>
                candidate.LordJob is LordJob_LoadAndEnterTransporters loading && loading.transportersGroup == group);
            foreach (Pawn servant in selected)
            {
                if (!ServantDepartureService.IsContractGuest(servant) || servant.Downed || !servant.Spawned) continue;
                if (lord == null)
                    lord = LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_LoadAndEnterTransporters(group), map);
                if (lord.ownedPawns.Contains(servant)) continue;
                servant.GetLord()?.Notify_PawnLost(servant, PawnLostCondition.ForcedToJoinOtherLord);
                lord.AddPawn(servant);
                servant.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
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

        public static void ApplyBoardingDuties(Lord lord, int group)
        {
            foreach (Pawn servant in lord.ownedPawns)
            {
                if (!ServantDepartureService.IsContractGuest(servant)) continue;
                servant.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("EnterTransporter"));
                servant.mindState.duty.transportersGroup = group;
            }
        }
    }
}
