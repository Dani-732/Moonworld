using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace MoonWorld
{
    public enum WarOutcome
    {
        Ongoing,
        PlayerVictory,
        PlayerDefeat
    }

    public sealed class GameComponent_MoonWorld : GameComponent
    {
        public int warStartTick = -1;
        private HolyGrailWarEntry currentWarEntry;
        private WarOutcome warOutcome = WarOutcome.Ongoing;
        internal Quest warQuest;
        internal EnemyBattleSession enemyBattle;
        internal EnemyChallengeSession enemyChallenge;
        internal int enemyBattleNextStartTickAbs = -1;
        internal bool finalBattleTriggered;
        internal List<WarReconnaissanceRecord> reconnaissance = new List<WarReconnaissanceRecord>();
        internal int nextReportId;
        internal List<WarReportRecord> reports = new List<WarReportRecord>();
        internal bool holyGrailRewardGranted;
        internal bool holyGrailRewardSpawned;
        internal bool holyGrailWishMade;
        internal bool holyGrailMaterializationGranted;
        internal int holyGrailServantDeadlineTickAbs = -1;

        public HolyGrailWarEntry CurrentWarEntry => currentWarEntry;
        public WarOutcome CurrentWarOutcome => warOutcome;
        public bool CanAcceptInvitation => currentWarEntry == null && warStartTick < 0;

        public GameComponent_MoonWorld(Game game) { }

        public override void LoadedGame()
        {
            ServantColonyMembership.ReconcileLoadedGame();
            // Old saves have no invitation record. A started war must not grant another summon.
            if (currentWarEntry == null && warStartTick >= 0)
                currentWarEntry = new HolyGrailWarEntry(null, alreadySummoned: true);
            EnemyWarPreparation.ReconcileLoadedWar(this);
            currentWarEntry?.ResolveLegacyPlayerServant();
            UnboundServantService.Tick();
            HolyGrailWarQuestService.Ensure(this);
            HolyGrailWarQuestService.SyncOutcome(this, notify: false);
            if (warOutcome == WarOutcome.PlayerVictory) HolyGrailEndingService.OnVictory(this);
            if (enemyBattle != null) EnemyBattleService.Advance(this);
            EnemyChallengeService.Tick(this);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                UnboundServantService.Tick();
                if (enemyBattle != null) EnemyBattleService.Advance(this);
                EnemyChallengeService.Tick(this);
                WarReconnaissanceService.ObserveVisiblePawns(this);
                HolyGrailEndingService.Tick(this);
            }
            if (Find.TickManager.TicksGame % 2500 == 0) ServantRecontractService.Tick();
            WarOutcomeService.Tick(this);
            if (Find.TickManager.TicksGame % 250 == 0) WarReportService.Reconcile(this);
            if (Find.TickManager.TicksGame % 2500 == 0)
            {
                EnemyBattleService.Tick(this);
                WorkshopRebuildService.Tick(this);
                WarFinalBattleService.Tick(this);
            }
            int interval = Mathf.Max(1, MW_DefOf.MW_HolyGrailWarSettings.pranaUpdateIntervalTicks);
            if (Find.TickManager.TicksGame % interval == 0)
            {
                PranaCycleService.Execute(interval);
            }
        }

        public void RecordWarStartIfNeeded()
        {
            if (warStartTick < 0)
            {
                warStartTick = Find.TickManager.TicksGame;
            }
        }

        internal void AcceptInvitation(Pawn master)
        {
            if (!CanAcceptInvitation)
                throw new System.InvalidOperationException("本届圣杯战争已经指定御主。");
            currentWarEntry = new HolyGrailWarEntry(master);
        }

        internal void CommitRegularSummon()
        {
            if (currentWarEntry == null || currentWarEntry.RegularSummonUsed)
                throw new System.InvalidOperationException("本届常规召唤资格不可用。");
            RecordWarStartIfNeeded();
            currentWarEntry.ConsumeRegularSummon();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref warStartTick, "warStartTick", -1);
            Scribe_Deep.Look(ref currentWarEntry, "currentWarEntry");
            Scribe_Values.Look(ref warOutcome, "warOutcome", WarOutcome.Ongoing);
            Scribe_References.Look(ref warQuest, "warQuest");
            Scribe_Deep.Look(ref enemyBattle, "enemyBattle");
            Scribe_Deep.Look(ref enemyChallenge, "enemyChallenge");
            Scribe_Values.Look(ref enemyBattleNextStartTickAbs, "enemyBattleNextStartTickAbs", -1);
            Scribe_Values.Look(ref finalBattleTriggered, "finalBattleTriggered", false);
            Scribe_Collections.Look(ref reconnaissance, "reconnaissance", LookMode.Deep);
            Scribe_Values.Look(ref nextReportId, "nextReportId", 0);
            Scribe_Collections.Look(ref reports, "reports", LookMode.Deep);
            Scribe_Values.Look(ref holyGrailRewardGranted, "holyGrailRewardGranted", false);
            Scribe_Values.Look(ref holyGrailRewardSpawned, "holyGrailRewardSpawned", false);
            Scribe_Values.Look(ref holyGrailWishMade, "holyGrailWishMade", false);
            Scribe_Values.Look(ref holyGrailMaterializationGranted, "holyGrailMaterializationGranted", false);
            Scribe_Values.Look(ref holyGrailServantDeadlineTickAbs, "holyGrailServantDeadlineTickAbs", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && reconnaissance == null)
                reconnaissance = new List<WarReconnaissanceRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && reports == null)
                reports = new List<WarReportRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                foreach (WarReportRecord report in reports)
                    if (report != null && report.id >= nextReportId) nextReportId = report.id + 1;
            }
        }

        internal bool TrySetWarOutcome(WarOutcome outcome)
        {
            if (warOutcome != WarOutcome.Ongoing || outcome == WarOutcome.Ongoing) return false;
            warOutcome = outcome;
            WarReportService.FinishAll(this, WarReportState.Completed,
                outcome == WarOutcome.PlayerVictory ? "玩家赢得圣杯战争" : "玩家退出圣杯战争");
            HolyGrailWarQuestService.SyncOutcome(this, notify: true);
            if (outcome == WarOutcome.PlayerVictory) HolyGrailEndingService.OnVictory(this);
            return true;
        }
    }
}
