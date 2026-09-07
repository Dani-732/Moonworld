using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // The deadline belongs to CompServantState; this service only advances that lifecycle.
    internal static class UnboundServantService
    {
        internal static bool Exists(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && ServantQuery.Instance.IsServant(pawn)
                && pawn.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated;
        }

        internal static List<Pawn> KnownServants()
        {
            var result = new List<Pawn>();
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
                if (Exists(pawn) && !result.Contains(pawn)) result.Add(pawn);
            var entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            if (entry != null)
            {
                if (Exists(entry.PlayerServant) && !result.Contains(entry.PlayerServant)) result.Add(entry.PlayerServant);
                foreach (var enemy in entry.Enemies)
                    if (Exists(enemy.EnemyServant) && !result.Contains(enemy.EnemyServant)) result.Add(enemy.EnemyServant);
            }
            return result;
        }

        internal static void NotifyMasterUnavailable(Pawn master)
        {
            if (master == null || CommandSpellService.HasQualification(master)) return;
            foreach (Pawn servant in KnownServants())
                if (servant.TryGetComp<CompServantState>()?.Master == master) Release(servant);
        }

        internal static void Release(Pawn servant)
        {
            if (!Exists(servant)) return;
            CompServantState state = servant.TryGetComp<CompServantState>();
            if (state == null) return;
            bool changed = state.Master != null || state.UnboundUntilTickAbs < 0;
            int deadline = state.UnboundUntilTickAbs;
            if (deadline < 0)
            {
                float days = servant.GetStatValue(MW_DefOf.MW_UnboundSurvivalDays, applyPostProcess: true, cacheStaleAfterTicks: 0);
                if (float.IsNaN(days) || float.IsInfinity(days)) days = 1f;
                deadline = (int)Math.Min(int.MaxValue, GenTicks.TicksAbs + Math.Ceiling(Math.Max(1f, days) * 60000d));
            }
            state.BeginUnbound(deadline);
            // An old shortage cannot cause another defeat before the unbound deadline.
            Hediff shortage = servant.health?.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_PranaShortage);
            if (shortage != null) servant.health.RemoveHediff(shortage);
            if (changed)
            {
                if (servant.Drafted) servant.drafter.Drafted = false;
                ServantPresenceEffects.Reconcile(servant);
            }
        }

        internal static void Tick()
        {
            foreach (Pawn servant in KnownServants())
            {
                CompServantState state = servant.TryGetComp<CompServantState>();
                if (state == null) continue;
                if (!CommandSpellService.HasQualification(state.Master)) Release(servant);
                if (state.Master == null && state.UnboundUntilTickAbs >= 0
                    && GenTicks.TicksAbs >= state.UnboundUntilTickAbs)
                    ServantLifecycleService.Instance.Annihilate(servant, ServantEndReason.UnboundExpired);
            }
        }
    }
}
