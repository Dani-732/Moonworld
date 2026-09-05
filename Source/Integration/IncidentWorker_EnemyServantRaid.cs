using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class IncidentWorker_EnemyServantRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return EnemyWarPartyService.ValidateRaid(parms.target as Map) == null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            IntVec3 cell;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null, map, 0f, out cell))
                return false;
            string rejection;
            if (!EnemyWarPartyService.TryDeploy(map, cell, out rejection))
            {
                Log.Warning("[MoonWorld] 敌方突袭事件未能部署：" + rejection);
                return false;
            }
            Pawn servant = Current.Game.GetComponent<GameComponent_MoonWorld>().CurrentWarEntry.EnemyServant;
            Messages.Message("敌方从者已从边缘突袭，御主仍留守场外。", servant, MessageTypeDefOf.ThreatBig, false);
            return true;
        }
    }
}
