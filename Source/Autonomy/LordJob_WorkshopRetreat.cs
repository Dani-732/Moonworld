using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    // Native exit duties handle movement, downing and blocked routes. No pawn relocation here.
    public sealed class LordJob_WorkshopRetreat : LordJob
    {
        public override bool AddFleeToil => false;
        public override bool CanAutoAddPawns => false;
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_ExitMap(LocomotionUrgency.Sprint, interruptCurrentJob: true));
            return graph;
        }
    }
}
