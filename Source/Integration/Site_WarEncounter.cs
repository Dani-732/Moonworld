using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    // A temporary location, not a workshop: no owner, buildings, escape or reconstruction flags.
    public sealed class Site_WarEncounter : Site
    {
        public override AcceptanceReport CanBeSettled => "临时交战地点不可定居。";
        public override bool GravShipCanLandOn => false;

        public override string GetInspectString()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (EnemyBattleService.AtSite(this))
                return base.GetInspectString() + "\n交战：" + war.enemyBattle.attacker.LabelShortCap + " / "
                    + war.enemyBattle.defender.LabelShortCap + "；已结算 " + war.enemyBattle.rounds + " 回合。";
            if (EnemyChallengeService.AtSite(this))
                return base.GetInspectString() + "\n约战从者：" + war.enemyChallenge.servant.LabelShortCap
                    + (war.enemyChallenge.onMap ? "\n已赴约，正在交战。" : "\n等待赴约，还剩 "
                        + (Math.Max(0, war.enemyChallenge.expiresAtTickAbs - GenTicks.TicksAbs) / 2500f).ToString("F1")
                        + " 小时。御主或从者到场即开战；逾期不自动突袭基地。");
            return base.GetInspectString() + "\n本次交锋已结束。";
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            EnemyBattleService.TryMaterialize(this);
            // Caravan pawns may be spawned after this hook; challenge service checks their arrival each 250 ticks.
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (Destroyed) return;
            if (HasMap && EnemyBattleService.AtSite(this)) EnemyBattleService.TryMaterialize(this);
            WarEncounterSiteUtility.Cleanup(this);
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            bool remove = base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
            if (EnemyBattleService.AtSite(this) || EnemyChallengeService.AtSite(this)) alsoRemoveWorldObject = false;
            return remove;
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            EnemyBattleService.BeforeMapRemoval(this);
            EnemyChallengeService.BeforeMapRemoval(this);
            base.Notify_MyMapAboutToBeRemoved();
        }

        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
    }
}
