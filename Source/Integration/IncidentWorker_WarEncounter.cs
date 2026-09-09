using RimWorld;
using Verse;

namespace MoonWorld
{
    // Natural selection only. The direct-raid IncidentDef remains callable without this roll.
    public sealed class IncidentWorker_WarEncounter : IncidentWorker
    {
        public override float BaseChanceThisGame => base.BaseChanceThisGame
            * WarRhythmPolicy.NaturalActionMultiplier(Current.Game?.GetComponent<GameComponent_MoonWorld>());

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return Current.Game?.GetComponent<GameComponent_MoonWorld>()?.enemyChallenge == null
                && EnemyWarPartyService.ValidateRaid(parms.target as Map) == null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            if (!Rand.Chance(WarRhythmPolicy.ChallengeChance(Current.Game?.GetComponent<GameComponent_MoonWorld>())))
                return MW_DefOf.MW_HolyGrailWarEnemyServantRaid.Worker.TryExecute(parms);
            if (EnemyChallengeService.TryStart(parms.target as Map, out string rejection)) return true;
            Log.Warning("[MoonWorld] 本次约战未发起：" + rejection);
            return false;
        }
    }
}
