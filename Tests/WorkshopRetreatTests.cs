using System;
using MoonWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

internal static partial class SummoningTests
{
    public sealed class MageRetreatPolicy : WorkshopRetreatPolicy
    {
        public static bool Ready;
        public override bool ShouldRetreat(Site_WarWorkshop workshop, Pawn master, Pawn servant)
            => workshop.ServantDefeatedHere && Ready;
    }

    private static Site_WarWorkshop EnterWorkshop()
    {
        PrepareEnemy();
        var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
        site.Map = new Map { IsPlayerHome = false, Parent = site };
        site.PostMapGenerate();
        return site;
    }

    private static void DefeatAt(Site_WarWorkshop site)
    {
        Pawn servant = State.CurrentWarEntry.FindEnemy(site.OwnerMaster).EnemyServant;
        servant.State.PresenceState = ServantPresenceState.DefeatedSpirit;
        site.NotifyServantDefeated(servant);
    }

    private static void ExitWorkshop(Site_WarWorkshop site, Pawn pawn)
    {
        pawn.DeSpawn();
        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        EnemyWarPartyService.RetainDepartedPawn(pawn);
        site.NotifyPawnExited(pawn);
    }

    private static EnemyWarParticipant AbandonWorkshop()
    {
        var site = EnterWorkshop();
        var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
        DefeatAt(site);
        ExitWorkshop(site, enemy.EnemyMaster);
        ExitWorkshop(site, enemy.EnemyServant);
        Check(site.BothEscaped && site.ShouldRemoveMapNow(out bool remove) && remove, "escaped workshop not removable");
        site.Notify_MyMapAboutToBeRemoved();
        site.Map = null;
        site.Destroy();
        Check(enemy.WorkshopRebuildPending, "no rebuild scheduled");
        return enemy;
    }

    private static void ReachRebuildTime(EnemyWarParticipant enemy)
    {
        Find.TickManager.TicksGame += enemy.WorkshopRebuildAtTickAbs - GenTicks.TicksAbs;
    }

