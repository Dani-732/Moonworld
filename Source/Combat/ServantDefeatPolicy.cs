using Verse;

namespace MoonWorld
{
    public interface IServantDefeatPolicy
    {
        bool ShouldIntercept(Pawn pawn, Pawn_HealthTracker health);
    }

    public sealed class ServantDefeatPolicy : IServantDefeatPolicy
    {
        public static readonly ServantDefeatPolicy Instance = new ServantDefeatPolicy();

        private ServantDefeatPolicy()
        {
        }

        public bool ShouldIntercept(Pawn pawn, Pawn_HealthTracker health)
        {
            if (pawn == null || health == null || health.Dead)
            {
                return false;
            }

            CompServantState state = pawn.TryGetComp<CompServantState>();
            if (state == null
                || state.PresenceState != ServantPresenceState.Materialized
                || state.DefeatResolutionInProgress)
            {
                return false;
            }

            return health.ShouldBeDead() || (!health.Downed && health.ShouldBeDowned());
        }
    }
}
