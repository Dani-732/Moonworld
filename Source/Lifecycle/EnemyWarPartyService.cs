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

        public static bool TryDeploy(Map map, IntVec3 cell, out string rejection)
        {
            rejection = null;
            GameComponent_MoonWorld war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            HolyGrailWarEntry entry = war?.CurrentWarEntry;
            if (generating || war == null || war.warStartTick < 0 || entry == null || !entry.RegularSummonUsed)
            { rejection = "请先完成本届玩家召唤。"; return false; }
            if (entry.EnemyDeployed)
            { rejection = "本届敌方主从已经部署，不能重新生成。"; return false; }
            if (map == null || !map.IsPlayerHome || !map.CanEverExit || !cell.InBounds(map)
                || !cell.Standable(map) || cell.Fogged(map))
            { rejection = "请选择有地图出口的玩家基地内已探索的可站立格。"; return false; }
            if (!TryResolveParticipants(entry, out rejection)) return false;
            Pawn playerMaster = entry.DesignatedMaster;
            if (playerMaster != null && (playerMaster.Dead || !playerMaster.Spawned || playerMaster.Map != map))
            { rejection = "本届玩家御主必须存活并位于该地图。"; return false; }
            IntVec3 servantCell;
            if (!CellFinder.TryFindRandomCellNear(cell, map, 3,
                c => c != cell && c.Standable(map) && !c.Fogged(map), out servantCell))
            { rejection = "附近没有可供敌方从者落地的位置。"; return false; }

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
                GenSpawn.Spawn(master, cell, map, WipeMode.Vanish);
                GenSpawn.Spawn(servant, servantCell, map, WipeMode.Vanish);
                if (!master.CanReachMapEdge() || !servant.CanReachMapEdge())
                    throw new InvalidOperationException("敌方落点必须有可通行的撤退路线。");
                if (!ServantLifecycleService.Instance.TryBindEnemy(master, servant, out rejection))
                    throw new InvalidOperationException(rejection);
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                if (prana == null) throw new InvalidOperationException("敌方从者缺少魔力 Need。");
                prana.CurLevel = prana.MaxLevel;
                lord = LordMaker.MakeNewLord(faction, new LordJob_EnemyWarParty(), map, new[] { master, servant });
                if (master.Dead || servant.Dead || !master.Spawned || !servant.Spawned
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
            // Preserve the actual participants for later encounters; do not regenerate away their damage.
            if (Find.WorldPawns.Contains(pawn)) Find.WorldPawns.RemovePawn(pawn);
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }
    }
}
