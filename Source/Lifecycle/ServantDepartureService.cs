using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
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

        public static bool CanDepartTogether(IEnumerable<Pawn> departing, out string rejection)
        {
            HashSet<Pawn> party = new HashSet<Pawn>(departing);
            foreach (Pawn carrier in new List<Pawn>(party))
                if (carrier?.carryTracker?.CarriedThing is Pawn carried) party.Add(carried);
            List<Pawn> bound = new List<Pawn>();
            foreach (Pawn pawn in party)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed) continue;
                if (IsContractServant(pawn) && !party.Contains(ServantQuery.Instance.GetMaster(pawn)))
                {
                    rejection = pawn.LabelShortCap + " 必须与契约御主加入同一离图队伍。";
                    return false;
                }
                if (pawn.Faction != Faction.OfPlayer) continue;
                bound.Clear();
                ServantQuery.Instance.GetBoundServants(pawn, bound);
                foreach (Pawn servant in bound)
                {
                    if (!servant.Dead && !servant.Destroyed
                        && servant.TryGetComp<CompServantState>().PresenceState != ServantPresenceState.Annihilated
                        && !party.Contains(servant))
                    {
                        rejection = pawn.LabelShortCap + " 无法离图：请将契约从者 "
                            + servant.LabelShortCap + " 加入同一离图队伍。";
                        return false;
                    }
                }
            }
            rejection = null;
            return true;
        }

        public static bool CanLaunchTogether(IEnumerable<CompTransporter> transporters, out string rejection)
        {
            List<Pawn> loaded = new List<Pawn>();
            if (transporters != null)
            {
                foreach (CompTransporter transporter in transporters)
                    foreach (Thing thing in transporter.innerContainer)
                        if (thing is Pawn pawn) loaded.Add(pawn);
            }
            return CanDepartTogether(loaded, out rejection);
        }

        public static bool CanExitIndividually(Pawn pawn, bool mayJoinCaravan, out string rejection)
        {
            List<Pawn> party = new List<Pawn> { pawn };
            Caravan caravan = pawn.GetCaravan();
            if (caravan == null && mayJoinCaravan && pawn.Spawned)
                caravan = CaravanExitMapUtility.FindCaravanToJoinFor(pawn);
            if (caravan != null) party.AddRange(caravan.PawnsListForReading);
            if (pawn.carryTracker?.CarriedThing is Pawn carried) party.Add(carried);
            return CanDepartTogether(party, out rejection);
        }
    }
}
