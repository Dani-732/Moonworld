using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public sealed class ServantQuery : IServantQuery, IContractLookup
    {
        public static readonly ServantQuery Instance = new ServantQuery();

        private ServantQuery()
        {
        }

        public bool IsServant(Pawn pawn)
        {
            return ServantIdentityUtility.GetIdentity(pawn) != null;
        }

        public bool TryGetSnapshot(Pawn pawn, out ServantSnapshot snapshot)
        {
            snapshot = new ServantSnapshot();
            if (!IsServant(pawn))
            {
                return false;
            }

            CompServantState state = pawn.TryGetComp<CompServantState>();
            if (state == null)
            {
                return false;
            }

            snapshot.servant = pawn;
            snapshot.master = state.Master;
            snapshot.presenceState = state.PresenceState;
            snapshot.rematerializationReadyTick = state.RematerializationReadyTick;
            snapshot.identity = ServantIdentityUtility.GetIdentity(pawn);
            return snapshot.identity != null;
        }

        public bool IsMaterialized(Pawn pawn)
        {
            ServantSnapshot snapshot;
            return TryGetSnapshot(pawn, out snapshot)
                && snapshot.presenceState == ServantPresenceState.Materialized;
        }

        public Pawn GetMaster(Pawn servant)
        {
            ServantSnapshot snapshot;
            return TryGetSnapshot(servant, out snapshot) ? snapshot.master : null;
        }

        public void GetBoundServants(Pawn master, List<Pawn> buffer)
        {
            if (master == null || buffer == null)
            {
                return;
            }

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    ServantSnapshot snapshot;
                    if (TryGetSnapshot(pawn, out snapshot) && snapshot.master == master)
                    {
                        buffer.Add(pawn);
                    }
                }
            }
        }
    }
}
