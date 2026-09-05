using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    // Kept for old save deserialization; colony membership removes this legacy lord.
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
