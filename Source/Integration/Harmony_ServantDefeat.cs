using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange))]
    public static class Harmony_ServantDefeat
    {
        public static bool Prefix(
            Pawn_HealthTracker __instance,
            Pawn ___pawn,
            Hediff hediff)
        {
            CompServantState state = ___pawn?.TryGetComp<CompServantState>();
            if (state != null && state.DefeatResolutionInProgress)
            {
                return false;
            }
            if (state != null
                && (state.PresenceState == ServantPresenceState.VoluntarySpirit
                    || state.PresenceState == ServantPresenceState.DefeatedSpirit)
                && (__instance.ShouldBeDead() || (!__instance.Downed && __instance.ShouldBeDowned())))
            {
                return false;
            }

            if (!ServantDefeatPolicy.Instance.ShouldIntercept(___pawn, __instance))
            {
                return true;
            }

            return !ServantLifecycleService.Instance.TryResolveDefeat(___pawn, hediff);
        }
    }
}
