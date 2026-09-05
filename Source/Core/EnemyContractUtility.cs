using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class EnemyContractUtility
    {
        public static bool IsWarPawn(Pawn pawn)
        {
            return pawn?.Faction != null && pawn.Faction.def == MW_DefOf.MW_WarOpposition;
        }

        public static bool HasEnemyContract(Pawn servant)
        {
            if (servant == null) return false;
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            return IsWarPawn(servant) && IsWarPawn(master) && servant.Faction == master.Faction;
        }

        public static bool CanReceiveSupply(Pawn servant)
        {
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            return HasEnemyContract(servant) && master != null && !master.Dead && !master.Destroyed
                && !master.IsPrisoner && !master.IsSlave && !servant.IsPrisoner && !servant.IsSlave
                && !servant.Dead && !servant.Destroyed && servant.Spawned
                && servant.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated;
        }
    }
}
