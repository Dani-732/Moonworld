using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class WarReconnaissanceRecord : IExposable
    {
        private HolyGrailWarClassDef seat;
        private ServantIdentityDef identity;
        private int progress;
        private bool servantSeen;
        private bool masterSeen;

        public int Progress => Math.Max(0, Math.Min(100, progress));
        public HolyGrailWarClassDef Seat => seat;
        public ServantIdentityDef Identity => identity;
        public bool ServantSeen => servantSeen;
        public bool MasterSeen => masterSeen;

        public WarReconnaissanceRecord() { }
        internal WarReconnaissanceRecord(EnemyWarParticipant participant)
        {
            seat = participant?.Seat;
            identity = participant?.EnemyIdentity;
        }

        internal bool Matches(EnemyWarParticipant participant)
        { return participant != null && ((seat != null && participant.Seat == seat) || (identity != null && participant.EnemyIdentity == identity)); }
        internal void Add(int amount) { progress = Math.Max(0, Math.Min(100, progress + amount)); }
        internal void MarkServantSeen() { servantSeen = true; }
        internal void MarkMasterSeen() { masterSeen = true; }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref seat, "seat");
            Scribe_Defs.Look(ref identity, "identity");
            Scribe_Values.Look(ref progress, "progress", 0);
            Scribe_Values.Look(ref servantSeen, "servantSeen", false);
            Scribe_Values.Look(ref masterSeen, "masterSeen", false);
        }
    }

    internal static class WarReconnaissanceService
    {
        internal const int ServantReveal = 34, MasterReveal = 67, SiteReveal = 100;

        internal static WarReconnaissanceRecord Find(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        {
            if (participant == null) return null;
            if (war.reconnaissance == null) war.reconnaissance = new List<WarReconnaissanceRecord>();
            WarReconnaissanceRecord record = war.reconnaissance.Find(r => r.Matches(participant));
            if (record == null)
            {
                record = new WarReconnaissanceRecord(participant);
                war.reconnaissance.Add(record);
            }
            return record;
        }

        internal static int Progress(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        {
            if (war == null || participant == null) return 0;
            if (war.CurrentWarEntry?.Participants.IndexOf(participant) == 0) return SiteReveal;
            return Find(war, participant).Progress;
        }

        internal static bool KnowsServant(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        { return war != null && participant != null && (war.CurrentWarEntry?.Participants.IndexOf(participant) == 0
            || Progress(war, participant) >= ServantReveal || Find(war, participant).ServantSeen); }
        internal static bool KnowsMaster(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        { return war != null && participant != null && (war.CurrentWarEntry?.Participants.IndexOf(participant) == 0
            || Progress(war, participant) >= MasterReveal || Find(war, participant).MasterSeen); }
        internal static bool KnowsSite(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        { return war != null && participant != null && (war.CurrentWarEntry?.Participants.IndexOf(participant) == 0 || Progress(war, participant) >= SiteReveal); }

        internal static void Advance(GameComponent_MoonWorld war, EnemyWarParticipant participant, int amount)
        { if (participant != null && war != null && war.CurrentWarEntry?.Participants.IndexOf(participant) != 0) Find(war, participant).Add(amount); }

        internal static void ObserveVisiblePawns(GameComponent_MoonWorld war)
        {
            if (war?.CurrentWarEntry == null) return;
            foreach (var map in Verse.Find.Maps)
            {
                if (map == null || !map.IsPlayerHome || !HasPlayerPawn(map)) continue;
                foreach (var participant in war.CurrentWarEntry.Enemies)
                {
                    var record = Find(war, participant);
                    if (participant.EnemyServant != null && IsVisible(participant.EnemyServant, map)) record.MarkServantSeen();
                    if (participant.EnemyMaster != null && IsVisible(participant.EnemyMaster, map)) record.MarkMasterSeen();
                }
            }
        }

        private static bool IsVisible(Pawn pawn, Map map)
        { return pawn.Spawned && pawn.Map == map && !pawn.Dead && !pawn.Destroyed && !pawn.Position.Fogged(map); }

        private static bool HasPlayerPawn(Map map)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.Faction == Faction.OfPlayer && !pawn.Dead && !pawn.Destroyed) return true;
            return false;
        }
    }
}
