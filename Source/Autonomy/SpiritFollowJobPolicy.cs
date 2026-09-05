using RimWorld;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public static class SpiritFollowJobPolicy
    {
        public static Job CreateJob(Pawn servant)
        {
            Job boarding = ServantTravelAutonomy.GetSpiritBoardingJob(servant);
            if (boarding != null) return boarding;
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            if (CanFollow(servant, master))
            {
                return JobMaker.MakeJob(MW_DefOf.MW_SpiritFollow, master);
            }

            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 250;
            return wait;
        }

        public static bool IsAllowed(Pawn servant, Job job)
        {
            if (servant == null || job == null)
            {
                return false;
            }
            if (job.def == JobDefOf.EnterTransporter)
            {
                Job boarding = ServantTravelAutonomy.GetSpiritBoardingJob(servant);
                return boarding != null && boarding.targetA == job.targetA;
            }
            if (job.def == JobDefOf.Wait)
            {
                return !CanFollow(servant, ServantQuery.Instance.GetMaster(servant));
            }
            if (job.def != MW_DefOf.MW_SpiritFollow)
            {
                return false;
            }

            Pawn master = ServantQuery.Instance.GetMaster(servant);
            return CanFollow(servant, master) && job.targetA.Thing == master;
        }

        private static bool CanFollow(Pawn servant, Pawn master)
        {
            return servant != null
                && master != null
                && !servant.Dead
                && !master.Dead
                && servant.Spawned
                && master.Spawned
                && servant.Map == master.Map;
        }
    }
}
