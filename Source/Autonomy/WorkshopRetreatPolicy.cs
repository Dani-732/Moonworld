using Verse;

namespace MoonWorld
{
    // Stateless decision hook. A future magecraft module can inspect the master's own abilities here.
    public abstract class WorkshopRetreatPolicy
    {
        public abstract bool ShouldRetreat(Site_WarWorkshop workshop, Pawn master, Pawn servant);
    }

    public sealed class RetreatAfterServantDefeat : WorkshopRetreatPolicy
    {
        public override bool ShouldRetreat(Site_WarWorkshop workshop, Pawn master, Pawn servant)
        {
            return workshop.ServantDefeatedHere;
        }
    }
}
