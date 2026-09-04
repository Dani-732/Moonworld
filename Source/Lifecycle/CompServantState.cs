using RimWorld;
using Verse;

namespace MoonWorld
{
    public class CompProperties_ServantState : CompProperties
    {
        public CompProperties_ServantState()
        {
            compClass = typeof(CompServantState);
        }
    }

    // This is the sole persisted owner of a servant's contract and presence state.
    public sealed class CompServantState : ThingComp
    {
        private Pawn master;
        private ServantPresenceState presenceState = ServantPresenceState.Materialized;
        private int rematerializationReadyTick = -1;
        private bool defeatResolutionInProgress;

        public Pawn Master => master;
        public ServantPresenceState PresenceState => presenceState;
        public int RematerializationReadyTick => rematerializationReadyTick;
        public bool DefeatResolutionInProgress => defeatResolutionInProgress;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn != null)
            {
                pawn.needs.AddOrRemoveNeedsAsAppropriate();
                PawnNeedAccess.EnsureNeed(pawn, MW_DefOf.MW_Prana);
                if (master != null)
                {
                    QuestLodgerAutonomyService.Initialize(pawn);
                }
            }
        }

        public void Bind(Pawn newMaster)
        {
            master = newMaster;
        }

        public void SetPresence(ServantPresenceState newState)
        {
            presenceState = newState;
        }

        public void SetRematerializationReadyTick(int tick)
        {
            rematerializationReadyTick = tick;
        }

        public void SetDefeatResolutionInProgress(bool value)
        {
            defeatResolutionInProgress = value;
        }

        public override void PostExposeData()
        {
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref presenceState, "presenceState", ServantPresenceState.Materialized);
            Scribe_Values.Look(ref rematerializationReadyTick, "rematerializationReadyTick", -1);
        }
    }
}
