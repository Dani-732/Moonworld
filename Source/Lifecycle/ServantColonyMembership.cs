using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class ServantColonyMembership
    {
        // SetFaction would otherwise replace the PawnKind before rebuilding Needs.
        internal static Pawn JoiningServant { get; private set; }

        public static void Initialize(Pawn servant, bool newContract = false)
        {
            ServantSnapshot snapshot;
            if (Faction.OfPlayer == null || servant == null || servant.Dead || servant.Destroyed
                || servant.IsPrisoner || servant.IsSlave
                || !ServantQuery.Instance.TryGetSnapshot(servant, out snapshot)
                || snapshot.presenceState == ServantPresenceState.Annihilated
                || snapshot.master == null || snapshot.master.Dead || snapshot.master.Faction != Faction.OfPlayer)
                return;

            bool legacyGuest = servant.HostFaction == Faction.OfPlayer;
            if (servant.Faction != Faction.OfPlayer && !legacyGuest && !newContract) return;

            Lord lord = servant.GetLord();
            if (lord?.LordJob is LordJob_ServantGuest || (legacyGuest && lord?.LordJob is LordJob_DefendPoint))
                lord.Notify_PawnLost(servant, PawnLostCondition.ForcedToJoinOtherLord);

            if (servant.Faction != Faction.OfPlayer)
            {
                Lord travelLord = lord?.LordJob is LordJob_FormAndSendCaravan
                    || lord?.LordJob is LordJob_LoadAndEnterTransporters ? lord : null;
                Pawn previous = JoiningServant;
                JoiningServant = servant;
                try { servant.SetFaction(Faction.OfPlayer); }
                finally { JoiningServant = previous; }
                if (travelLord != null && !travelLord.ownedPawns.Contains(servant))
                    travelLord.AddPawn(servant);
            }
            else if (servant.HostFaction != null)
            {
                servant.guest.SetGuestStatus(null);
            }

            // Repeated loads and map entries must preserve the player's work priorities.
            servant.workSettings?.EnableAndInitializeIfNotAlreadyInitialized();
        }

        public static void ReconcileLoadedGame()
        {
            // Faction changes can modify map/world pawn lists during enumeration.
            foreach (Pawn servant in new List<Pawn>(PawnsFinder.AllMapsAndWorld_Alive))
            {
                Initialize(servant);
                if (ServantQuery.Instance.IsServant(servant))
                    ServantPresenceEffects.Reconcile(servant);
            }
        }
    }
}
