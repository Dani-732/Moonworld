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

        internal static bool HasWorkshop(EnemyWarParticipant enemy) => FindWorkshop(enemy) != null;

        internal static Site_WarWorkshop FindWorkshop(EnemyWarParticipant enemy)
        {
            foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                if (worldObject is Site_WarWorkshop site && !site.Destroyed && site.OwnerMaster == enemy.EnemyMaster)
                    return site;
            return null;
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
                TryRebuild(war, enemy, out _);
            }
        }

        internal static string RebuildRejection(GameComponent_MoonWorld war, EnemyWarParticipant enemy, bool ignoreTime = false)
        {
            if (war == null || war.CurrentWarOutcome != WarOutcome.Ongoing)
                return "本届战争没有进行中。";
            if (enemy == null || war.CurrentWarEntry == null || !war.CurrentWarEntry.Enemies.Contains(enemy))
                return "不是本届敌方阵营。";
            if (enemy.EnemyEliminated) return "该阵营已淘汰，不能重建或复活。";
            Site_WarWorkshop existing = FindWorkshop(enemy);
            if (existing != null)
                return "所选阵营 " + enemy.Seat?.label + " 已有工坊 #" + existing.ID + "（地块 " + existing.Tile + "）。"
                    + (existing.RetreatOrdered
                        ? "该工坊已下达撤退命令，尚未完成移除；请核对双人逃脱及地图清理。"
                        : "该工坊未下达撤退命令，不需要重复重建；请核对选择的职阶和从者。");
            if (!enemy.WorkshopRebuildPending) return "没有待重建记录；缺少双人逃脱及旧工坊移除记录，不能凭空补建。";
            if (!IsFreeSurvivor(enemy.EnemyMaster)) return "原御主不是自由场外角色；请查看御主位置、存活、被俘及持有容器。";
            if (!IsFreeSurvivor(enemy.EnemyServant)) return "原从者不是自由场外角色；请查看从者位置、存活、被俘及持有容器。";
            if (!ignoreTime && GenTicks.TicksAbs < enemy.WorkshopRebuildAtTickAbs)
                return "重建等待尚余 " + ((enemy.WorkshopRebuildAtTickAbs - GenTicks.TicksAbs) / 60000f).ToString("F2") + " 天。";
            return EnemyRestUtility.ReadinessRejection(enemy.EnemyServant, ignoreTime);
        }

        internal static bool TryRebuild(GameComponent_MoonWorld war, EnemyWarParticipant enemy, out string rejection, bool ignoreTime = false)
        {
            rejection = RebuildRejection(war, enemy, ignoreTime);
            if (rejection != null) return false;
            Site_WarWorkshop site = null;
            try
            {
                PlanetTile origin = enemy.LostWorkshopTile;
                if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, origin, selectLandmarkChance: 0f, layer: origin.Layer)
                    || tile == origin)
                { rejection = "没有找到不同于旧工坊的有效新地块，保留记录等待重试。"; return false; }
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
                rejection = "创建工坊失败：" + ex.Message;
                return false;
            }
            enemy.CompleteWorkshopRebuild();
            Messages.Message("敌方御主已在新地点重建魔术工坊。", enemy.EnemyMaster, MessageTypeDefOf.ThreatBig, false);
            return true;
        }
    }
}
