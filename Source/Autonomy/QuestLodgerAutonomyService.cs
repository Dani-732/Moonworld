using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    public interface IServantAutonomyPolicy
    {
        void Initialize(Pawn servant);
    }

    // The first slice deliberately reuses vanilla visitor duties while keeping the servant on-map.
    public static class QuestLodgerAutonomyService
    {
        public static void Initialize(Pawn servant)
        {
            if (servant == null || servant.Map == null || Faction.OfPlayer == null)
            {
                return;
            }

            if (servant.guest != null)
            {
                servant.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            }

            if (servant.GetLord() == null)
            {
                Faction lordFaction = servant.Faction ?? Faction.OfPlayer;
                LordMaker.MakeNewLord(
                    lordFaction,
                    new LordJob_ServantGuest(Faction.OfPlayer, servant.Position),
                    servant.Map,
                    new[] { servant });
            }
        }
    }

    // Vanilla LordJob_VisitColony contains the desired guest duties, but its graph also
    // contains timed/conditional paths to LordToil_ExitMap. Servants have no visit duration.
    public sealed class LordJob_ServantGuest : LordJob_VisitColony
    {
        public LordJob_ServantGuest()
        {
        }

        public LordJob_ServantGuest(Faction factionToVisit, IntVec3 chillSpot)
            : base(factionToVisit, chillSpot, null)
        {
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = base.CreateGraph();
            graph.transitions.RemoveAll(transition => transition.target is LordToil_ExitMap);
            return graph;
        }
    }
}
