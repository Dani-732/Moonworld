using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public enum ServantPresenceState
    {
        Materialized,
        VoluntarySpirit,
        DefeatedSpirit,
        Annihilated
    }

    public enum ServantEndReason
    {
        MasterDeath,
        SpiritDamageLimit,
        ExplicitKill
    }

    public struct ServantSnapshot
    {
        public Pawn servant;
        public Pawn master;
        public ServantPresenceState presenceState;
        public ServantIdentityDef identity;
    }

    public interface IServantQuery
    {
        bool IsServant(Pawn pawn);
        bool TryGetSnapshot(Pawn pawn, out ServantSnapshot snapshot);
        bool IsMaterialized(Pawn pawn);
        bool IsSpirit(Pawn pawn);
    }

    public interface IContractLookup
    {
        Pawn GetMaster(Pawn servant);
        void GetBoundServants(Pawn master, List<Pawn> buffer);
    }

    public interface IServantLifecycle
    {
        bool TryBind(Pawn master, Pawn servant, out string rejection);
        bool TryEnterVoluntarySpirit(Pawn master, Pawn servant);
        bool TryRematerialize(Pawn master, Pawn servant);
        bool TryResolveDefeat(Pawn servant, Hediff triggeringHediff = null);
        bool TryPreserveSpirit(Pawn servant, Hediff triggeringHediff = null);
        void PrepareForVanillaDeath(Pawn servant);
        void Annihilate(Pawn servant, ServantEndReason reason);
    }

    public interface IServantSummoningService
    {
        bool TrySummon(Pawn master, Map map, IntVec3 cell, out Pawn servant, out string rejection);
    }
}
