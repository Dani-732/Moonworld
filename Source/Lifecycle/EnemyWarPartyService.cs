using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class EnemyWarPartyService
    {
        private static bool generating;

        public static string ValidateRaid(Map map)
        {
            GameComponent_MoonWorld war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            HolyGrailWarEntry entry = war?.CurrentWarEntry;
            if (war?.CurrentWarOutcome != WarOutcome.Ongoing)
                return "本届圣杯战争已经结束，不能继续发动敌方突袭。";
            if (generating || war == null || war.warStartTick < 0 || entry == null || !entry.RegularSummonUsed)
                return "请先完成本届玩家召唤，并等待当前部署结束。";
            if (map == null || !map.IsPlayerHome || !map.CanEverExit)
                return "敌方突袭需要有出口的玩家基地。";
            bool playerPresent = IsPlayerTarget(entry.DesignatedMaster, map);
            foreach (var participant in entry.Participants)
                playerPresent |= IsPlayerTarget(participant.CurrentMaster, map)
                    || (participant.CurrentMaster == null && IsPlayerTarget(participant.EnemyServant, map));
            if (!playerPresent) return "本届玩家御主或落单从者必须位于该基地。";
            foreach (var enemy in entry.Participants)
                if (!enemy.EnemyEliminated && !WorkshopRebuildService.BlocksRaid(enemy)
                    && EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null) return null;
            return "当前没有可出战的敌方阵营：可能正在出击、休整、撤离重建或已经淘汰。";
        }

        private static bool IsPlayerTarget(Pawn pawn, Map map) => pawn != null && !pawn.Dead && !pawn.Destroyed
            && pawn.Spawned && pawn.Map == map && pawn.Faction == Faction.OfPlayer && !pawn.IsPrisoner && !pawn.IsSlave;

        public static bool TryDeploy(Map map, IntVec3 cell, out string rejection)
        { return TryDeploy(map, cell, out rejection, out _); }

        public static bool TryDeploy(Map map, IntVec3 cell, out string rejection, out Pawn deployedServant)
        {
            deployedServant = null;
            rejection = ValidateRaid(map);
            if (rejection != null) return false;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                || cell.GetFirstPawn(map) != null)
            { rejection = "请选择已探索且未被角色占用的可站立格。"; return false; }
            HolyGrailWarEntry entry = Current.Game.GetComponent<GameComponent_MoonWorld>().CurrentWarEntry;
            var ready = new List<EnemyWarParticipant>();
            foreach (var enemy in entry.Participants)
                if (!enemy.EnemyEliminated && !WorkshopRebuildService.BlocksRaid(enemy)
                    && EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null) ready.Add(enemy);
            EnemyWarParticipant selected = ready.RandomElement();
            if (!TryRedeployExisting(selected, map, cell, out rejection)) return false;
            deployedServant = selected.EnemyServant;
            return true;
        }

        internal static bool TryRedeployExisting(EnemyWarParticipant entry, Map map, IntVec3 cell, out string rejection,
            Func<bool> reservationValid = null)
        {
            rejection = "原从者当前不可部署。";
            if (generating || entry == null || map == null
                || EnemyRestUtility.ReadinessRejection(entry.EnemyServant, reservationValid != null, reservationValid != null) != null
                || (reservationValid != null && !reservationValid())) return false;
            Pawn servant = entry.EnemyServant;
            Pawn originalMaster = ServantQuery.Instance.GetMaster(servant);
            Faction originalFaction = servant.Faction;
            Func<bool> stillOwned = () => servant.Faction == originalFaction
                && ServantQuery.Instance.GetMaster(servant) == originalMaster && EnemyContractUtility.HasEnemyContract(servant)
                && !servant.Dead && !servant.Destroyed && !servant.IsPrisoner && !servant.IsSlave && !servant.Suspended
                && Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarOutcome == WarOutcome.Ongoing
                && (reservationValid == null || reservationValid());
            Lord lord = null;
            generating = true;
            try
            {
                Find.WorldPawns.RemovePawn(servant);
                GenSpawn.Spawn(servant, cell, map, servant.Rotation, WipeMode.Vanish);
                if (!servant.Spawned || servant.Map != map || !servant.CanReachMapEdge()
                    || !stillOwned())
                    throw new InvalidOperationException("敌方再袭落点、撤退路线或契约无效。");
                lord = LordMaker.MakeNewLord(servant.Faction, new LordJob_EnemyWarParty(), map, new[] { servant });
                if (!stillOwned() || !servant.Spawned || servant.Map != map)
                    throw new InvalidOperationException("敌方部署期间参与者或约战已改变。");
                // Commit presence last; earlier failures must leave a resting spirit unchanged.
                if (!ServantLifecycleService.Instance.TryPrepareEnemyRaid(servant, out rejection))
                    throw new InvalidOperationException(rejection);
                if (!stillOwned() || !servant.Spawned || servant.Map != map)
                    throw new InvalidOperationException("敌方部署提交前参与者或约战已改变。");
                entry.RecordEnemyDeployment(originalMaster, servant);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    // LordMaker can throw after registering a partially constructed lord.
                    Lord activeLord = lord ?? servant.GetLord();
                    if (activeLord != null && activeLord == servant.GetLord()
                        && activeLord.LordJob is LordJob_EnemyWarParty && servant.Map == map)
                        map.lordManager.RemoveLord(activeLord);
                }
                finally
                {
                    Pawn currentMaster = ServantQuery.Instance.GetMaster(servant);
                    if (servant.Faction == originalFaction && (currentMaster == null || currentMaster == originalMaster))
                        EnemyBattleService.ReturnToWorld(servant, map);
                }
                Log.Error("[MoonWorld] 敌方从者再袭部署失败: " + ex);
                rejection = "敌方部署失败；保留原从者与当前归属，可安全撤回的角色已退回场外。";
                return false;
            }
            finally { generating = false; }
        }

        internal static bool TryDeployFinalBattle(Map map, out string rejection)
        {
            rejection = ValidateRaid(map);
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (rejection != null) return false;
            if (war.enemyBattle != null || war.enemyChallenge != null)
            { rejection = "当前已有其他交战或约战，不能重复部署圣杯决战。"; return false; }
            var ready = new List<EnemyWarParticipant>();
            foreach (var participant in war.CurrentWarEntry.Enemies)
            {
                if (participant.EnemyEliminated) continue;
                if (WorkshopRebuildService.BlocksRaid(participant)
                    || EnemyRestUtility.ReadinessRejection(participant.EnemyServant) != null)
                { rejection = "仍存续的敌方从者尚未全部满足圣杯决战的健康、魔力、休整和占用条件。"; return false; }
                ready.Add(participant);
            }
            if (ready.Count == 0) { rejection = "没有满足现有健康、魔力和休整条件的敌方从者。"; return false; }
            var cells = new List<IntVec3>();
            foreach (var participant in ready)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map)
                    && c.GetFirstPawn(map) == null && !cells.Contains(c), map, 0f, out IntVec3 cell))
                { rejection = "玩家基地边缘没有足够的可用落点。"; return false; }
                cells.Add(cell);
            }
            var deployed = new List<EnemyWarParticipant>();
            var previous = new Dictionary<EnemyWarParticipant, System.Tuple<bool, int>>();
            try
            {
                for (int i = 0; i < ready.Count; i++)
                {
                    var participant = ready[i];
                    previous[participant] = System.Tuple.Create(participant.EnemyDeployed, participant.EnemyRestStartTickAbs);
                    if (!TryRedeployExisting(participant, map, cells[i], out rejection,
                        () => war.CurrentWarOutcome == WarOutcome.Ongoing && war.enemyBattle == null && war.enemyChallenge == null
                            && WarRhythmPolicy.FinalBattleDue(war))) throw new InvalidOperationException(rejection);
                    deployed.Add(participant);
                }
                foreach (var participant in ready)
                    if (!participant.EnemyServant.Spawned || participant.EnemyServant.Map != map)
                        throw new InvalidOperationException("圣杯决战参与者未全部进入玩家基地。");
                return true;
            }
            catch (Exception ex)
            {
                foreach (var participant in deployed)
                {
                    EnemyBattleService.ReturnToWorld(participant.EnemyServant, map);
                    if (previous.TryGetValue(participant, out var state)) participant.RestoreDeploymentState(state.Item1, state.Item2);
                }
                rejection = "圣杯决战部署失败，已保留未改变的原从者：" + ex.Message;
                return false;
            }
        }

        public static void RetainDepartedPawn(Pawn pawn)
        {
            EnemyWarParticipant entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(pawn);
            if (entry == null || (pawn != entry.EnemyMaster && pawn != entry.EnemyServant)
                || (pawn == entry.EnemyServant && entry.EnemyEliminated)
                || !WorkshopRebuildService.IsFreeSurvivor(pawn) || pawn.Faction == Faction.OfPlayer
                || !EnemyContractUtility.IsWarPawn(pawn)) return;
            // Pawn.ExitMap has already transferred it to WorldPawns and written its native timestamp.
            // Do not remove and re-add it here: that makes the rest clock mutable.
            if (Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
            if (pawn == entry.EnemyServant)
            {
                entry.RecordEnemyDeparture(pawn);
                WarReportService.FinishForActor(Current.Game?.GetComponent<GameComponent_MoonWorld>(), pawn, "从者离场");
            }
        }
    }
}
