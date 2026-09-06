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
        private bool defeatResolutionInProgress;

        public Pawn Master => master;
        public ServantPresenceState PresenceState => presenceState;
        public bool DefeatResolutionInProgress => defeatResolutionInProgress;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (ServantQuery.Instance.IsServant(pawn))
            {
                pawn.needs.AddOrRemoveNeedsAsAppropriate();
                PawnNeedAccess.EnsureNeed(pawn, MW_DefOf.MW_Prana);
                NoblePhantasmService.EnsureAbilities(pawn);
                if (master != null && !respawningAfterLoad)
                {
                    ServantColonyMembership.Initialize(pawn);
                }
                ServantPresenceEffects.Reconcile(pawn);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn != null && pawn.Spawned && pawn.IsHashIntervalTick(250)
                && ServantQuery.Instance.IsServant(pawn))
            {
                ServantPresenceEffects.Reconcile(pawn);
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

        public void SetDefeatResolutionInProgress(bool value)
        {
            defeatResolutionInProgress = value;
        }

        public override string CompInspectStringExtra()
        {
            // The shared source race also contains characters not yet connected to MoonWorld.
            if (!ServantQuery.Instance.IsServant(parent as Pawn)) return null;
            string stateLabel;
            switch (presenceState)
            {
                case ServantPresenceState.VoluntarySpirit:
                    stateLabel = "主动灵体化";
                    break;
                case ServantPresenceState.DefeatedSpirit:
                    stateLabel = "战败灵体化";
                    break;
                case ServantPresenceState.Annihilated:
                    stateLabel = "已湮灭";
                    break;
                default:
                    stateLabel = "实体化";
                    break;
            }

            string result = "存在状态：" + stateLabel;
            HolyGrailWarClass warClass = ServantIdentityUtility.GetIdentity(parent as Pawn)?.warClass ?? HolyGrailWarClass.None;
            if (warClass != HolyGrailWarClass.None) result += "\n职阶：" + warClass;
            if (master != null)
            {
                result += "\n契约御主：" + master.LabelShort;
                if (presenceState != ServantPresenceState.Annihilated)
                {
                    Pawn pawn = parent as Pawn;
                    result += "\n最低维持魔力：" + ServantSustainPolicy.Threshold(pawn).ToString("0.##")
                        + (ServantSustainPolicy.IsTogether(pawn) ? "（主从同行）" : "（主从分离 ×"
                            + ServantSustainPolicy.SeparationMultiplier(pawn).ToString("0.##") + "）");
                }
            }
            return result;
        }

        public override void PostExposeData()
        {
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref presenceState, "presenceState", ServantPresenceState.Materialized);
        }
    }
}
