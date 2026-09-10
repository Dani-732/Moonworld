using System;
using MoonWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

internal static partial class SummoningTests
{
    private static EnemyChallengeSession Challenge()
    {
        PrepareEnemy();
        Check(EnemyChallengeService.TryStart(map, out string reason), reason);
        return State.enemyChallenge;
    }

    private static Map ChallengeMap(EnemyChallengeSession offer, Pawn visitor = null)
    {
        var result = new Map { IsPlayerHome = false, Parent = offer.site };
        offer.site.Map = result;
        offer.site.PostMapGenerate(); // The caravan is not spawned until AFTER this hook.
        Check(!offer.onMap && !offer.servant.Spawned, "premature attendance");
        if (visitor != null) Enter(visitor, result);
        return result;
    }

    private static void Enter(Pawn pawn, Map target)
    {
        if (pawn.Spawned) pawn.DeSpawn();
        Find.WorldPawns.RemovePawn(pawn);
        GenSpawn.Spawn(pawn, cell, target, WipeMode.Vanish);
    }

    private static void EnemyChallengeScenarios()
    {
        Test("challenge reserves original without map and blocks duplicate and direct raid", () => {
            var offer = Challenge(); int count = PawnGenerator.Created.Count;
            Check(!offer.site.HasMap && !offer.servant.Spawned && !offer.master.Spawned
                && offer.expiresAtTickAbs == GenTicks.TicksAbs + 60000, "offer state");
            Check(State.reports.Count == 1 && State.reports[0].kind == WarReportKind.Challenge
                && State.reports[0].IsActive && State.reports[0].actorA == offer.servant
                && State.reports[0].site == offer.site, "challenge report missing");
            Check(TileFinder.LastMin == 1 && TileFinder.LastMax == 3, "challenge distance");
            Check(!EnemyChallengeService.TryStart(map, out _) && !Deploy()
                && EnemyRestUtility.ReadinessRejection(offer.servant) != null
                && PawnGenerator.Created.Count == count, "reservation bypass");
        });
        Test("reserved servant excluded from mutual battle and original workshop", () => {
            SevenClasses(); PrepareEnemy(); Check(EnemyChallengeService.TryStart(map, out _), "offer");
            var offer = State.enemyChallenge;
            Check(EnemyBattleService.TryStart(State, false) && !State.enemyBattle.Contains(offer.servant), "AI stole challenger");
            var site = WorkshopRebuildService.FindWorkshop(State.CurrentWarEntry.FindEnemy(offer.servant));
            site.Map = new Map { Parent = site }; site.PostMapGenerate();
            Check(offer.master.Spawned && !offer.servant.Spawned, "workshop stole challenger");
        });
        Test("ordinary colonist arrival and map unload preserve absolute waiting deadline", () => {
            var offer = Challenge(); int deadline = offer.expiresAtTickAbs;
            var visitor = new Pawn(); var target = ChallengeMap(offer, visitor);
            EnemyChallengeService.Tick(State);
            Check(!offer.onMap && !offer.servant.Spawned, "ordinary colonist counted");
            visitor.DeSpawn(); offer.site.Notify_MyMapAboutToBeRemoved(); offer.site.Map = null;
            Check(State.enemyChallenge == offer && offer.expiresAtTickAbs == deadline && !offer.site.Destroyed,
                "ordinary visit cancelled or extended offer");
        });
        Test("post-generation participant arrival is picked up on subsequent tick exactly once", () => {
            var offer = Challenge(); var target = ChallengeMap(offer);
            EnemyChallengeService.Tick(State); Check(!offer.onMap, "empty map counted");
            offer.servant.needs.Prana.CurLevel = 91;
            Enter(State.CurrentWarEntry.PlayerServant, target);
            EnemyChallengeService.Tick(State); EnemyChallengeService.Tick(State);
            Check(offer.onMap && offer.servant.Map == target && offer.servant.Position.Id != 0 && !offer.master.Spawned
                && target.mapPawns.AllPawnsSpawned.Count == 2 && offer.servant.needs.Prana.CurLevel == 91,
                "arrival lost original, requested position or resources");
        });
        Test("designated master can attend without servant", () => {
            var offer = Challenge(); ChallengeMap(offer, master); EnemyChallengeService.Tick(State);
            Check(offer.onMap, "master attendance rejected");
        });
        Test("waiting expires exactly at absolute deadline without direct raid", () => {
            var offer = Challenge(); int count = PawnGenerator.Created.Count;
            Find.TickManager.TicksGame += 59999; EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == offer, "expired early");
            Find.TickManager.TicksGame++; EnemyChallengeService.Tick(State); EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == null && offer.site.Destroyed && !offer.servant.Spawned
                && Find.WorldPawns.Contains(offer.servant) && PawnGenerator.Created.Count == count
                && State.CurrentWarEntry.FindEnemy(offer.servant).EnemyRestStartTickAbs == GenTicks.TicksAbs,
                "timeout raided, duplicated or omitted rest");
            Check(State.reports.Count == 1 && State.reports[0].state == WarReportState.Expired
                && State.reports[0].resultKey == "约战期限结束", "challenge expiry report missing");
        });
        Test("expiry with ordinary player on map preserves map until native unload", () => {
            var offer = Challenge(); var target = ChallengeMap(offer, new Pawn());
            Find.TickManager.TicksGame += 60000; EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == null && !offer.site.Destroyed && offer.site.Map == target
                && !offer.site.ShouldRemoveMapNow(out _), "occupied map destroyed");
            target.mapPawns.AllPawnsSpawned[0].DeSpawn();
            Check(offer.site.ShouldRemoveMapNow(out _), "empty map never removable");
            offer.site.Notify_MyMapAboutToBeRemoved(); offer.site.Map = null;
            WarEncounterSiteUtility.Cleanup(offer.site); Check(offer.site.Destroyed, "orphan site leaked");
        });
        Test("save fields preserve original references deadline and map phase", () => {
            var offer = Challenge(); ChallengeMap(offer, master); EnemyChallengeService.Tick(State);
            Scribe.Data.Clear(); Scribe.Loading = false; offer.ExposeData();
            Find.TickManager.TicksGame += 500;
            Scribe.Loading = true; var loaded = new EnemyChallengeSession(); loaded.ExposeData(); Scribe.Loading = false;
            State.enemyChallenge = loaded; EnemyChallengeService.Tick(State);
            Check(loaded.servant == offer.servant && loaded.master == offer.master && loaded.site == offer.site
                && loaded.faction == offer.faction && loaded.onMap && loaded.expiresAtTickAbs == offer.expiresAtTickAbs,
                "save changed identity, deadline or map phase");
        });
        Test("waiting load does not renew expired invitation", () => {
            var offer = Challenge(); Scribe.Data.Clear(); offer.ExposeData();
            Find.TickManager.TicksGame += 60000; Scribe.Loading = true;
            var loaded = new EnemyChallengeSession(); loaded.ExposeData(); Scribe.Loading = false;
            State.enemyChallenge = loaded; EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == null && loaded.expiresAtTickAbs == offer.expiresAtTickAbs, "load renewed offer");
        });
        Test("attended combat ignores waiting deadline and withdrawal ends only this encounter", () => {
            var offer = Challenge(); var player = State.CurrentWarEntry.PlayerServant;
            var target = ChallengeMap(offer, player); EnemyChallengeService.Tick(State);
            Find.TickManager.TicksGame += 60000; EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == offer && offer.onMap, "active fight timed out");
            player.DeSpawn(); EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == null && !offer.servant.Spawned && !offer.site.Destroyed, "withdrawal continued fight");
            offer.site.Notify_MyMapAboutToBeRemoved(); offer.site.Map = null; WarEncounterSiteUtility.Cleanup(offer.site);
            Find.TickManager.TicksGame += 180000;
            Check(Deploy() && offer.servant.Map == map, "independent later raid cancelled");
        });
        Test("departing participant ends fight even when ordinary colonist remains", () => {
            var offer = Challenge(); var target = ChallengeMap(offer, master);
            Enter(new Pawn(), target); EnemyChallengeService.Tick(State);
            master.DeSpawn(); EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == null && !offer.servant.Spawned && !offer.site.Destroyed, "fight bound to bystander");
        });
        Test("failed deployment retains original reservation and deadline then retries", () => {
            var offer = Challenge(); var target = ChallengeMap(offer, master); int deadline = offer.expiresAtTickAbs;
            LordMaker.Fail = true; EnemyChallengeService.Tick(State);
            Check(State.enemyChallenge == offer && !offer.onMap && !offer.servant.Spawned
                && Find.WorldPawns.Contains(offer.servant) && target.mapPawns.AllPawnsSpawned.Count == 1
                && offer.expiresAtTickAbs == deadline, "partial deployment leaked");
            LordMaker.Fail = false; EnemyChallengeService.Tick(State); Check(offer.onMap, "retry failed");
        });
        Test("spawn callback cannot recursively deploy or tick challenger", () => {
            var offer = Challenge(); ChallengeMap(offer, master); bool nested = true;
            GenSpawn.Callback = () => { EnemyChallengeService.Tick(State); nested = Deploy(); };
            EnemyChallengeService.Tick(State); GenSpawn.Callback = null;
            Check(offer.onMap && !nested && offer.site.Map.mapPawns.AllPawnsSpawned.Count == 2, "reentry duplicated deployment");
        });
        Test("world creation callback cannot duplicate offer or steal reserved pawn", () => {
            PrepareEnemy(); bool nested = true, raid = true;
            Find.WorldObjects.Callback = () => { nested = EnemyChallengeService.TryStart(map, out _); raid = Deploy(); };
            Check(EnemyChallengeService.TryStart(map, out _) && !nested && !raid
                && Find.WorldObjects.All.Count == 2, "creation reentry stole reservation");
        });
        Test("creation ownership change cancels without restoring old contract", () => {
            PrepareEnemy(); var pawn = State.CurrentWarEntry.EnemyServant;
            Find.WorldObjects.Callback = () => { pawn.Faction = Faction.OfPlayer; pawn.State.Master = master; };
            Check(!EnemyChallengeService.TryStart(map, out _) && State.enemyChallenge == null
                && pawn.Faction == Faction.OfPlayer && pawn.State.Master == master
                && State.CurrentWarEntry.EnemyRestStartTickAbs == -1 && Find.WorldObjects.All.Count == 1, "creation restored ownership");
        });
        Test("natural challenge location failure never falls back to raid", () => {
            PrepareEnemy(); TileFinder.Fail = true; Rand.Pass = true;
            Check(!new IncidentWorker_WarEncounter().TryExecute(new IncidentParms { target = map })
                && State.enemyChallenge == null && !State.CurrentWarEntry.EnemyServant.Spawned, "failed offer became raid");
        });
        Test("natural direct branch retains independent direct worker", () => {
            PrepareEnemy(); Rand.Pass = false;
            Check(new IncidentWorker_WarEncounter().TryExecute(new IncidentParms { target = map })
                && State.enemyChallenge == null && State.CurrentWarEntry.EnemyServant.Spawned, "direct branch disabled");
            Check(State.reports.Count == 1 && State.reports[0].kind == WarReportKind.DirectRaid
                && State.reports[0].IsActive && State.reports[0].actorA == State.CurrentWarEntry.EnemyServant,
                "direct raid report missing");
        });
        Test("direct worker can deploy other servant while challenge waits", () => {
            SevenClasses(); PrepareEnemy(); Check(EnemyChallengeService.TryStart(map, out _), "offer");
            var offer = State.enemyChallenge;
            Check(Deploy() && !offer.servant.Spawned && State.enemyChallenge == offer, "independent raid stole or cancelled offer");
        });
        foreach (string change in new[] { "player", "new-master", "prisoner", "slave", "container", "death", "annihilated", "unbound", "war-end", "defeat" })
        {
            Test("challenge cancellation protects current state: " + change, () => {
                var offer = Challenge(); var target = ChallengeMap(offer, master); EnemyChallengeService.Tick(State);
                var pawn = offer.servant; int deadline = GenTicks.TicksAbs + 30000;
                if (change == "player") { pawn.Faction = Faction.OfPlayer; pawn.State.Master = master; }
                if (change == "new-master") pawn.State.Master = new Pawn { Faction = pawn.Faction };
                if (change == "prisoner") pawn.IsPrisoner = true;
                if (change == "slave") pawn.IsSlave = true;
                if (change == "container") { pawn.DeSpawn(); pawn.ParentHolder = new object(); }
                if (change == "death") pawn.Dead = true;
                if (change == "annihilated") pawn.State.PresenceState = ServantPresenceState.Annihilated;
                if (change == "unbound") { pawn.State.Master = null; pawn.State.UnboundUntilTickAbs = deadline; }
                if (change == "war-end") State.TrySetWarOutcome(WarOutcome.PlayerVictory);
                if (change == "defeat") pawn.State.PresenceState = ServantPresenceState.DefeatedSpirit;
                EnemyChallengeService.Tick(State);
                Check(State.enemyChallenge == null && !offer.site.Destroyed, "invalid offer retained or occupied site destroyed");
                if (change == "player" || change == "new-master" || change == "prisoner" || change == "slave")
                    Check(pawn.Spawned && pawn.Map == target && State.CurrentWarEntry.FindEnemy(pawn).EnemyRestStartTickAbs == -1,
                        "new owner/captive abducted or rest written");
                if (change == "container") Check(pawn.ParentHolder != null && !Find.WorldPawns.Contains(pawn)
                    && State.CurrentWarEntry.FindEnemy(pawn).EnemyRestStartTickAbs == -1, "container stolen or rest written");
                if (change == "death" || change == "annihilated") Check(!Find.WorldPawns.Contains(pawn), "dead servant retained");
                if (change == "unbound") Check(pawn.State.Master == null && pawn.State.UnboundUntilTickAbs == deadline, "unbound reset");
            });
        }
        foreach (string change in new[] { "player", "new-master", "prisoner", "container", "war-end" })
        {
            Test("deployment callback change cannot be undone by rollback: " + change, () => {
                var offer = Challenge(); var target = ChallengeMap(offer, master); var pawn = offer.servant;
                GenSpawn.Callback = () => {
                    if (change == "player") { pawn.Faction = Faction.OfPlayer; pawn.State.Master = master; }
                    if (change == "new-master") pawn.State.Master = new Pawn { Faction = pawn.Faction };
                    if (change == "prisoner") pawn.IsPrisoner = true;
                    if (change == "container") { pawn.DeSpawn(); pawn.ParentHolder = new object(); }
                    if (change == "war-end") State.TrySetWarOutcome(WarOutcome.PlayerVictory);
                };
                EnemyChallengeService.Tick(State); GenSpawn.Callback = null; EnemyChallengeService.Tick(State);
                Check(State.enemyChallenge == null && !offer.onMap, "invalid deployment committed");
                if (change == "player" || change == "new-master" || change == "prisoner")
                    Check(pawn.Spawned && pawn.Map == target && State.CurrentWarEntry.FindEnemy(pawn).EnemyRestStartTickAbs == -1,
                        "callback owner/captive abducted");
                if (change == "container") Check(pawn.ParentHolder != null && !Find.WorldPawns.Contains(pawn), "container extracted");
            });
        }
    }
}
