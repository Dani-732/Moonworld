using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class EnemyContractUtility
    {
        public static bool IsWarPawn(Pawn pawn)
        {
            var def = pawn?.Faction?.def;
            return def != null && (def == MW_DefOf.MW_WarOpposition
                || def == MW_DefOf.MW_WarOpposition_Saber
                || def == MW_DefOf.MW_WarOpposition_Archer
                || def == MW_DefOf.MW_WarOpposition_Lancer
                || def == MW_DefOf.MW_WarOpposition_Assassin
                || def == MW_DefOf.MW_WarOpposition_Caster
                || def == MW_DefOf.MW_WarOpposition_Rider
                || def == MW_DefOf.MW_WarOpposition_Berserker);
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
                && !servant.Dead && !servant.Destroyed && (servant.Spawned || IsResting(servant))
                && servant.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated;
        }

        public static bool IsResting(Pawn servant)
        {
            HolyGrailWarEntry entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            return entry != null && entry.HasEnemyParticipants && !entry.EnemyEliminated
                && servant == entry.EnemyServant && HasEnemyContract(servant)
                && ServantQuery.Instance.GetMaster(servant) == entry.EnemyMaster
                && IsFreeWorldPawn(servant) && IsFreeWorldPawn(entry.EnemyMaster)
                && servant.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated;
        }

        private static bool IsFreeWorldPawn(Pawn pawn)
        {
            return pawn != null && !pawn.Spawned && !pawn.Dead && !pawn.Destroyed
                && !pawn.IsPrisoner && !pawn.IsSlave && pawn.ParentHolder == null
                && Find.WorldPawns.Contains(pawn) && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer);
        }
    }
}
