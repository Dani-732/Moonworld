using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class IncidentWorker_HolyGrailWarInvitation : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null || !map.IsPlayerHome
                || Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CanAcceptInvitation != true
                || ChoiceLetter_HolyGrailWar.HasPendingInvitation()) return false;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                if (HolyGrailWarEntryService.CanDesignate(pawn)) return true;
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return CanFireNowSub(parms) && ChoiceLetter_HolyGrailWar.Offer(expires: true);
        }
    }
}