    private static void RunWorkshopTests()
    {
        Test("debug rebuild bypasses clocks but preserves original pawns and resources", () => {
            var enemy = AbandonWorkshop(); int rest = enemy.EnemyRestStartTickAbs;
            int now = GenTicks.TicksAbs; enemy.EnemyServant.needs.Prana.CurLevel = 85;
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out string reason) && reason.Contains("等待"), "normal timer bypassed");
            Check(WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true)
                && GenTicks.TicksAbs == now && enemy.EnemyRestStartTickAbs == rest
                && enemy.EnemyServant.needs.Prana.CurLevel == 85 && PawnGenerator.Created.Count == 3,
                "debug reset resources, clocks or generated pawns");
        });
        Test("debug rebuild reports blockers and never fabricates escape", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out string reason, ignoreTime: true)
                && reason.Contains("旧工坊"), "existing site bypassed");
            site.Map = null; site.Destroy();
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true)
                && reason.Contains("没有待重建记录"), "escape invented");
        });
        Test("debug rebuild still requires freedom health mana and active war", () => {
            var enemy = AbandonWorkshop(); int deadline = enemy.WorkshopRebuildAtTickAbs;
            enemy.EnemyMaster.IsPrisoner = true;
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out string reason, ignoreTime: true)
                && reason.Contains("御主"), "prisoner released");
            enemy.EnemyMaster.IsPrisoner = false; enemy.EnemyServant.needs.Prana.CurLevel = 0;
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true)
                && reason.Contains("魔力"), "mana filled");
            enemy.EnemyServant.needs.Prana.CurLevel = 90;
            enemy.EnemyServant.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 2 });
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true)
                && reason.Contains("伤势") && enemy.WorkshopRebuildAtTickAbs == deadline, "injury or deadline overwritten");
            enemy.EnemyServant.health.hediffSet.hediffs.Clear(); master.Dead = true; WarOutcomeService.Tick(State);
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true)
                && reason.Contains("战争"), "ended war rebuilt");
        });
        Test("debug rebuild exposes failed selection and retries without extending deadline", () => {
            var enemy = AbandonWorkshop(); int deadline = enemy.WorkshopRebuildAtTickAbs;
            TileFinder.Fail = true;
            Check(!WorkshopRebuildService.TryRebuild(State, enemy, out string reason, ignoreTime: true)
                && reason.Contains("地块") && enemy.WorkshopRebuildAtTickAbs == deadline, "selection failure hidden");
            TileFinder.Fail = false;
            Check(WorkshopRebuildService.TryRebuild(State, enemy, out reason, ignoreTime: true), "debug retry failed");
        });
        Test("workshop defeat orders both original pawns to retreat without teleport", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
            DefeatAt(site);
            Check(site.RetreatOrdered && enemy.EnemyMaster.Lord.LordJob is LordJob_WorkshopRetreat
                && ((LordJob_EnemyWarParty)enemy.EnemyServant.Lord.LordJob).Retreating, "retreat duties missing");
            Check(enemy.EnemyMaster.Spawned && enemy.EnemyServant.Spawned && !site.BothEscaped
                && !enemy.EnemyEliminated && !enemy.WorkshopRebuildPending, "defeat became escape or elimination");
            Lord lord = enemy.EnemyMaster.Lord; site.EvaluateRetreat();
            Check(enemy.EnemyMaster.Lord == lord, "repeat tick recreated retreat lord");
        });
        Test("voluntary spirit and foreign defeat do not signal workshop loss", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
            site.NotifyServantDefeated(enemy.EnemyServant);
            site.NotifyServantDefeated(master);
            Check(!site.ServantDefeatedHere && !site.RetreatOrdered, "non-defeat accepted");
            enemy.EnemyServant.State.PresenceState = ServantPresenceState.DefeatedSpirit;
            enemy.EnemyServant.Map = map; site.NotifyServantDefeated(enemy.EnemyServant);
            Check(!site.ServantDefeatedHere, "remote defeat accepted");
        });
        Test("external mage policy may keep fighting then later order retreat", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
            enemy.Seat.workshopRetreatPolicy = typeof(MageRetreatPolicy); MageRetreatPolicy.Ready = false;
            DefeatAt(site);
            Check(site.ServantDefeatedHere && !site.RetreatOrdered
                && enemy.EnemyMaster.Lord.LordJob is RimWorld.LordJob_DefendBase, "override ignored");
            MageRetreatPolicy.Ready = true; site.EvaluateRetreat();
            Check(site.RetreatOrdered, "later policy decision not evaluated");
            MageRetreatPolicy.Ready = false; site.EvaluateRetreat();
            Check(site.RetreatOrdered, "retreat order reversed mid-exit");
        });
        Test("partial native lord failure retries without recording an escape", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster);
            LordMaker.Fail = true; DefeatAt(site);
            Check(site.RetreatOrdered && enemy.EnemyMaster.Lord == null && !site.BothEscaped, "partial lord retained");
            LordMaker.Fail = false; site.EvaluateRetreat();
            Check(enemy.EnemyMaster.Lord.LordJob is LordJob_WorkshopRetreat, "retreat did not recover");
        });
        Test("blocked or downed withdrawal retains running map", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster); DefeatAt(site);
            Pawn.EdgeBlocked = true; enemy.EnemyMaster.Downed = true;
            ExitWorkshop(site, enemy.EnemyServant);
            Check(!site.ShouldRemoveMapNow(out bool remove) && !remove && !site.BothEscaped, "downed master auto-escaped");
            site.Notify_MyMapAboutToBeRemoved();
            Check(enemy.EnemyMaster.Spawned, "map unload despawned withdrawing master");
        });
        Test("capture or simple despawn is not successful escape", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster); DefeatAt(site);
            enemy.EnemyMaster.IsPrisoner = true; ExitWorkshop(site, enemy.EnemyMaster);
            ExitWorkshop(site, enemy.EnemyServant);
            Check(!site.BothEscaped, "prisoner counted as escape");
            site.Map = null; site.Destroy(); Check(!enemy.WorkshopRebuildPending, "captured master rebuilt");
        });
        Test("arbitrary site deletion does not schedule rebuilding", () => {
            PrepareEnemy(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Destroy();
            Check(!State.CurrentWarEntry.Enemies[0].WorkshopRebuildPending, "deletion fabricated escape");
        });
        Test("escaped guards cannot raid while player still occupies the old workshop", () => {
            var site = EnterWorkshop(); var enemy = State.CurrentWarEntry.FindEnemy(site.OwnerMaster); DefeatAt(site);
            ExitWorkshop(site, enemy.EnemyMaster); ExitWorkshop(site, enemy.EnemyServant);
            Find.TickManager.TicksGame += 180000;
            Check(EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null && !Deploy()
                && !enemy.WorkshopRebuildPending && Find.WorldObjects.All.Count == 1,
                "escaped guards redeployed before old workshop was resolved");
        });
        Test("ordinary player retreat preserves workshop and surviving guards", () => {
            var site = EnterWorkshop();
            Check(site.ShouldRemoveMapNow(out bool remove) && !remove, "ordinary retreat destroyed site");
            site.Notify_MyMapAboutToBeRemoved();
            Check(Find.WorldPawns.Contains(site.OwnerMaster) && !site.RetreatOrdered, "ordinary withdrawal changed");
        });
        Test("escaping original pair rebuilds after delay without generation or resource reset", () => {
            var enemy = AbandonWorkshop(); Pawn owner = enemy.EnemyMaster, servant = enemy.EnemyServant;
            servant.needs.Prana.CurLevel = 87; int rest = enemy.EnemyRestStartTickAbs;
            PlanetTile oldTile = enemy.LostWorkshopTile; int created = PawnGenerator.Created.Count;
            Check(!Deploy(), "homeless enemy raided before rebuilding");
            Check(State.warQuest.GetFirstPartOfType<QuestPart_HolyGrailWar>().DescriptionPart.Contains("等待休整并重建"),
                "pending rebuild absent from quest");
            Find.TickManager.TicksGame += 179999; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 0, "rebuild too early");
            Find.TickManager.TicksGame++; WorkshopRebuildService.Tick(State);
            var rebuilt = (Site_WarWorkshop)Find.WorldObjects.All[0];
            Check(!enemy.WorkshopRebuildPending && rebuilt.Tile != oldTile && rebuilt.OwnerMaster == owner
                && enemy.EnemyServant == servant && servant.State.Master == owner
                && PawnGenerator.Created.Count == created && servant.needs.Prana.CurLevel == 87
                && enemy.EnemyRestStartTickAbs == rest && State.CurrentWarOutcome == WarOutcome.Ongoing,
                "rebuild regenerated, reset resources or changed war");
            WorkshopRebuildService.Tick(State); Check(Find.WorldObjects.All.Count == 1, "duplicate workshop");
            Check(Deploy(), "rebuilt ready enemy cannot raid");
        });
        Test("rebuilding waits for prana and injury recovery", () => {
            var enemy = AbandonWorkshop(); ReachRebuildTime(enemy);
            enemy.EnemyServant.needs.Prana.CurLevel = 20; WorkshopRebuildService.Tick(State);
            Check(enemy.WorkshopRebuildPending && Find.WorldObjects.All.Count == 0, "low prana rebuilt");
            enemy.EnemyServant.needs.Prana.CurLevel = 90;
            enemy.EnemyServant.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 1 });
            WorkshopRebuildService.Tick(State); Check(Find.WorldObjects.All.Count == 0, "injured servant rebuilt");
            enemy.EnemyServant.health.hediffSet.hediffs.Clear(); WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 1, "recovered pair did not rebuild");
        });
        Test("captured or transported survivors are not pulled into rebuilding", () => {
            var enemy = AbandonWorkshop(); ReachRebuildTime(enemy);
            enemy.EnemyMaster.IsPrisoner = true; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 0, "captured master rebuilt");
            enemy.EnemyMaster.IsPrisoner = false; enemy.EnemyServant.ParentHolder = new object();
            WorkshopRebuildService.Tick(State); Check(Find.WorldObjects.All.Count == 0, "transported servant rebuilt");
            enemy.EnemyServant.ParentHolder = null; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 1, "released free pair did not rebuild");
        });
        Test("eliminated faction cancels pending workshop without resurrection", () => {
            var enemy = AbandonWorkshop(); ReachRebuildTime(enemy);
            enemy.EnemyServant.State.PresenceState = ServantPresenceState.Annihilated;
            WorkshopRebuildService.Tick(State);
            Check(!enemy.WorkshopRebuildPending && Find.WorldObjects.All.Count == 0, "annihilated servant rebuilt");
        });
        Test("failed site selection and partial placement retain rebuild deadline for retry", () => {
            var enemy = AbandonWorkshop(); ReachRebuildTime(enemy); int deadline = enemy.WorkshopRebuildAtTickAbs;
            TileFinder.Fail = true; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 0 && enemy.WorkshopRebuildAtTickAbs == deadline, "selection changed deadline");
            TileFinder.Fail = false; Find.WorldObjects.FailAdd = true; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 0 && enemy.WorkshopRebuildAtTickAbs == deadline, "partial site survived");
            Find.WorldObjects.FailAdd = false; WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 1 && !enemy.WorkshopRebuildPending, "placement retry failed");
        });
        Test("retreat and per-pawn escape state roundtrip", () => {
            var site = EnterWorkshop(); DefeatAt(site); ExitWorkshop(site, site.OwnerMaster);
            site.ExposeData(); Scribe.Loading = true;
            var loaded = new Site_WarWorkshop { Map = site.Map }; loaded.ExposeData();
            Check(loaded.ServantDefeatedHere && loaded.RetreatOrdered && !loaded.BothEscaped && loaded.OwnerMaster == site.OwnerMaster,
                "retreat state lost");
            ExitWorkshop(loaded, State.CurrentWarEntry.EnemyServant);
            Check(loaded.BothEscaped, "saved master escape flag lost");
        });
        Test("rebuild record roundtrips original references and absolute deadline", () => {
            var enemy = AbandonWorkshop(); enemy.ExposeData(); Scribe.Loading = true;
            var loaded = new EnemyWarParticipant(); loaded.ExposeData();
            Check(loaded.WorkshopRebuildAtTickAbs == enemy.WorkshopRebuildAtTickAbs
                && loaded.LostWorkshopTile == enemy.LostWorkshopTile && loaded.EnemyMaster == enemy.EnemyMaster
                && loaded.EnemyServant == enemy.EnemyServant && loaded.EnemyRestStartTickAbs == enemy.EnemyRestStartTickAbs,
                "rebuild state lost");
        });
        Test("seven factions keep independent workshop defeat and rebuilding state", () => {
            SevenClasses(); var enemy = AbandonWorkshop();
            Check(Find.WorldObjects.All.Count == 5, "other workshops removed");
            foreach (var other in State.CurrentWarEntry.Enemies)
                if (other != enemy) Check(!other.WorkshopRebuildPending && other.EnemyRestStartTickAbs < 0, "other faction affected");
            ReachRebuildTime(enemy); WorkshopRebuildService.Tick(State);
            Check(Find.WorldObjects.All.Count == 6, "six enemy workshops not restored");
        });
    }
}
