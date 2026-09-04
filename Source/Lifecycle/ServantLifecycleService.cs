using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ServantLifecycleService : IServantLifecycle
    {
        public static readonly ServantLifecycleService Instance = new ServantLifecycleService();

        private ServantLifecycleService()
        {
        }

        public bool TryBind(Pawn master, Pawn servant, out string rejection)
        {
            rejection = null;
            if (master == null || servant == null)
            {
                rejection = "御主或从者不存在。";
                return false;
            }
            if (!MasterCircuitUtility.HasCircuit(master))
            {
                rejection = "该角色没有魔力回路。";
                return false;
            }
            if (!ServantQuery.Instance.IsServant(servant))
            {
                rejection = "目标不是 MoonWorld 从者。";
                return false;
            }

            CompServantState state = servant.TryGetComp<CompServantState>();
            if (state == null)
            {
                rejection = "从者缺少生命周期组件。";
                return false;
            }
            if (state.Master != null && state.Master != master)
            {
                rejection = "从者已与其他御主订立契约。";
                return false;
            }

            state.Bind(master);
            state.SetPresence(ServantPresenceState.Materialized);
            state.SetRematerializationReadyTick(-1);
            QuestLodgerAutonomyService.Initialize(servant);
            return true;
        }

        public bool TryEnterVoluntarySpirit(Pawn master, Pawn servant)
        {
            CompServantState state = GetBoundState(master, servant);
            if (state == null || state.PresenceState != ServantPresenceState.Materialized)
            {
                return false;
            }

            state.SetPresence(ServantPresenceState.VoluntarySpirit);
            return true;
        }

        public bool TryRematerialize(Pawn master, Pawn servant)
        {
            CompServantState state = GetBoundState(master, servant);
            if (state == null)
            {
                return false;
            }
            if (state.PresenceState == ServantPresenceState.DefeatedSpirit
                && Find.TickManager.TicksGame < state.RematerializationReadyTick)
            {
                return false;
            }
            if (state.PresenceState != ServantPresenceState.VoluntarySpirit
                && state.PresenceState != ServantPresenceState.DefeatedSpirit)
            {
                return false;
            }

            state.SetPresence(ServantPresenceState.Materialized);
            state.SetRematerializationReadyTick(-1);
            return true;
        }

        // Combat will call this after its vanilla-state-change integration has established a normal defeat.
        public void ResolveDefeat(Pawn servant)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState != ServantPresenceState.Materialized || state.DefeatResolutionInProgress)
            {
                return;
            }

            state.SetDefeatResolutionInProgress(true);
            try
            {
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                if (prana != null)
                {
                    prana.CurLevel = 0f;
                }

                Hediff spiritDamage = servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_SpiritDamage);
                if (spiritDamage == null)
                {
                    spiritDamage = servant.health.AddHediff(MW_DefOf.MW_SpiritDamage);
                }
                spiritDamage.Severity += 1f;

                ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
                if (profile == null || spiritDamage.Severity >= profile.maxSpiritDamageStages)
                {
                    Annihilate(servant, ServantEndReason.SpiritDamageLimit);
                    return;
                }

                state.SetPresence(ServantPresenceState.DefeatedSpirit);
                state.SetRematerializationReadyTick(Find.TickManager.TicksGame + profile.rematerializationCooldownTicks);
            }
            finally
            {
                if (state.PresenceState != ServantPresenceState.Annihilated)
                {
                    state.SetDefeatResolutionInProgress(false);
                }
            }
        }

        public void Annihilate(Pawn servant, ServantEndReason reason)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState == ServantPresenceState.Annihilated)
            {
                return;
            }

            state.SetPresence(ServantPresenceState.Annihilated);
            servant.Kill(null);
        }

        private static CompServantState GetBoundState(Pawn master, Pawn servant)
        {
            if (master == null || servant == null)
            {
                return null;
            }

            CompServantState state = servant.TryGetComp<CompServantState>();
            return state != null && state.Master == master ? state : null;
        }
    }
}
