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
        public Pawn OwnerMaster => ownerMaster;
        internal void SetOwner(Pawn master) { ownerMaster = master; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerMaster, "ownerMaster");
            Scribe_Values.Look(ref defendersPlaced, "defendersPlaced", false);
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n所属御主：" + (ownerMaster?.LabelShortCap ?? "未知")
                + "\n可派远行队进攻。工坊毁坏不等于阵营淘汰。";
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
            base.TickInterval(delta);
            if (HasMap && !defendersPlaced && Find.TickManager.TicksGame >= nextPlacementRetryTick)
                PlaceDefenders();
        }

        private void PlaceDefenders()
        {
            if (!HasMap || defendersPlaced) return;
            defendersPlaced = WarWorkshopService.TryPlaceDefenders(this);
            nextPlacementRetryTick = Find.TickManager.TicksGame + 2500;
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            bool remove = base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
            // An ordinary retreat preserves the site; losing buildings alone never eliminates a faction.
            if (remove && WarWorkshopService.HasSurvivingOwner(this)) alsoRemoveWorldObject = false;
            return remove;
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            WarWorkshopService.ReturnDefendersToWorld(this);
            base.Notify_MyMapAboutToBeRemoved();
            defendersPlaced = false;
        }

        // First slice supports the native caravan attack/reform flow; transport arrivals remain deferred.
        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
    }
}
