using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class ServantTravelAutonomy
    {
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
