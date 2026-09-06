using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class ServantDepartureService
    {
        public static bool IsContractServant(Pawn pawn)
        {
            ServantSnapshot snapshot;
            return pawn != null && !pawn.Dead && !pawn.Destroyed
                && ServantQuery.Instance.TryGetSnapshot(pawn, out snapshot)
                && snapshot.presenceState != ServantPresenceState.Annihilated
                && snapshot.master != null && !snapshot.master.Dead
                && snapshot.master.Faction == Faction.OfPlayer
                && pawn.Faction == Faction.OfPlayer && pawn.HostFaction == null
                && !pawn.IsPrisoner && !pawn.IsSlave;
        }
    }
}
