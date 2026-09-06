using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    internal static class WarWorkshopService
    {
        internal static bool HasSurvivingOwner(Site_WarWorkshop site)
        {
            EnemyWarParticipant entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(site.OwnerMaster);
            return entry != null && entry.EnemyMaster == site.OwnerMaster && !entry.EnemyEliminated;
        }

        internal static bool TryPlaceDefenders(Site_WarWorkshop site)
        {
            if (site.RetreatOrdered) return true;
            EnemyWarParticipant entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(site.OwnerMaster);
            if (entry == null || site.OwnerMaster != entry.EnemyMaster) return true;
            Map map = site.Map;
            if (map == null) return false;
            // Query before moving the master: raid readiness intentionally requires both pawns off-map.
            bool ready = EnemyRestUtility.ReadinessRejection(entry.EnemyServant) == null;
            var moved = new List<Pawn>();
            try
            {
                Place(entry.EnemyMaster, map, moved, servant: false);
                Place(entry.EnemyServant, map, moved, servant: true);
                if (ready && moved.Contains(entry.EnemyServant)
                    && ServantQuery.Instance.IsSpirit(entry.EnemyServant))
                {
                    if (!ServantLifecycleService.Instance.TryRematerialize(entry.EnemyMaster, entry.EnemyServant, out string reason))
                        throw new InvalidOperationException(reason);
                }
                return true;
            }
            catch (Exception ex)
            {
                foreach (Pawn pawn in moved)
                {
                    Lord lord = pawn.GetLord();
                    if (lord != null) map.lordManager.RemoveLord(lord);
                    if (pawn.Spawned) pawn.DeSpawn();
                    if (!Find.WorldPawns.Contains(pawn))
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                }
                Log.Error("[MoonWorld] 工坊主从落地失败，已退回原世界角色，稍后重试：" + ex);
                return false;
            }
        }

        private static void Place(Pawn pawn, Map map, List<Pawn> moved, bool servant)
        {
            // Pawns on a raid map, in a caravan, captured or inside a transporter are never pulled out.
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Spawned || pawn.ParentHolder != null
                || pawn.IsPrisoner || pawn.IsSlave || !EnemyContractUtility.IsWarPawn(pawn)
                || !Find.WorldPawns.Contains(pawn)) return;
            if (servant && (pawn.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated
                || !EnemyContractUtility.HasEnemyContract(pawn))) return;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 35,
                c => c.Standable(map) && c.GetFirstPawn(map) == null
                    && map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)), out IntVec3 cell))
                throw new InvalidOperationException("工坊附近没有可通行且连通地图边缘的主从落点。");
            moved.Add(pawn);
            Find.WorldPawns.RemovePawn(pawn);
            GenSpawn.Spawn(pawn, cell, map, pawn.Rotation, WipeMode.Vanish, respawningAfterLoad: true);
            if (!pawn.Spawned || pawn.Map != map) throw new InvalidOperationException("工坊角色未在预期地图落地。");
            if (servant)
                LordMaker.MakeNewLord(pawn.Faction, new LordJob_EnemyWarParty(), map, new[] { pawn });
            else
                LordMaker.MakeNewLord(pawn.Faction, new LordJob_DefendBase(pawn.Faction, cell, 25000), map, new[] { pawn });
        }

        internal static void ReturnDefendersToWorld(Site_WarWorkshop site)
        {
            if (site.RetreatOrdered) return;
            EnemyWarParticipant entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(site.OwnerMaster);
            if (entry == null || site.OwnerMaster != entry.EnemyMaster) return;
            Return(entry.EnemyMaster, site.Map);
            Return(entry.EnemyServant, site.Map);
        }

        internal static bool HasWithdrawingPawnOnMap(Site_WarWorkshop site)
        {
            var enemy = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(site.OwnerMaster);
            return enemy != null && (IsFreeOnMap(enemy.EnemyMaster, site.Map) || IsFreeOnMap(enemy.EnemyServant, site.Map));
        }

        private static bool IsFreeOnMap(Pawn pawn, Map map)
        {
            return map != null && pawn != null && pawn.MapHeld == map && !pawn.Dead && !pawn.Destroyed
                && !pawn.IsPrisoner && !pawn.IsSlave;
        }

        internal static void OrderRetreat(Site_WarWorkshop site, EnemyWarParticipant enemy)
        {
            Pawn master = enemy.EnemyMaster;
            if (IsFreeOnMap(master, site.Map) && master.Spawned && !(master.GetLord()?.LordJob is LordJob_WorkshopRetreat))
            {
                Lord previous = master.GetLord();
                if (previous != null) site.Map.lordManager.RemoveLord(previous);
                try
                {
                    LordMaker.MakeNewLord(master.Faction, new LordJob_WorkshopRetreat(), site.Map, new[] { master });
                }
                catch
                {
                    Lord partial = master.GetLord();
                    if (partial != null) site.Map.lordManager.RemoveLord(partial);
                    throw;
                }
            }
            Pawn servant = enemy.EnemyServant;
            if (IsFreeOnMap(servant, site.Map) && servant.Spawned)
                (servant.GetLord()?.LordJob as LordJob_EnemyWarParty)?.BeginRetreat();
        }

        private static void Return(Pawn pawn, Map map)
        {
            if (pawn == null || map == null || !pawn.Spawned || pawn.Map != map || pawn.Dead || pawn.Destroyed
                || pawn.IsPrisoner || pawn.IsSlave || !EnemyContractUtility.IsWarPawn(pawn)) return;
            Lord lord = pawn.GetLord();
            if (lord != null) map.lordManager.RemoveLord(lord);
            pawn.DeSpawn();
            if (!Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            EnemyWarPartyService.RetainDepartedPawn(pawn);
        }
    }
}
