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
            Pawn playerMaster = entry.DesignatedMaster;
            if (playerMaster == null || playerMaster.Dead || playerMaster.Destroyed || !playerMaster.Spawned
                || playerMaster.Map != map || playerMaster.Faction != Faction.OfPlayer
                || playerMaster.IsPrisoner || playerMaster.IsSlave)
                return "本届玩家御主必须存活、自由且位于该基地。";
            foreach (var enemy in entry.Enemies)
                if (!enemy.EnemyEliminated && EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null) return null;
            return "当前没有可出战的敌方阵营：可能正在出击、休整或已经淘汰。";
        }

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
            foreach (var enemy in entry.Enemies)
                if (!enemy.EnemyEliminated && EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null) ready.Add(enemy);
            EnemyWarParticipant selected = ready.RandomElement();
            generating = true;
            try
            {
                if (!TryRedeployExisting(selected, map, cell, out rejection)) return false;
                deployedServant = selected.EnemyServant;
                return true;
            }
            finally { generating = false; }
        }

        private static bool TryRedeployExisting(EnemyWarParticipant entry, Map map, IntVec3 cell, out string rejection)
        {
            rejection = null;
            Pawn servant = entry.EnemyServant;
            Lord lord = null;
            try
            {
                Find.WorldPawns.RemovePawn(servant);
                GenSpawn.Spawn(servant, cell, map, servant.Rotation, WipeMode.Vanish, respawningAfterLoad: true);
                if (!servant.Spawned || servant.Map != map || !servant.CanReachMapEdge()
                    || !EnemyContractUtility.HasEnemyContract(servant)
                    || ServantQuery.Instance.GetMaster(servant) != entry.EnemyMaster)
                    throw new InvalidOperationException("敌方再袭落点、撤退路线或契约无效。");
                lord = LordMaker.MakeNewLord(servant.Faction, new LordJob_EnemyWarParty(), map, new[] { servant });
                // Commit presence last; earlier failures must leave a resting spirit unchanged.
                if (!ServantLifecycleService.Instance.TryPrepareEnemyRaid(servant, out rejection))
                    throw new InvalidOperationException(rejection);
                entry.RecordEnemyDeployment(entry.EnemyMaster, servant);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    // LordMaker can throw after registering a partially constructed lord.
                    Lord activeLord = lord ?? servant.GetLord();
                    if (activeLord != null) map.lordManager.RemoveLord(activeLord);
                }
                finally
                {
                    if (servant.Spawned) servant.DeSpawn();
                    if (!Find.WorldPawns.Contains(servant))
                        Find.WorldPawns.PassToWorld(servant, PawnDiscardDecideMode.KeepForever);
                }
                Log.Error("[MoonWorld] 敌方从者再袭部署失败: " + ex);
                rejection = "敌方再袭失败，原从者已退回场外，保留契约与休整时间。";
                return false;
            }
        }

        public static void RetainDepartedPawn(Pawn pawn)
        {
            EnemyWarParticipant entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(pawn);
            if (entry == null || (pawn != entry.EnemyMaster && pawn != entry.EnemyServant)
                || pawn.Spawned || pawn.Dead || pawn.Destroyed || !EnemyContractUtility.IsWarPawn(pawn)) return;
            // Pawn.ExitMap has already transferred it to WorldPawns and written its native timestamp.
            // Do not remove and re-add it here: that makes the rest clock mutable.
            if (Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
            if (pawn == entry.EnemyServant) entry.RecordEnemyDeparture(pawn);
        }
    }
}
