using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    internal enum WarReportKind
    {
        FieldBattle,
        WorkshopBattle,
        Challenge,
        DirectRaid,
        FinalBattle
    }

    internal enum WarReportState
    {
        Active,
        Completed,
        Cancelled,
        Expired
    }

    // An event index only. Pawn, contract, health and mana remain authoritative elsewhere.
    internal sealed class WarReportRecord : IExposable
    {
        internal int id = -1;
        internal WarReportKind kind;
        internal WarReportState state = WarReportState.Active;
        internal Pawn actorA, actorB;
        internal Pawn masterA, masterB;
        internal Site site;
        internal int startedAtTickAbs;
        internal int endedAtTickAbs = -1;
        internal int rounds;
        internal string resultKey;

        public WarReportRecord() { }

        internal bool IsActive => state == WarReportState.Active;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", -1);
            Scribe_Values.Look(ref kind, "kind", WarReportKind.FieldBattle);
            Scribe_Values.Look(ref state, "state", WarReportState.Active);
            Scribe_References.Look(ref actorA, "actorA");
            Scribe_References.Look(ref actorB, "actorB");
            Scribe_References.Look(ref masterA, "masterA");
            Scribe_References.Look(ref masterB, "masterB");
            Scribe_References.Look(ref site, "site");
            Scribe_Values.Look(ref startedAtTickAbs, "startedAtTickAbs", 0);
            Scribe_Values.Look(ref endedAtTickAbs, "endedAtTickAbs", -1);
            Scribe_Values.Look(ref rounds, "rounds", 0);
            Scribe_Values.Look(ref resultKey, "resultKey", null);
        }
    }

    internal static class WarReportService
    {
        internal static WarReportRecord Start(GameComponent_MoonWorld war, WarReportKind kind,
            Pawn actorA, Pawn actorB, Site site)
        {
            if (war == null) return null;
            if (war.reports == null) war.reports = new List<WarReportRecord>();
            var record = new WarReportRecord
            {
                id = war.nextReportId++,
                kind = kind,
                actorA = actorA,
                actorB = actorB,
                masterA = ServantQuery.Instance.GetMaster(actorA),
                masterB = ServantQuery.Instance.GetMaster(actorB),
                site = site,
                startedAtTickAbs = GenTicks.TicksAbs,
                state = WarReportState.Active
            };
            war.reports.Add(record);
            return record;
        }

        internal static WarReportRecord Find(GameComponent_MoonWorld war, int id)
        {
            return war?.reports?.Find(r => r != null && r.id == id);
        }

        internal static void Finish(GameComponent_MoonWorld war, int id, WarReportState state, string resultKey, int rounds = 0)
        {
            WarReportRecord record = Find(war, id);
            if (record == null || !record.IsActive) return;
            record.state = state;
            record.endedAtTickAbs = GenTicks.TicksAbs;
            record.resultKey = resultKey;
            record.rounds = rounds;
        }

        internal static void FinishForActor(GameComponent_MoonWorld war, Pawn pawn, string resultKey)
        {
            if (war?.reports == null || pawn == null) return;
            foreach (WarReportRecord record in war.reports)
                if (record != null && record.IsActive && (record.actorA == pawn || record.actorB == pawn))
                    Finish(war, record.id, WarReportState.Completed, resultKey, record.rounds);
        }

        internal static void FinishAll(GameComponent_MoonWorld war, WarReportState state, string resultKey)
        {
            if (war?.reports == null) return;
            var active = new List<int>();
            foreach (WarReportRecord record in war.reports)
                if (record != null && record.IsActive) active.Add(record.id);
            foreach (int id in active) Finish(war, id, state, resultKey);
        }

        internal static void Trim(GameComponent_MoonWorld war, int maximum = 80)
        {
            if (war?.reports == null || war.reports.Count <= maximum) return;
            war.reports.Sort((a, b) => a.startedAtTickAbs.CompareTo(b.startedAtTickAbs));
            while (war.reports.Count > maximum) war.reports.RemoveAt(0);
        }

        internal static void Reconcile(GameComponent_MoonWorld war)
        {
            if (war?.reports == null) return;
            foreach (WarReportRecord record in war.reports)
            {
                if (record == null || !record.IsActive) continue;
                bool aGone = IsGone(record.actorA);
                bool bGone = record.actorB != null && IsGone(record.actorB);
                if ((record.actorA != null && aGone && (record.actorB == null || bGone))
                    || (record.kind == WarReportKind.DirectRaid && aGone))
                    Finish(war, record.id, WarReportState.Completed, "参与者已退场", record.rounds);
            }
            Trim(war);
        }

        private static bool IsGone(Pawn pawn)
        {
            if (pawn == null) return true;
            if (pawn.Dead || pawn.Destroyed) return true;
            return pawn.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated;
        }

        internal static string KindLabel(WarReportKind kind)
        {
            switch (kind)
            {
                case WarReportKind.FieldBattle: return "野外交战";
                case WarReportKind.WorkshopBattle: return "工坊交战";
                case WarReportKind.Challenge: return "基地约战";
                case WarReportKind.DirectRaid: return "基地突袭";
                case WarReportKind.FinalBattle: return "圣杯决战";
                default: return "战争事件";
            }
        }

        internal static string StateLabel(WarReportRecord record)
        {
            if (record == null) return "未知";
            switch (record.state)
            {
                case WarReportState.Active: return "进行中";
                case WarReportState.Completed: return record.resultKey ?? "已结束";
                case WarReportState.Expired: return "期限结束";
                case WarReportState.Cancelled: return record.resultKey ?? "已取消";
                default: return "未知";
            }
        }

        internal static string ParticipantLabel(GameComponent_MoonWorld war, Pawn pawn)
        {
            if (pawn == null) return "未知从者";
            EnemyWarParticipant participant = war?.CurrentWarEntry?.FindEnemy(pawn);
            bool player = participant != null && war.CurrentWarEntry.Participants.IndexOf(participant) == 0;
            return player || participant != null && WarReconnaissanceService.KnowsServant(war, participant)
                ? pawn.LabelShortCap : "未知从者";
        }
    }
}
