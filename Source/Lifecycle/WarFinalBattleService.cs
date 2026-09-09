using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    internal static class WarFinalBattleService
    {
        internal static void Tick(GameComponent_MoonWorld war)
        {
            if (war == null || war.finalBattleTriggered || war.CurrentWarOutcome != WarOutcome.Ongoing
                || !WarRhythmPolicy.FinalBattleDue(war) || war.enemyBattle != null || war.enemyChallenge != null) return;
            Map map = Current.Game?.AnyPlayerHomeMap;
            if (map == null || !map.IsPlayerHome) return;
            if (MW_DefOf.MW_HolyGrailWarFinalBattle.Worker.TryExecute(new IncidentParms { target = map, forced = true }))
                war.finalBattleTriggered = true;
        }
    }
}
