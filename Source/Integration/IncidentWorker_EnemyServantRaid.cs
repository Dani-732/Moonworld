using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class IncidentWorker_EnemyServantRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            GameComponent_MoonWorld war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            HolyGrailWarEntry entry = war?.CurrentWarEntry;
            if (map == null || !map.IsPlayerHome || entry == null || !entry.RegularSummonUsed
                || entry.EnemyDeployed || entry.EnemyEliminated) return false;
            Pawn master = entry.DesignatedMaster;
            return master != null && !master.Dead && !master.Destroyed && master.Spawned && master.Map == map;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            IntVec3 cell;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map), map, 0f, out cell))
                return false;
            string rejection;
            if (!EnemyWarPartyService.TryDeploy(map, cell, out rejection))
            {
                Log.Warning("[MoonWorld] 敌方突袭事件未能部署：" + rejection);
                return false;
            }
            Messages.Message("敌方从者已从边缘突袭，御主仍留守场外。", MessageTypeDefOf.ThreatBig, false);
            return true;
        }
    }
}
