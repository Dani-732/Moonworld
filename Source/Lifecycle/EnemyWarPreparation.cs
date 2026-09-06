using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    // Owned by the summoning transaction until its final commit. No war state is written here.
    internal sealed class EnemyWarPreparation : IDisposable
    {
        internal Pawn Master { get; private set; }
        internal Pawn Servant { get; private set; }
        private Site_WarWorkshop workshop;
        private bool ownsPawns;
        private bool committed;

        internal void Prepare(Map origin, ServantIdentityDef identity, HolyGrailWarEntry existing = null)
        {
            if (origin == null || !TileFinder.TryFindNewSiteTile(out PlanetTile tile, origin.Tile,
                selectLandmarkChance: 0f, layer: origin.Tile.Layer))
                throw new InvalidOperationException("附近没有可建立敌方魔术工坊的世界地块。");

            if (existing != null && existing.HasEnemyParticipants)
            {
                // Upgrade the site only. Never replace an old, wounded or deployed participant.
                Master = existing.EnemyMaster;
                Servant = existing.EnemyServant;
                if (existing.EnemyEliminated)
                    throw new InvalidOperationException("已淘汰敌方不能重新建立初始工坊。");
            }
            else
            {
                ownsPawns = true;
                FactionDef oppositionDef = OppositionDef(identity.warClass);
                Faction faction = Find.FactionManager.FirstFactionOfDef(oppositionDef);
                if (faction == null)
                {
                    faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(oppositionDef, hidden: true));
                    Find.FactionManager.Add(faction);
                }
                if (!faction.HostileTo(Faction.OfPlayer))
                    throw new InvalidOperationException("敌方派系必须与玩家敌对。");
                Master = PawnGenerator.GeneratePawn(new PawnGenerationRequest(MW_DefOf.MW_EnemyMaster, faction,
                    PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    validatorPreGear: pawn => { Master = pawn; return true; }));
                if (Master == null) throw new InvalidOperationException("敌方御主生成失败。");
                if (!MasterCircuitUtility.HasCircuit(Master))
                    Master.story.traits.GainTrait(new Trait(MW_DefOf.MW_MagusCircuit_Basic));
                if (!Master.story.traits.HasTrait(MW_DefOf.MW_MageRank_Apprentice))
                    Master.story.traits.GainTrait(new Trait(MW_DefOf.MW_MageRank_Apprentice));
                Servant = PawnGenerator.GeneratePawn(new PawnGenerationRequest(identity.servantKind, faction,
                    PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    validatorPreGear: pawn => { Servant = pawn; return true; }));
                if (Servant == null) throw new InvalidOperationException("敌方从者生成失败。");
                HolyGrailWarContentBridge.InitializeWorldServant(Servant);
                PawnNeedAccess.EnsureNeed(Servant, MW_DefOf.MW_Prana);
                NoblePhantasmService.EnsureAbilities(Servant);
                if (!ServantLifecycleService.Instance.TryBindEnemy(Master, Servant, out string rejection))
                    throw new InvalidOperationException(rejection);
                Need_Prana prana = Servant.needs.TryGetNeed<Need_Prana>();
                if (prana == null) throw new InvalidOperationException("敌方从者缺少魔力 Need。");
                prana.CurLevel = prana.MaxLevel;
                Find.WorldPawns.PassToWorld(Master, PawnDiscardDecideMode.KeepForever);
                Find.WorldPawns.PassToWorld(Servant, PawnDiscardDecideMode.KeepForever);
                if (Master.Dead || Master.Destroyed || Master.Spawned || Servant.Dead || Servant.Destroyed || Servant.Spawned
                    || !Find.WorldPawns.Contains(Master) || !Find.WorldPawns.Contains(Servant)
                    || !EnemyContractUtility.HasEnemyContract(Servant))
                    throw new InvalidOperationException("场外敌方主从或契约无效。");
            }

            workshop = (Site_WarWorkshop)WorldObjectMaker.MakeWorldObject(MW_DefOf.MW_WarWorkshop);
            workshop.Tile = tile;
            workshop.SetFaction(Master.Faction);
            workshop.SetOwner(Master);
            workshop.AddPart(new SitePart(workshop, MW_DefOf.MW_WarWorkshopPart, new SitePartParams()));
            Find.WorldObjects.Add(workshop);
            if (!workshop.Spawned) throw new InvalidOperationException("敌方工坊未成功加入世界地图。");
            if (Master.Dead || Master.Destroyed || Servant.Dead || Servant.Destroyed
                || Master.IsPrisoner || Master.IsSlave || Servant.IsPrisoner || Servant.IsSlave
                || ServantQuery.Instance.GetMaster(Servant) != Master || !EnemyContractUtility.HasEnemyContract(Servant)
                || Servant.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated
                || (ownsPawns && (Master.Spawned || Servant.Spawned
                    || !Find.WorldPawns.Contains(Master) || !Find.WorldPawns.Contains(Servant))))
                throw new InvalidOperationException("工坊建立期间敌方主从或契约发生变化。");
        }

        private static FactionDef OppositionDef(HolyGrailWarClass seat)
        {
            FactionDef selected = null;
            switch (seat)
            {
                case HolyGrailWarClass.Saber: selected = MW_DefOf.MW_WarOpposition_Saber; break;
                case HolyGrailWarClass.Archer: selected = MW_DefOf.MW_WarOpposition_Archer; break;
                case HolyGrailWarClass.Lancer: selected = MW_DefOf.MW_WarOpposition_Lancer; break;
                case HolyGrailWarClass.Assassin: selected = MW_DefOf.MW_WarOpposition_Assassin; break;
                case HolyGrailWarClass.Caster: selected = MW_DefOf.MW_WarOpposition_Caster; break;
                case HolyGrailWarClass.Rider: selected = MW_DefOf.MW_WarOpposition_Rider; break;
                case HolyGrailWarClass.Berserker: selected = MW_DefOf.MW_WarOpposition_Berserker; break;
            }
            return selected ?? MW_DefOf.MW_WarOpposition;
        }

        internal void Commit(HolyGrailWarEntry entry)
        {
            entry.RecordEnemyPreparation(Master, Servant);
            committed = true;
        }

        public void Dispose()
        {
            if (committed) return;
            try { if (workshop != null && !workshop.Destroyed) workshop.Destroy(); }
            finally
            {
                if (ownsPawns)
                {
                    try { ServantSummoningService.Rollback(Servant); }
                    finally { ServantSummoningService.Rollback(Master); }
                }
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
                if (!HolyGrailWarClassUtility.IsWarClass(identity?.warClass ?? HolyGrailWarClass.None)) continue;
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

        internal static void ReconcileLoadedWar(GameComponent_MoonWorld war)
        {
            HolyGrailWarEntry entry = war.CurrentWarEntry;
            if (war.CurrentWarOutcome != WarOutcome.Ongoing || war.warStartTick < 0 || entry == null
                || !entry.RegularSummonUsed || entry.EnemyPrepared || entry.EnemyEliminated) return;
            // Older ongoing wars receive their missing startup objects once, after full load.
            Map origin = entry.DesignatedMaster?.Map;
            if (origin == null || entry.DesignatedMaster.Dead || entry.DesignatedMaster.Destroyed) return;
            try
            {
                if (!TryResolveParticipants(entry, out string rejection))
                    throw new InvalidOperationException(rejection);
                using (var preparation = new EnemyWarPreparation())
                {
                    preparation.Prepare(origin, entry.EnemyIdentity, entry);
                    preparation.Commit(entry);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MoonWorld] 旧档敌方开战准备失败，保留原参与者和开战时间，可读档重试：" + ex);
            }
        }
    }
}
