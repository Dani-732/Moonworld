using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    internal static class ServantRecontractService
    {
        private static bool changing;
        internal static List<Pawn> Masters()
        {
            var result = new List<Pawn>();
            // PawnsFinder reuses its list; IsWaitingMaster performs another pawn query.
            foreach (Pawn pawn in new List<Pawn>(PawnsFinder.AllMapsAndWorld_Alive))
                if (IsWaitingMaster(pawn) && !result.Contains(pawn)) result.Add(pawn);
            var entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            if (entry != null)
                foreach (var participant in entry.Participants)
                {
                    if (IsWaitingMaster(participant.OriginalMaster) && !result.Contains(participant.OriginalMaster))
                        result.Add(participant.OriginalMaster);
                    if (IsWaitingMaster(participant.EnemyMaster) && !result.Contains(participant.EnemyMaster)) result.Add(participant.EnemyMaster);
                }
            result.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            return result;
        }

        internal static bool IsWaitingMaster(Pawn pawn)
        {
            if (!CommandSpellService.HasQualification(pawn) || !MasterCircuitUtility.HasCircuit(pawn)
                || ServantQuery.Instance.IsServant(pawn) || pawn.IsPrisoner || pawn.IsSlave || pawn.Downed
                || pawn.Suspended || pawn.InMentalState || (pawn.Faction != Faction.OfPlayer && !EnemyContractUtility.IsWarPawn(pawn))) return false;
            foreach (Pawn servant in UnboundServantService.KnownServants())
                if (ServantQuery.Instance.GetMaster(servant) == pawn) return false;
            return true;
        }

        internal static bool PlayerInvolved(Pawn master, Pawn servant) => master?.Faction == Faction.OfPlayer || servant?.Faction == Faction.OfPlayer;
        internal static string Rejection(Pawn master, Pawn servant)
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (war?.CurrentWarOutcome != WarOutcome.Ongoing || war.warStartTick < 0) return "本届战争未进行中。";
            if (!IsWaitingMaster(master)) return "御主必须有有效令咒与回路、没有存续契约从者，且清醒自由。";
            var state = servant?.TryGetComp<CompServantState>();
            if (!UnboundServantService.Exists(servant) || state == null || state.Master != null
                || state.UnboundUntilTickAbs < 0 || GenTicks.TicksAbs >= state.UnboundUntilTickAbs
                || war.CurrentWarEntry?.FindEnemy(servant)?.EnemyServant != servant)
                return "目标必须是本届尚未到期的落单从者。";
            if (servant.IsPrisoner || servant.IsSlave || servant.Suspended || servant.InMentalState)
                return "从者当前无法自由订立契约。";
            if ((!master.Spawned && master.GetCaravan() == null && !WorkshopRebuildService.IsFreeSurvivor(master))
                || (!servant.Spawned && servant.GetCaravan() == null && !WorkshopRebuildService.IsFreeSurvivor(servant)))
                return "主从仍在运输容器或不可交接的状态。";
            if (servant.GetCaravan() != null && servant.Faction != master.Faction)
                return "从者仍在远行队中，抵达地图后才能跨派系交接。";
            if ((ServantTravelAutonomy.HasTravelAssignment(master) || ServantTravelAutonomy.HasTravelAssignment(servant))
                && master.Faction != servant.Faction) return "请在旅行集合结束后变更阵营。";
            if (master.Faction == Faction.OfPlayer && WorkshopRebuildService.IsFreeSurvivor(servant)
                && !master.Spawned && master.GetCaravan() == null) return "玩家御主需要在地图或远行队中接收场外从者。";
            return null;
        }

        internal static bool TryRecontract(Pawn master, Pawn servant, bool playerConfirmed, out string rejection)
        {
            rejection = Rejection(master, servant);
            if (rejection != null) return false;
            if (changing) { rejection = "正在处理另一份契约。"; return false; }
            if (PlayerInvolved(master, servant) && !playerConfirmed) { rejection = "涉及玩家角色的重契约需要确认。"; return false; }
            var entry = Current.Game.GetComponent<GameComponent_MoonWorld>().CurrentWarEntry;
            var participant = entry.FindEnemy(servant);
            var state = servant.TryGetComp<CompServantState>();
            int deadline = state.UnboundUntilTickAbs;
            int worldSince = servant.becameWorldPawnTickAbs;
            Faction masterFaction = master.Faction, servantFaction = servant.Faction;
            Lord masterLord = master.GetLord(), servantLord = servant.GetLord();
            var masterNeeds = CaptureNeeds(master);
            var servantNeeds = CaptureNeeds(servant);
            Site_WarWorkshop site = WorkshopRebuildService.FindWorkshop(participant);
            // A retired master's old workshop may be inherited only when the new seat has none.
            if (site == null)
                foreach (var obj in Find.WorldObjects.AllWorldObjects)
                    if (obj is Site_WarWorkshop old && !old.Destroyed && old.OwnerMaster == master && !old.RetreatOrdered
                        && !UnboundServantService.Exists(old.Participant?.EnemyServant)) { site = old; break; }
            changing = true;
            Faction created = null;
            Faction siteFaction = site?.Faction;
            bool keepTravel = false;
            bool arriving = masterFaction == Faction.OfPlayer && WorkshopRebuildService.IsFreeSurvivor(servant);
            Caravan arrivalCaravan = arriving ? master.GetCaravan() : null;
            try
            {
                Faction target = Faction.OfPlayer;
                if (masterFaction != Faction.OfPlayer)
                {
                    FactionDef factionDef = participant.Seat?.oppositionFaction;
                    if (factionDef == null) throw new InvalidOperationException("从者职阶没有派系定义。");
                    target = Find.FactionManager.FirstFactionOfDef(factionDef);
                    if (target == null)
                    {
                        target = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(factionDef, hidden: true));
                        Find.FactionManager.Add(target); created = target;
                        target.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile, false);
                        foreach (var other in entry.Participants)
                            if (other.EnemyServant?.Faction != null && other.EnemyServant.Faction != target
                                && EnemyContractUtility.IsWarPawn(other.EnemyServant))
                                target.SetRelationDirect(other.EnemyServant.Faction, FactionRelationKind.Hostile, false);
                    }
                }
                keepTravel = masterFaction == target && servantFaction == target
                    && (master.GetCaravan() != null || servant.GetCaravan() != null
                        || ServantTravelAutonomy.HasTravelAssignment(master) || ServantTravelAutonomy.HasTravelAssignment(servant));
                if (!keepTravel)
                {
                    masterLord?.RemovePawn(master); servantLord?.RemovePawn(servant);
                    master.jobs?.StopAll(false, false); servant.jobs?.StopAll(false, false);
                }
                ServantColonyMembership.SetFactionPreservingKind(master, target);
                ServantColonyMembership.SetFactionPreservingKind(servant, target);
                bool bound = target == Faction.OfPlayer
                    ? ServantLifecycleService.Instance.TryBind(master, servant, out rejection)
                    : ServantLifecycleService.Instance.TryBindEnemy(master, servant, out rejection);
                if (!bound) throw new InvalidOperationException(rejection);
                if (!CommandSpellService.HasQualification(master) || state.Master != master || master.Faction != target || servant.Faction != target)
                    throw new InvalidOperationException("重契约期间资格或派系发生变化。");
                RestoreNeeds(master, masterNeeds); RestoreNeeds(servant, servantNeeds);
                NoblePhantasmService.EnsureAbilities(servant);
                if (arriving)
                {
                    if (arrivalCaravan != null)
                    {
                        arrivalCaravan.AddPawn(servant, addCarriedPawnToWorldPawnsIfAny: true);
                        if (servant.GetCaravan() != arrivalCaravan) throw new InvalidOperationException("原从者未能加入远行队。");
                    }
                    else
                    {
                        Map map = master.Map;
                        if (map == null || !CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && c.GetFirstPawn(map) == null,
                            map, 0f, out IntVec3 cell)) throw new InvalidOperationException("没有可接收原从者的地图边缘落点。");
                        Find.WorldPawns.RemovePawn(servant);
                        GenSpawn.Spawn(servant, cell, map, servant.Rotation, WipeMode.Vanish);
                        if (!servant.Spawned || servant.Map != map) throw new InvalidOperationException("原从者入场失败。");
                    }
                    RestoreNeeds(servant, servantNeeds);
                }
                if (target != Faction.OfPlayer)
                {
                    if (servant.Spawned && servant.GetLord() == null)
                        LordMaker.MakeNewLord(target, new LordJob_EnemyWarParty(), servant.Map, new[] { servant });
                    if (master.Spawned && master.GetLord() == null)
                        LordMaker.MakeNewLord(target, new LordJob_DefendBase(target, master.Position, 25000), master.Map, new[] { master });
                }
                if (!CommandSpellService.HasQualification(master) || !UnboundServantService.Exists(servant)
                    || state.Master != master || master.Faction != target || servant.Faction != target)
                    throw new InvalidOperationException("入场期间契约或角色状态发生变化。");
                if (site != null)
                {
                    site.SetFaction(target);
                    site.TransferContractOwner(master, servant);
                }
                participant.RecordRecontractMaster(master);
                foreach (var obj in Find.WorldObjects.AllWorldObjects)
                    if (obj is Site_WarWorkshop old && old != site && old.OwnerMaster == master
                        && old.Participant != participant) old.AbandonContractOwner();
                if (target == Faction.OfPlayer) participant.CompleteWorkshopRebuild();
            }
            catch (Exception ex)
            {
                state.RestoreContract(null, deadline);
                if (arriving)
                {
                    arrivalCaravan?.RemovePawn(servant);
                    if (servant.Spawned) servant.DeSpawn();
                    if (!Find.WorldPawns.Contains(servant)) Find.WorldPawns.PassToWorld(servant, PawnDiscardDecideMode.KeepForever);
                    servant.becameWorldPawnTickAbs = worldSince;
                }
                ServantColonyMembership.SetFactionPreservingKind(master, masterFaction);
                ServantColonyMembership.SetFactionPreservingKind(servant, servantFaction);
                RestoreLord(master, masterLord); RestoreLord(servant, servantLord);
                if (site != null && site.Faction != siteFaction) site.SetFaction(siteFaction);
                RestoreNeeds(master, masterNeeds); RestoreNeeds(servant, servantNeeds);
                if (created != null) { created.RemoveAllRelations(); Find.FactionManager.AllFactionsListForReading.Remove(created); }
                rejection = "重契约失败，保留原落单期限：" + ex.Message;
                Log.Warning("[MoonWorld] " + rejection);
                return false;
            }
            finally { changing = false; }
            try
            {
                if (!keepTravel)
                {
                    RemoveEmptyLord(masterLord); if (servantLord != masterLord) RemoveEmptyLord(servantLord);
                }
                Messages.Message(master.LabelShortCap + " 与 " + servant.LabelShortCap + " 重新订立契约，归入 " + participant.Seat?.label + " 阵营。",
                    servant, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception ex) { Log.Warning("[MoonWorld] 契约已成立，通知失败：" + ex.Message); }
            return true;
        }

        private static Dictionary<NeedDef, float> CaptureNeeds(Pawn pawn)
        {
            var values = new Dictionary<NeedDef, float>();
            if (pawn.needs != null) foreach (Need need in pawn.needs.AllNeeds) values[need.def] = need.CurLevel;
            return values;
        }
        private static void RestoreNeeds(Pawn pawn, Dictionary<NeedDef, float> values)
        {
            if (pawn.needs != null) foreach (Need need in pawn.needs.AllNeeds)
                if (values.TryGetValue(need.def, out float value)) need.CurLevel = value;
        }
        private static void RemoveEmptyLord(Lord lord)
        { if (lord != null && lord.ownedPawns.Count == 0) lord.Map.lordManager.RemoveLord(lord); }
        private static void RestoreLord(Pawn pawn, Lord previous)
        {
            Lord current = pawn.GetLord();
            if (current != null && current != previous) { current.RemovePawn(pawn); RemoveEmptyLord(current); }
            if (previous != null && !previous.ownedPawns.Contains(pawn)) previous.AddPawn(pawn);
        }

        internal static void Tick()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (war?.CurrentWarOutcome != WarOutcome.Ongoing || war.warStartTick < 0) return;
            var masters = Masters();
            foreach (var participant in war.CurrentWarEntry.Participants)
            {
                Pawn servant = participant.EnemyServant;
                var state = servant?.TryGetComp<CompServantState>();
                if (state == null || state.Master != null || !UnboundServantService.Exists(servant)) continue;
                var playerOptions = masters.FindAll(m => PlayerInvolved(m, servant) && Rejection(m, servant) == null);
                if (playerOptions.Count > 0 && !state.RecontractOfferSent && Rand.Chance(0.8f))
                {
                    ChoiceLetter_Recontract.Offer(servant);
                    state.MarkRecontractOfferSent();
                }
                if (servant.Faction == Faction.OfPlayer || ChoiceLetter_Recontract.HasPending(servant)) continue;
                var enemies = masters.FindAll(m => !PlayerInvolved(m, servant) && !m.Spawned && !servant.Spawned && Rejection(m, servant) == null);
                if (enemies.Count > 0) TryRecontract(enemies.RandomElement(), servant, false, out _);
            }
        }
    }
}
