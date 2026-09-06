using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    public sealed class Site_WarWorkshop : Site
    {
        private Pawn ownerMaster;
        private bool defendersPlaced;
        private int nextPlacementRetryTick;
        private bool servantDefeatedHere;
        private bool retreatOrdered;
        private bool masterEscaped;
        private bool servantEscaped;
        private bool policyErrorLogged;
        public bool ServantDefeatedHere => servantDefeatedHere;
        public bool RetreatOrdered => retreatOrdered;
        public bool BothEscaped => masterEscaped && servantEscaped;
        public bool MasterEscaped => masterEscaped;
        public bool ServantEscaped => servantEscaped;
        public Pawn OwnerMaster => ownerMaster;
        internal void SetOwner(Pawn master) { ownerMaster = master; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerMaster, "ownerMaster");
            Scribe_Values.Look(ref defendersPlaced, "defendersPlaced", false);
            Scribe_Values.Look(ref servantDefeatedHere, "servantDefeatedHere", false);
            Scribe_Values.Look(ref retreatOrdered, "retreatOrdered", false);
            Scribe_Values.Look(ref masterEscaped, "masterEscaped", false);
            Scribe_Values.Look(ref servantEscaped, "servantEscaped", false);
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n所属御主：" + (ownerMaster?.LabelShortCap ?? "未知")
                + (retreatOrdered ? "\n守军正在撤离。御主逃脱：" + (masterEscaped ? "是" : "否")
                    + "；从者逃脱：" + (servantEscaped ? "是" : "否") : "\n可派远行队进攻。工坊毁坏不等于阵营淘汰。");
        }

        public override AcceptanceReport CanBeSettled => "敌方魔术工坊不可定居。";
        public override bool GravShipCanLandOn => false;
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            PlaceDefenders();
        }

        protected override void TickInterval(int delta)
        {
            EvaluateRetreat();
            base.TickInterval(delta);
            if (HasMap && !defendersPlaced && Find.TickManager.TicksGame >= nextPlacementRetryTick)
                PlaceDefenders();
        }

        private void PlaceDefenders()
        {
            if (!HasMap || defendersPlaced || retreatOrdered) return;
            defendersPlaced = WarWorkshopService.TryPlaceDefenders(this);
            nextPlacementRetryTick = Find.TickManager.TicksGame + 2500;
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            // Preserve the running map while withdrawing pawns still need an actual exit.
            if (retreatOrdered && WarWorkshopService.HasWithdrawingPawnOnMap(this))
            { alsoRemoveWorldObject = false; return false; }
            bool remove = base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
            // An ordinary retreat preserves the site; losing buildings alone never eliminates a faction.
            if (remove && WarWorkshopService.HasSurvivingOwner(this)) alsoRemoveWorldObject = retreatOrdered;
            return remove;
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            if (!retreatOrdered) WarWorkshopService.ReturnDefendersToWorld(this);
            base.Notify_MyMapAboutToBeRemoved();
            defendersPlaced = false;
            if (!retreatOrdered) servantDefeatedHere = false;
        }

        public void NotifyServantDefeated(Pawn pawn)
        {
            var enemy = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(ownerMaster);
            if (!HasMap || pawn == null || !pawn.Spawned || pawn.Map != Map || enemy?.EnemyServant != pawn
                || pawn.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.DefeatedSpirit
                || ownerMaster == null || !ownerMaster.Spawned || ownerMaster.Map != Map) return;
            servantDefeatedHere = true;
            EvaluateRetreat();
        }

        internal void EvaluateRetreat()
        {
            if (!HasMap || Destroyed) return;
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            var enemy = war?.CurrentWarEntry?.FindEnemy(ownerMaster);
            if (enemy == null) return;
            try
            {
                if (!retreatOrdered && war.CurrentWarOutcome == WarOutcome.Ongoing && !enemy.EnemyEliminated
                    && ownerMaster.Spawned && ownerMaster.Map == Map && !ownerMaster.IsPrisoner && !ownerMaster.IsSlave
                    && enemy.Seat?.RetreatPolicy.ShouldRetreat(this, ownerMaster, enemy.EnemyServant) == true)
                {
                    retreatOrdered = true;
                    Messages.Message("工坊御主决定撤退，正在寻找离开战场的路线。", ownerMaster, MessageTypeDefOf.ThreatBig, false);
                }
                if (retreatOrdered) WarWorkshopService.OrderRetreat(this, enemy);
            }
            catch (Exception ex)
            {
                if (!policyErrorLogged) Log.Error("[MoonWorld] 工坊撤退决策或职责安排失败，将重试：" + ex);
                policyErrorLogged = true;
            }
        }

        public void NotifyPawnExited(Pawn pawn)
        {
            if (!retreatOrdered || !WorkshopRebuildService.IsFreeSurvivor(pawn)) return;
            var enemy = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(ownerMaster);
            if (enemy == null) return;
            if (pawn == ownerMaster) masterEscaped = true;
            if (pawn == enemy.EnemyServant) servantEscaped = true;
        }

        public override void Destroy()
        {
            if (Destroyed) return;
            bool escaped = BothEscaped && !HasMap;
            base.Destroy();
            if (escaped) WorkshopRebuildService.Schedule(ownerMaster, Tile);
        }

        // First slice supports the native caravan attack/reform flow; transport arrivals remain deferred.
        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
    }
}
