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
            return TryBindInternal(master, servant, false, out rejection);
        }

        internal bool TryBindEnemy(Pawn master, Pawn servant, out string rejection)
        {
            return TryBindInternal(master, servant, true, out rejection);
        }

        private bool TryBindInternal(Pawn master, Pawn servant, bool enemy, out string rejection)
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
            bool factionValid = enemy
                ? EnemyContractUtility.IsWarPawn(master) && servant.Faction == master.Faction
                : master.Faction == Faction.OfPlayer;
            if (!factionValid || master.Dead || master.Destroyed || master.IsPrisoner || master.IsSlave
                || servant.Dead || servant.Destroyed
                || servant.IsPrisoner || servant.IsSlave)
            {
                rejection = "契约双方必须存活、阵营有效，且不是囚犯或奴隶。";
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
            if (!enemy) ServantColonyMembership.Initialize(servant, newContract: true);
            ServantPresenceEffects.Reconcile(servant);
            return true;
        }

        public bool TryEnterVoluntarySpirit(Pawn master, Pawn servant)
        {
            string rejection;
            return TryEnterVoluntarySpirit(master, servant, out rejection);
        }

        public bool TryEnterVoluntarySpirit(Pawn master, Pawn servant, out string rejection)
        {
            rejection = null;
            CompServantState state = GetBoundState(master, servant);
            if (state == null)
            {
                rejection = "该从者未与当前御主订立契约。";
                return false;
            }
            if (!CanChangePresence(master, servant))
            {
                rejection = "需要有效的存活主从契约，且从者位于地图上。";
                return false;
            }
            if (servant.Downed)
            {
                rejection = "倒地从者不能主动灵体化。";
                return false;
            }
            if (state.PresenceState != ServantPresenceState.Materialized)
            {
                rejection = "从者当前不是实体化状态。";
                return false;
            }

            state.SetPresence(ServantPresenceState.VoluntarySpirit);
            ServantPresenceEffects.Reconcile(servant);
            return true;
        }

        public bool TryRematerialize(Pawn master, Pawn servant)
        {
            string rejection;
            return TryRematerialize(master, servant, out rejection);
        }

        public bool TryRematerialize(Pawn master, Pawn servant, out string rejection)
        {
            rejection = null;
            CompServantState state = GetBoundState(master, servant);
            if (state == null)
            {
                rejection = "该从者未与当前御主订立契约。";
                return false;
            }
            if (!CanChangePresence(master, servant))
            {
                rejection = "需要有效的存活主从契约，且从者位于地图上。";
                return false;
            }
            if (state.PresenceState != ServantPresenceState.VoluntarySpirit
                && state.PresenceState != ServantPresenceState.DefeatedSpirit)
            {
                rejection = "从者当前不是可解除的灵体状态。";
                return false;
            }
            if (!servant.Position.Standable(servant.Map))
            {
                rejection = "从者必须位于可站立、可通行的格子上才能实体化。";
                return false;
            }
            if (servant.health.ShouldBeDead() || servant.health.ShouldBeDowned())
            {
                rejection = "从者当前伤势仍会导致死亡或倒地，无法实体化。";
                return false;
            }

            state.SetPresence(ServantPresenceState.Materialized);
            ServantPresenceEffects.Reconcile(servant);
            return true;
        }

        internal bool TryPrepareEnemyRaid(Pawn servant, out string rejection)
        {
            rejection = "敌方从者当前无法实体化出战。";
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            CompServantState state = GetBoundState(master, servant);
            if (state == null || !EnemyContractUtility.HasEnemyContract(servant)
                || !EnemyContractUtility.CanReceiveSupply(servant) || master.Spawned || master.Downed
                || !servant.Spawned || !servant.Position.Standable(servant.Map)
                || servant.health.ShouldBeDead() || servant.health.ShouldBeDowned()) return false;
            ServantPresenceState previous = state.PresenceState;
            if (previous != ServantPresenceState.Materialized && previous != ServantPresenceState.DefeatedSpirit
                && previous != ServantPresenceState.VoluntarySpirit) return false;
            try
            {
                state.SetPresence(ServantPresenceState.Materialized);
                ServantPresenceEffects.Reconcile(servant);
                rejection = null;
                return true;
            }
            catch
            {
                state.SetPresence(previous);
                ServantPresenceEffects.Reconcile(servant);
                throw;
            }
        }

        public bool TryResolveDefeat(Pawn servant, Hediff triggeringHediff = null)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState != ServantPresenceState.Materialized || state.DefeatResolutionInProgress)
            {
                return false;
            }

            state.SetDefeatResolutionInProgress(true);
            try
            {
                if (servant.health.ShouldBeDead()
                    && !ServantFatalDamageRecovery.TryStabilize(servant, triggeringHediff))
                {
                    Log.Warning("[MoonWorld] 无法稳定从者 " + servant.LabelShortCap + " 的致命伤，交还原版死亡流程。");
                    return false;
                }

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
                    return true;
                }

                state.SetPresence(ServantPresenceState.DefeatedSpirit);
                ServantPresenceEffects.Reconcile(servant);
                return true;
            }
            finally
            {
                if (state.PresenceState != ServantPresenceState.Annihilated)
                {
                    state.SetDefeatResolutionInProgress(false);
                }
            }
        }

        public bool TryPreserveSpirit(Pawn servant, Hediff triggeringHediff = null)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || !ServantQuery.Instance.IsSpirit(servant) || state.DefeatResolutionInProgress)
            {
                return false;
            }

            state.SetDefeatResolutionInProgress(true);
            try
            {
                return ServantFatalDamageRecovery.TryStabilize(servant, triggeringHediff);
            }
            finally
            {
                state.SetDefeatResolutionInProgress(false);
            }
        }

        public void PrepareForVanillaDeath(Pawn servant)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState == ServantPresenceState.Annihilated)
            {
                return;
            }

            state.SetDefeatResolutionInProgress(true);
            try
            {
                state.SetPresence(ServantPresenceState.Annihilated);
                ServantPresenceEffects.Reconcile(servant);
            }
            finally
            {
                state.SetDefeatResolutionInProgress(false);
            }
        }

        public void Annihilate(Pawn servant, ServantEndReason reason)
        {
            CompServantState state = servant == null ? null : servant.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState == ServantPresenceState.Annihilated)
            {
                return;
            }

            PrepareForVanillaDeath(servant);
            servant.Kill(null);
        }

        public static bool CanChangePresence(Pawn master, Pawn servant)
        {
            return master != null
                && servant != null
                && !master.Dead
                && !servant.Dead
                && !master.Destroyed && !servant.Destroyed
                && !master.IsPrisoner && !master.IsSlave
                && !servant.IsPrisoner && !servant.IsSlave
                && MasterCircuitUtility.HasCircuit(master)
                && GetBoundState(master, servant) != null
                && ((master.Faction == Faction.OfPlayer && servant.Faction == Faction.OfPlayer
                        && master.HostFaction == null && servant.HostFaction == null)
                    || EnemyContractUtility.HasEnemyContract(servant))
                && servant.Spawned
                && servant.Map != null;
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
