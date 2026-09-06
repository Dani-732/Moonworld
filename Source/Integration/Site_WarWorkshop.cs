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
        public Pawn OwnerMaster => ownerMaster;
        internal void SetOwner(Pawn master) { ownerMaster = master; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerMaster, "ownerMaster");
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n所属御主：" + (ownerMaster?.LabelShortCap ?? "未知")
                + "\n魔术工坊地图尚未开放。";
        }

        // This slice exposes a persistent location, without creating an unfinished combat map.
        public override AcceptanceReport CanBeSettled => "魔术工坊暂不可进入或定居。";
        public override bool GravShipCanLandOn => false;
        public override IEnumerable<Gizmo> GetGizmos() { yield break; }
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan) { yield break; }
        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
    }
}
