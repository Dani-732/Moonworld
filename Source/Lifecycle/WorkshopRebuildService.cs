using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    internal static class WorkshopRebuildService
    {
        internal static bool IsFreeSurvivor(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Spawned && pawn.ParentHolder == null
                && !pawn.IsPrisoner && !pawn.IsSlave && Find.WorldPawns.Contains(pawn);
        }

        internal static bool HasWorkshop(EnemyWarParticipant enemy)
        {
            foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                if (worldObject is Site_WarWorkshop site && !site.Destroyed && site.OwnerMaster == enemy.EnemyMaster)
                    return true;
            return false;
        }

        internal static bool BlocksRaid(EnemyWarParticipant enemy)
        {
            if (enemy.WorkshopRebuildPending) return true;
            foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                if (worldObject is Site_WarWorkshop site && !site.Destroyed && site.OwnerMaster == enemy.EnemyMaster
                    && site.RetreatOrdered) return true;
            return false;
        }

        internal static void Schedule(Pawn master, PlanetTile oldTile)
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            var enemy = war?.CurrentWarEntry?.FindEnemy(master);
            if (war?.CurrentWarOutcome != WarOutcome.Ongoing || enemy == null || enemy.EnemyEliminated
                || !IsFreeSurvivor(master) || !IsFreeSurvivor(enemy.EnemyServant)
                || !EnemyContractUtility.HasEnemyContract(enemy.EnemyServant) || HasWorkshop(enemy)) return;
            enemy.ScheduleWorkshopRebuild(oldTile);
        }

        internal static void Tick(GameComponent_MoonWorld war)
        {
            if (war.CurrentWarOutcome != WarOutcome.Ongoing || war.CurrentWarEntry == null) return;
            foreach (var enemy in war.CurrentWarEntry.Enemies)
            {
                if (!enemy.WorkshopRebuildPending) continue;
                if (enemy.EnemyEliminated) { enemy.CompleteWorkshopRebuild(); continue; }
                if (HasWorkshop(enemy)) { enemy.CompleteWorkshopRebuild(); continue; }
                if (GenTicks.TicksAbs < enemy.WorkshopRebuildAtTickAbs || !IsFreeSurvivor(enemy.EnemyMaster)
                    || !IsFreeSurvivor(enemy.EnemyServant) || EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) != null) continue;
                Rebuild(enemy);
            }
        }

        private static void Rebuild(EnemyWarParticipant enemy)
        {
            Site_WarWorkshop site = null;
            try
            {
                PlanetTile origin = enemy.LostWorkshopTile;
                if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, origin, selectLandmarkChance: 0f, layer: origin.Layer)
                    || tile == origin) return;
                site = (Site_WarWorkshop)WorldObjectMaker.MakeWorldObject(MW_DefOf.MW_WarWorkshop);
                site.Tile = tile;
                site.SetFaction(enemy.EnemyMaster.Faction);
                site.SetOwner(enemy.EnemyMaster);
                site.AddPart(new SitePart(site, MW_DefOf.MW_WarWorkshopPart, new SitePartParams()));
                Find.WorldObjects.Add(site);
                if (!site.Spawned || site.Destroyed || enemy.EnemyEliminated || !IsFreeSurvivor(enemy.EnemyMaster)
                    || !IsFreeSurvivor(enemy.EnemyServant) || !EnemyContractUtility.HasEnemyContract(enemy.EnemyServant))
                    throw new InvalidOperationException("工坊重建期间原主从或新据点失效。");
            }
            catch (Exception ex)
            {
                if (site != null && !site.Destroyed) site.Destroy();
                Log.Warning("[MoonWorld] 工坊重建失败，保留原主从与待重建记录：" + ex.Message);
                return;
            }
            enemy.CompleteWorkshopRebuild();
            Messages.Message("敌方御主已在新地点重建魔术工坊。", enemy.EnemyMaster, MessageTypeDefOf.ThreatBig, false);
        }
    }
}
