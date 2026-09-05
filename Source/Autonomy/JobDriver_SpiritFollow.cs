using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public sealed class JobDriver_SpiritFollow : JobDriver
    {
        private const float FollowDistance = 4f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            Toil follow = ToilMaker.MakeToil("SpiritFollow");
            follow.tickAction = FollowTick;
            follow.defaultCompleteMode = ToilCompleteMode.Never;
            yield return follow;
        }

        private void FollowTick()
        {
            Pawn master = job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (!CanContinueFollowing(master))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (pawn.Position.InHorDistOf(master.Position, FollowDistance))
            {
                return;
            }

            IntVec3 nextCell = StepToward(pawn.Position, master.Position);
            if (!nextCell.InBounds(pawn.Map))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            bool diagonal = nextCell.x != pawn.Position.x && nextCell.z != pawn.Position.z;
            int moveTicks = Math.Max(1, (int)Math.Ceiling(
                diagonal ? pawn.TicksPerMoveDiagonal : pawn.TicksPerMoveCardinal));
            if (!pawn.IsHashIntervalTick(moveTicks))
            {
                return;
            }

            pawn.rotationTracker.FaceCell(nextCell);
            pawn.Position = nextCell;
            pawn.Notify_Teleported(false, false);
        }

        private bool CanContinueFollowing(Pawn master)
        {
            return ServantQuery.Instance.IsSpirit(pawn)
                && master != null
                && !master.Dead
                && pawn.Spawned
                && master.Spawned
                && pawn.Map == master.Map;
        }

        private static IntVec3 StepToward(IntVec3 origin, IntVec3 destination)
        {
            return new IntVec3(
                origin.x + Math.Sign(destination.x - origin.x),
                origin.y,
                origin.z + Math.Sign(destination.z - origin.z));
        }
    }
}
