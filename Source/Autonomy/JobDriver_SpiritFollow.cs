using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public sealed class JobDriver_SpiritFollow : JobDriver
    {
        private const float DefaultFollowDistance = 4f;
        private const float DefaultTeleportDistance = 10f;
        private const int DefaultTeleportRadius = 2;

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

            ServantAutonomyProfileDef profile = ServantIdentityUtility.GetIdentity(pawn)?.autonomyProfile;
            float followDistance = profile?.spiritFollowDistance ?? DefaultFollowDistance;
            float teleportDistance = profile?.spiritTeleportDistance ?? DefaultTeleportDistance;
            if (!pawn.Position.InHorDistOf(master.Position, teleportDistance))
            {
                TeleportNear(master, profile?.spiritTeleportRadius ?? DefaultTeleportRadius);
                return;
            }
            if (pawn.Position.InHorDistOf(master.Position, followDistance))
            {
                if (pawn.pather.Moving)
                {
                    pawn.pather.StopDead();
                }
                return;
            }

            if (!pawn.pather.Moving || pawn.pather.Destination.Thing != master)
            {
                pawn.pather.StartPath(master, PathEndMode.OnCell);
            }
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

        private void TeleportNear(Pawn master, int radius)
        {
            Map map = pawn.Map;
            IntVec3 destination;
            if (!CellFinder.TryFindRandomCellNear(
                master.Position,
                map,
                radius,
                cell => cell != master.Position && cell.Standable(map),
                out destination))
            {
                destination = master.Position;
            }

            pawn.Position = destination;
            pawn.Notify_Teleported(false, true);
        }
    }
}
