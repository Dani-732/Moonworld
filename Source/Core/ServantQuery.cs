using System.Collections.Generic;
using RimWorld;
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
            snapshot.identity = ServantIdentityUtility.GetIdentity(pawn);
            return snapshot.identity != null;
        }

        public bool IsMaterialized(Pawn pawn)
        {
            ServantSnapshot snapshot;
            return TryGetSnapshot(pawn, out snapshot)
                && snapshot.presenceState == ServantPresenceState.Materialized;
        }

        public bool IsSpirit(Pawn pawn)
        {
            ServantSnapshot snapshot;
            if (!TryGetSnapshot(pawn, out snapshot))
            {
                return false;
            }

            return snapshot.presenceState == ServantPresenceState.VoluntarySpirit
                || snapshot.presenceState == ServantPresenceState.DefeatedSpirit;
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

            // Includes map-held containers and world pawns without a second persisted contract index.
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
            {
                ServantSnapshot snapshot;
                if (TryGetSnapshot(pawn, out snapshot) && snapshot.master == master && !buffer.Contains(pawn))
                {
                    buffer.Add(pawn);
                }
            }
        }
    }
}
