using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    public interface IServantAutonomyPolicy
    {
        void Initialize(Pawn servant);
    }

    // Guest status keeps servants autonomous; DefendPoint supplies vanilla long-needs behavior
    // without the timed departure graph used by colony visitors.
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

            Lord lord = servant.GetLord();
            if (lord?.LordJob is LordJob_ServantGuest)
            {
                lord.RemovePawn(servant);
                lord = null;
            }

            if (lord == null)
            {
                Faction lordFaction = servant.Faction ?? Faction.OfPlayer;
                LordMaker.MakeNewLord(
                    lordFaction,
                    new LordJob_DefendPoint(servant.Position, null, null, false, false),
                    servant.Map,
                    new[] { servant });
            }
        }
    }

    // Kept for old save deserialization. Initialize migrates servants away from this LordJob.
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
