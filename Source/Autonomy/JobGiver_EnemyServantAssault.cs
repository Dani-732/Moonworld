using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    public sealed class JobGiver_EnemyServantAssault : JobGiver_AIFightEnemies
    {
        protected override Thing FindAttackTarget(Pawn pawn)
        {
            return (pawn.GetLord()?.LordJob as LordJob_EnemyWarParty)?.GetPreferredTarget(pawn)
                ?? base.FindAttackTarget(pawn);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            Job combat = base.TryGiveJob(pawn);
            if (combat != null) return combat;
            Pawn target = (pawn.GetLord()?.LordJob as LordJob_EnemyWarParty)?.GetPreferredTarget(pawn);
            if (target == null) return null;
            if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
                wait.expiryInterval = 250;
                wait.checkOverrideOnExpire = true;
                return wait;
            }
            Job approach = JobMaker.MakeJob(JobDefOf.Goto, target);
            approach.expiryInterval = 250;
            approach.checkOverrideOnExpire = true;
            return approach;
        }
    }
}
