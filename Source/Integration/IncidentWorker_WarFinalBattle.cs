using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class IncidentWorker_WarFinalBattle : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            return war?.CurrentWarOutcome == WarOutcome.Ongoing && WarRhythmPolicy.FinalBattleDue(war)
                && war.enemyBattle == null && war.enemyChallenge == null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            Map map = parms.target as Map;
            string rejection = null;
            if (!CanFireNowSub(parms) || !EnemyWarPartyService.TryDeployFinalBattle(map, out rejection))
            {
                if (rejection != null) Log.Warning("[MoonWorld] 圣杯决战未能部署：" + rejection);
                return false;
            }
            Messages.Message("圣杯决战开始：所有敌方从者同时突袭玩家基地，彼此互为敌对目标。",
                MessageTypeDefOf.ThreatBig, false);
            if (war != null) war.finalBattleTriggered = true;
            return true;
        }
    }
}
