using System;
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
            if (generating || war == null || war.warStartTick < 0 || entry == null || !entry.RegularSummonUsed)
                return "请先完成本届玩家召唤，并等待当前部署结束。";
            if (map == null || !map.IsPlayerHome || !map.CanEverExit)
                return "敌方突袭需要有出口的玩家基地。";
            Pawn playerMaster = entry.DesignatedMaster;
            if (playerMaster == null || playerMaster.Dead || playerMaster.Destroyed || !playerMaster.Spawned
                || playerMaster.Map != map || playerMaster.Faction != Faction.OfPlayer
                || playerMaster.IsPrisoner || playerMaster.IsSlave)
                return "本届玩家御主必须存活、自由且位于该基地。";
            if (entry.EnemyEliminated) return "本届敌对阵营已淘汰，不会再袭或重新生成。";
            return entry.EnemyDeployed ? EnemyRestUtility.ReadinessRejection(entry.EnemyServant) : null;
        }

        public static bool TryDeploy(Map map, IntVec3 cell, out string rejection)
        {
            rejection = ValidateRaid(map);
            if (rejection != null) return false;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                || cell.GetFirstPawn(map) != null)
            { rejection = "请选择已探索且未被角色占用的可站立格。"; return false; }
            HolyGrailWarEntry entry = Current.Game.GetComponent<GameComponent_MoonWorld>().CurrentWarEntry;
            if (!TryResolveParticipants(entry, out rejection)) return false;
            if (entry.EnemyDeployed)
            {
                generating = true;
                try { return TryRedeployExisting(entry, map, cell, out rejection); }
                finally { generating = false; }
            }

            generating = true;
            Pawn master = null;
            Pawn servant = null;
            Lord lord = null;
            try
            {
                Faction faction = Find.FactionManager.FirstFactionOfDef(MW_DefOf.MW_WarOpposition);
                if (faction == null)
                {
                    faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(MW_DefOf.MW_WarOpposition, hidden: true));
                    Find.FactionManager.Add(faction);
                }
                if (!faction.HostileTo(Faction.OfPlayer))
                    throw new InvalidOperationException("敌方派系必须与玩家敌对。");
                master = PawnGenerator.GeneratePawn(new PawnGenerationRequest(MW_DefOf.MW_EnemyMaster, faction,
                    PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    validatorPreGear: pawn => { master = pawn; return true; }));
                if (master == null) throw new InvalidOperationException("敌方御主生成失败。");
                if (!MasterCircuitUtility.HasCircuit(master))
                    master.story.traits.GainTrait(new Trait(MW_DefOf.MW_MagusCircuit_Basic));
                if (!master.story.traits.HasTrait(MW_DefOf.MW_MageRank_Apprentice))
                    master.story.traits.GainTrait(new Trait(MW_DefOf.MW_MageRank_Apprentice));
                servant = PawnGenerator.GeneratePawn(new PawnGenerationRequest(entry.EnemyIdentity.servantKind, faction,
                    PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    validatorPreGear: pawn => { servant = pawn; return true; }));
                if (servant == null) throw new InvalidOperationException("敌方从者生成失败。");
                // The master stays off-map; only the servant takes part in the raid.
                Find.WorldPawns.PassToWorld(master, PawnDiscardDecideMode.KeepForever);
                GenSpawn.Spawn(servant, cell, map, WipeMode.Vanish);
                if (!servant.CanReachMapEdge())
                    throw new InvalidOperationException("敌方落点必须有可通行的撤退路线。");
                if (!ServantLifecycleService.Instance.TryBindEnemy(master, servant, out rejection))
                    throw new InvalidOperationException(rejection);
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                if (prana == null) throw new InvalidOperationException("敌方从者缺少魔力 Need。");
                prana.CurLevel = prana.MaxLevel;
                lord = LordMaker.MakeNewLord(faction, new LordJob_EnemyWarParty(), map, new[] { servant });
                if (master.Dead || master.Destroyed || servant.Dead || master.Spawned || !servant.Spawned
                    || !Find.WorldPawns.Contains(master)
                    || !EnemyContractUtility.HasEnemyContract(servant))
                    throw new InvalidOperationException("敌方生成后身份或契约无效。");
                entry.RecordEnemyDeployment(master, servant);
                return true;
            }
            catch (Exception ex)
            {
                if (lord != null) map.lordManager.RemoveLord(lord);
                try { ServantSummoningService.Rollback(servant); }
                finally { ServantSummoningService.Rollback(master); }
                Log.Error("[MoonWorld] 敌方主从部署失败: " + ex);
                rejection = "敌方部署失败，未消费部署机会；玩家召唤与开战时间保持。";
                return false;
            }
            finally { generating = false; }
        }

        private static bool TryRedeployExisting(HolyGrailWarEntry entry, Map map, IntVec3 cell, out string rejection)
        {
            rejection = null;
            Pawn servant = entry.EnemyServant;
            Lord lord = null;
            try
            {
                Find.WorldPawns.RemovePawn(servant);
                GenSpawn.Spawn(servant, cell, map, WipeMode.Vanish);
                if (!servant.Spawned || servant.Map != map || !servant.CanReachMapEdge()
                    || !EnemyContractUtility.HasEnemyContract(servant)
                    || ServantQuery.Instance.GetMaster(servant) != entry.EnemyMaster)
                    throw new InvalidOperationException("敌方再袭落点、撤退路线或契约无效。");
                lord = LordMaker.MakeNewLord(servant.Faction, new LordJob_EnemyWarParty(), map, new[] { servant });
                // Commit presence last; earlier failures must leave a resting spirit unchanged.
                if (!ServantLifecycleService.Instance.TryPrepareEnemyRaid(servant, out rejection))
                    throw new InvalidOperationException(rejection);
                entry.ClearEnemyRestStart();
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

        private static bool TryResolveParticipants(HolyGrailWarEntry entry, out string rejection)
        {
            rejection = null;
            if (entry.PlayerIdentity != null && entry.EnemyIdentity != null) return true;
            // Legacy saves lack the first summoned identity; only infer it when unambiguous.
            ServantIdentityDef found = null;
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
            {
                Pawn master = ServantQuery.Instance.GetMaster(pawn);
                if (master?.Faction != Faction.OfPlayer
                    || (entry.DesignatedMaster != null && master != entry.DesignatedMaster)) continue;
                ServantIdentityDef identity = ServantIdentityUtility.GetIdentity(pawn);
                if (HolyGrailWarClassUtility.Opponent(identity?.warClass ?? HolyGrailWarClass.None) == HolyGrailWarClass.None) continue;
                if (found != null && found != identity)
                { rejection = "旧档存在不同职阶契约，无法确定本届首骑；请用新开局测试敌方。"; return false; }
                found = identity;
            }
            ServantIdentityDef opponent = HolyGrailWarClassUtility.PickOpponent(found);
            if (found == null || opponent == null)
            { rejection = "无法确定本届己方及敌方职阶，请使用新召唤后的存档。"; return false; }
            entry.SetParticipants(found, opponent);
            return true;
        }

        public static void RetainDepartedPawn(Pawn pawn)
        {
            HolyGrailWarEntry entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
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
