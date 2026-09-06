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
            HolyGrailWarQuestService.Ensure(this);
            HolyGrailWarQuestService.SyncOutcome(this, notify: false);
        }

        public override void GameComponentTick()
        {
            WarOutcomeService.Tick(this);
            if (Find.TickManager.TicksGame % 2500 == 0) WorkshopRebuildService.Tick(this);
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
        }

        internal bool TrySetWarOutcome(WarOutcome outcome)
        {
            if (warOutcome != WarOutcome.Ongoing || outcome == WarOutcome.Ongoing) return false;
            warOutcome = outcome;
            HolyGrailWarQuestService.SyncOutcome(this, notify: true);
            return true;
        }
    }
}
