using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    public static class HolyGrailEndingService
    {
        internal const int ServantFadeDelayTicks = 60000;
        internal const int WealthWishAmount = 10000;

        internal static void OnVictory(GameComponent_MoonWorld state)
        {
            if (state == null || state.CurrentWarOutcome != WarOutcome.PlayerVictory) return;
            if (!state.holyGrailRewardGranted)
            {
                state.holyGrailRewardGranted = true;
                state.holyGrailServantDeadlineTickAbs = HolyGrailEndingPolicy.DeadlineFor(GenTicks.TicksAbs, ServantFadeDelayTicks);
            }
            TrySpawnGrail(state);
        }

        internal static void Tick(GameComponent_MoonWorld state)
        {
            if (state == null || state.CurrentWarOutcome != WarOutcome.PlayerVictory
                || !state.holyGrailRewardGranted) return;

            if (!state.holyGrailRewardSpawned)
                TrySpawnGrail(state);

            if (HolyGrailEndingPolicy.DeadlineDue(state.holyGrailMaterializationGranted,
                state.holyGrailServantDeadlineTickAbs, GenTicks.TicksAbs))
            {
                DismissUnmaterializedServants(state);
                state.holyGrailServantDeadlineTickAbs = -1;
                Messages.Message("圣杯降临一日后，未许愿实体化的灵体从者已退场。", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        internal static bool TryWishWealth(Building_HolyGrail grail, Pawn user)
        {
            GameComponent_MoonWorld state;
            if (!CanWish(grail, user, out state)) return false;

            int remaining = WealthWishAmount;
            var placedGold = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(grail.Position, 8f, true))
            {
                if (!cell.InBounds(grail.Map) || !cell.Standable(grail.Map)) continue;
                int count = Mathf.Min(ThingDefOf.Gold.stackLimit, remaining);
                Thing gold = ThingMaker.MakeThing(ThingDefOf.Gold);
                gold.stackCount = count;
                if (!GenPlace.TryPlaceThing(gold, cell, grail.Map, ThingPlaceMode.Near))
                {
                    if (!gold.Destroyed) gold.Destroy(DestroyMode.Vanish);
                    continue;
                }
                placedGold.Add(gold);
                remaining -= count;
                if (remaining <= 0) break;
            }

            if (remaining > 0)
            {
                foreach (Thing gold in placedGold)
                    if (gold != null && !gold.Destroyed) gold.Destroy(DestroyMode.Vanish);
                Messages.Message("圣杯周围没有足够空间承载财富，许愿未完成。", grail, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ResolveWish(state, grail, materialization: false);
            Messages.Message("圣杯许愿成功：黄金降临。", grail, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        internal static bool TryWishMaterialization(Building_HolyGrail grail, Pawn user)
        {
            GameComponent_MoonWorld state;
            if (!CanWish(grail, user, out state)) return false;

            int materialized = 0;
            foreach (Pawn servant in WarServants(state))
            {
                if (servant == null || servant.Destroyed || servant.Dead) continue;
                CompServantState servantState = servant.TryGetComp<CompServantState>();
                if (!IsOwnedByPlayer(servant, servantState)
                    || servantState.PresenceState == ServantPresenceState.Annihilated
                    || IsPermanentlyMaterialized(servant)) continue;

                servantState.SetPresence(ServantPresenceState.Materialized);
                ServantPresenceEffects.Reconcile(servant);
                Pawn master = servantState.Master;
                if (servant.Faction != Faction.OfPlayer)
                    ServantColonyMembership.SetFactionPreservingKind(servant, Faction.OfPlayer);
                if (master != null && master.Faction == Faction.OfPlayer)
                    ServantColonyMembership.Initialize(servant, newContract: true);
                if (servant.story?.traits != null)
                    servant.story.traits.GainTrait(new Trait(MW_DefOf.MW_HolyGrailMaterialized));
                Hediff shortage = servant.health?.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_PranaShortage);
                if (shortage != null) servant.health.RemoveHediff(shortage);
                AddGratitude(servant, master);
                materialized++;
            }

            state.holyGrailMaterializationGranted = true;
            ResolveWish(state, grail, materialization: true);
            Messages.Message("圣杯许愿成功：" + materialized + " 名灵体从者实体化并加入殖民地。", grail,
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private static bool CanWish(Building_HolyGrail grail, Pawn user, out GameComponent_MoonWorld state)
        {
            state = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            return grail != null && !grail.Destroyed && grail.Spawned && grail.Map != null
                && user != null && user.Faction == Faction.OfPlayer
                && state != null && state.CurrentWarOutcome == WarOutcome.PlayerVictory
                && state.holyGrailRewardGranted && !state.holyGrailWishMade;
        }

        private static void ResolveWish(GameComponent_MoonWorld state, Building_HolyGrail grail, bool materialization)
        {
            state.holyGrailWishMade = true;
            if (materialization) state.holyGrailServantDeadlineTickAbs = -1;
            if (grail != null && !grail.Destroyed) grail.Destroy(DestroyMode.Vanish);
        }

        private static void TrySpawnGrail(GameComponent_MoonWorld state)
        {
            if (state.holyGrailRewardSpawned || state.holyGrailWishMade || state.CurrentWarEntry == null) return;
            Map map = FindPlayerMap(state.CurrentWarEntry);
            if (map == null || !TryFindRewardCell(map, state.CurrentWarEntry.DesignatedMaster, out IntVec3 cell)) return;

            Thing building = ThingMaker.MakeThing(MW_DefOf.MW_HolyGrail);
            MinifiedThing minified = MinifyUtility.MakeMinified(building, DestroyMode.Vanish);
            if (minified == null || !GenPlace.TryPlaceThing(minified, cell, map, ThingPlaceMode.Near))
            {
                if (minified != null && !minified.Destroyed) minified.Destroy(DestroyMode.Vanish);
                return;
            }

            state.holyGrailRewardSpawned = true;
            Messages.Message("圣杯已降临：请将其安装在地面后使用。", minified, MessageTypeDefOf.PositiveEvent, false);
        }

        private static Map FindPlayerMap(HolyGrailWarEntry entry)
        {
            if (entry.DesignatedMaster?.Spawned == true && entry.DesignatedMaster.Map != null)
                return entry.DesignatedMaster.Map;
            return Find.Maps.FirstOrDefault(map => map.IsPlayerHome);
        }

        private static bool TryFindRewardCell(Map map, Pawn master, out IntVec3 cell)
        {
            if (master?.Spawned == true && master.Map == map && master.Position.Standable(map)
                && master.Position.GetFirstBuilding(map) == null)
            {
                cell = master.Position;
                return true;
            }
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(map.Center, 12f, true))
                if (candidate.InBounds(map) && candidate.Standable(map) && candidate.GetFirstBuilding(map) == null
                    && candidate.GetFirstPawn(map) == null)
                {
                    cell = candidate;
                    return true;
                }
            cell = IntVec3.Invalid;
            return false;
        }

        private static IEnumerable<Pawn> WarServants(GameComponent_MoonWorld state)
        {
            var result = new List<Pawn>();
            foreach (EnemyWarParticipant participant in state.CurrentWarEntry?.Participants ?? new List<EnemyWarParticipant>())
                if (participant?.EnemyServant != null && !result.Contains(participant.EnemyServant)) result.Add(participant.EnemyServant);
            if (state.CurrentWarEntry?.PlayerServant != null && !result.Contains(state.CurrentWarEntry.PlayerServant))
                result.Add(state.CurrentWarEntry.PlayerServant);
            return result;
        }

        private static void DismissUnmaterializedServants(GameComponent_MoonWorld state)
        {
            foreach (Pawn servant in WarServants(state).ToList())
            {
                CompServantState servantState = servant?.TryGetComp<CompServantState>();
                if (!HolyGrailEndingPolicy.ShouldDismiss(servant != null && !servant.Destroyed && !servant.Dead,
                    IsOwnedByPlayer(servant, servantState), IsPermanentlyMaterialized(servant),
                    servantState?.PresenceState == ServantPresenceState.Annihilated)) continue;
                servantState.SetPresence(ServantPresenceState.Annihilated);
                ServantPresenceEffects.Reconcile(servant);
                Caravan caravan = servant.GetCaravan();
                caravan?.RemovePawn(servant);
                if (servant.Spawned) servant.DeSpawn(DestroyMode.Vanish);
                if (Find.WorldPawns.Contains(servant)) Find.WorldPawns.RemovePawn(servant);
                if (!servant.Destroyed) servant.Destroy(DestroyMode.Vanish);
            }
        }

        private static void AddGratitude(Pawn servant, Pawn master)
        {
            if (servant?.needs?.mood?.thoughts?.memories == null || master == null || MW_DefOf.MW_HolyGrailGratitude == null) return;
            servant.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(MW_DefOf.MW_HolyGrailGratitude, master);
            servant.needs.mood.thoughts.memories.TryGainMemory(MW_DefOf.MW_HolyGrailGratitude, master);
        }

        internal static bool IsPermanentlyMaterialized(Pawn servant)
        {
            return servant?.story?.traits?.HasTrait(MW_DefOf.MW_HolyGrailMaterialized) == true;
        }

        private static bool IsOwnedByPlayer(Pawn servant, CompServantState state)
        {
            return servant != null && state != null
                && (servant.Faction == Faction.OfPlayer || state.Master?.Faction == Faction.OfPlayer);
        }
    }
}
