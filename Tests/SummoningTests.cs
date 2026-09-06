using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;
using RimWorld.Planet;

// Production entry, war state and summoning service; Unity generation and Scribe use host doubles.
internal static partial class SummoningTests
{
    private static Pawn master;
    private static Map map;
    private static IntVec3 cell;
    private static int passed;
    private static GameComponent_MoonWorld State => Current.Game.State;
    private static void Check(bool result, string reason) { if (!result) { Console.WriteLine("FAIL " + reason); throw new Exception(reason); } }
    private static void Setup()
    {
        Current.Game = new Game();
        map = new Map(); master = new Pawn { Map = map }; cell = new IntVec3 { Valid = true };
        Find.TickManager.TicksGame = 1234;
        Find.WorldPawns.Pawns.Clear();
        PawnGenerator.Created.Clear(); Find.FactionManager = new FactionManager();
        CellFinder.Fail = Verse.AI.Group.LordMaker.Fail = Pawn.EdgeBlocked = false;
        Find.WorldPawns.FailPass = false; Find.WorldObjects = new WorldObjectsHolder(); Find.QuestManager = new RimWorld.QuestManager(); TileFinder.Fail = false; HolyGrailWarContentBridge.Fail = false;
        MW_DefOf.MW_HolyGrailWarSettings.enemyRestDurationTicks = 180000;
        MW_DefOf.MW_HolyGrailWarSettings.enemyRaidPranaFraction = .8f;
        PawnGenerator.Last = null; PawnGenerator.Fail = 0; PawnGenerator.Callback = null;
        PawnGenerator.FailAt = 0; PawnGenerator.FailAfterValidation = false;
        GenSpawn.Fail = ServantLifecycleService.Fail = false;
        GenSpawn.Callback = null; ServantLifecycleService.Callback = null;
        HolyGrailWarContentBridge.Calls = HolyGrailWarContentBridge.FailAt = 0;
        DefDatabase<ServantIdentityDef>.AllDefsListForReading = new List<ServantIdentityDef> {
            new ServantIdentityDef { warClass = HolyGrailWarClass.Saber },
            new ServantIdentityDef { warClass = HolyGrailWarClass.Archer } };
        SetPools(); GenCollection.Draws.Clear(); GenCollection.Sizes.Clear();
        Scribe.Loading = false; Scribe.Data.Clear();
    }
    private static void SetPools()
    {
        var definitions = new[] { MW_DefOf.MW_WarOpposition_Saber, MW_DefOf.MW_WarOpposition_Archer,
            MW_DefOf.MW_WarOpposition_Lancer, MW_DefOf.MW_WarOpposition_Assassin,
            MW_DefOf.MW_WarOpposition_Caster, MW_DefOf.MW_WarOpposition_Rider, MW_DefOf.MW_WarOpposition_Berserker };
        DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading = new List<HolyGrailWarClassDef>();
        foreach (var identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            if (HolyGrailWarClassDef.Resolve(identity.warClass) == null)
                DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading.Add(new HolyGrailWarClassDef {
                    defName = identity.warClass.ToString(), label = identity.warClass.ToString(), legacyClass = identity.warClass,
                    oppositionFaction = definitions[(int)identity.warClass - 1] });
        DefDatabase<ServantSummonPoolDef>.AllDefsListForReading = new List<ServantSummonPoolDef>();
        foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            DefDatabase<ServantSummonPoolDef>.AllDefsListForReading.Add(new ServantSummonPoolDef {
                warClass = identity.warClass, servants = new List<ServantIdentityDef> { identity } });
    }
    private static void SevenClasses()
    {
        DefDatabase<ServantIdentityDef>.AllDefsListForReading.Clear();
        for (int i = 1; i <= 7; i++)
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Add(new ServantIdentityDef { warClass = (HolyGrailWarClass)i });
        SetPools();
    }
    private static void Test(string name, Action body) { Setup(); body(); passed++; Console.WriteLine("PASS " + name); }
    private static void Accept()
    {
        string reason; Check(HolyGrailWarEntryService.TryAccept(master, out reason), reason);
        Check(State.warStartTick == -1, "accept started war");
    }
    private static bool Summon(Pawn owner = null)
    {
        Pawn servant; string reason;
        return ServantSummoningService.Instance.TrySummon(owner ?? master, map, cell, out servant, out reason);
    }
    private static void RejectUnspent()
    {
        Check(!Summon(), "unexpected summon");
        Check(!State.CurrentWarEntry.RegularSummonUsed && State.warStartTick == -1, "failed summon spent qualification or started war");
        Check(Find.WorldPawns.Pawns.Count == 0, "failed summon retained world pawn");
        foreach (Pawn p in PawnGenerator.Created)
            Check(p.Destroyed && p.State.Master == null, "failed summon retained pawn or contract");
        Check(Find.WorldObjects.All.Count == 0 && !State.CurrentWarEntry.HasEnemyParticipants,
            "failed summon retained site or participants");
    }
    private static void PrepareEnemy()
    {
        Accept(); Check(Summon(), "player summon failed");
    }
    private static bool Deploy()
    {
        string reason; return EnemyWarPartyService.TryDeploy(map, cell, out reason);
    }
    private static void FailedDeployment()
    {
        Check(!Deploy(), "enemy failure accepted");
        Check(!State.CurrentWarEntry.EnemyDeployed && State.warStartTick == 1234
            && State.CurrentWarEntry.RegularSummonUsed, "enemy failure changed player settlement");
        Check(State.CurrentWarEntry.EnemyPrepared && !State.CurrentWarEntry.EnemyServant.Spawned
            && Find.WorldPawns.Contains(State.CurrentWarEntry.EnemyServant)
            && State.CurrentWarEntry.EnemyServant.State.Master == State.CurrentWarEntry.EnemyMaster,
            "failed deployment lost prepared pair");
    }
    private static Pawn RestEnemy(bool ready = true)
    {
        PrepareEnemy(); Check(Deploy(), "initial deployment failed");
        Pawn enemy = State.CurrentWarEntry.EnemyServant;
        enemy.DeSpawn(); enemy.State.PresenceState = ServantPresenceState.DefeatedSpirit;
        // Pawn.ExitMap first hands the pawn to WorldPawns; the postfix only pins that entry.
        Find.WorldPawns.PassToWorld(enemy, RimWorld.Planet.PawnDiscardDecideMode.Decide);
        EnemyWarPartyService.RetainDepartedPawn(enemy);
        if (ready) Find.TickManager.TicksGame += 180000;
        return enemy;
    }
    private static void CheckReturned(Pawn enemy, int since)
    {
        Check(!enemy.Spawned && Find.WorldPawns.Contains(enemy) && enemy.Lord == null, "failed raid leaked map pawn or lord");
        Check(!enemy.Destroyed && State.CurrentWarEntry.EnemyRestStartTickAbs == since
            && enemy.State.PresenceState == ServantPresenceState.DefeatedSpirit,
            "failed raid reset rest deadline or presence");
        Check(enemy.State.Master == State.CurrentWarEntry.EnemyMaster && PawnGenerator.Created.Count == 3
            && State.warStartTick == 1234 && State.CurrentWarEntry.RegularSummonUsed, "failed raid changed pair or player settlement");
    }
    public static void Main()
    {
        RunWorkshopTests();
        Test("seven faction war only ends after all six opponents lose qualification", () => {
            SevenClasses(); PrepareEnemy(); var enemies = State.CurrentWarEntry.Enemies;
            Check(enemies.Count == 6 && State.warQuest.GetFirstPartOfType<QuestPart_HolyGrailWar>().Factions.Count == 7, "roster incomplete");
            var factions = new HashSet<Faction>();
            for (int i = 0; i < 6; i++)
            {
                factions.Add(enemies[i].EnemyMaster.Faction);
                Check(enemies[i].EnemyMaster.Faction == enemies[i].EnemyServant.Faction, "mismatched faction");
                if (i % 2 == 0) enemies[i].EnemyMaster.Dead = true;
                else enemies[i].EnemyServant.State.PresenceState = ServantPresenceState.Annihilated;
                WarOutcomeService.Tick(State);
                Check(State.CurrentWarOutcome == (i == 5 ? WarOutcome.PlayerVictory : WarOutcome.Ongoing), "premature/absent victory");
            }
            Check(factions.Count == 6 && State.warQuest.EndCalls == 1, "factions shared or repeated outcome");
        });
        Test("six independent raids preserve separate rest deadlines and workshop owners", () => {
            SevenClasses(); PrepareEnemy(); var enemies = State.CurrentWarEntry.Enemies;
            for (int i = 0; i < 6; i++) Check(Deploy(), "eligible faction cannot raid");
            Check(!Deploy() && PawnGenerator.Created.Count == 13, "duplicate raid spawned pawn");
            Pawn first = enemies[0].EnemyServant; first.Spawned = false;
            Find.WorldPawns.PassToWorld(first, PawnDiscardDecideMode.KeepForever); EnemyWarPartyService.RetainDepartedPawn(first);
            int tick = enemies[0].EnemyRestStartTickAbs;
            Find.TickManager.TicksGame += 100;
            Pawn second = enemies[1].EnemyServant; second.Spawned = false;
            Find.WorldPawns.PassToWorld(second, PawnDiscardDecideMode.KeepForever); EnemyWarPartyService.RetainDepartedPawn(second);
            Check(enemies[0].EnemyRestStartTickAbs == tick && enemies[1].EnemyRestStartTickAbs == tick + 100
                && enemies[2].EnemyRestStartTickAbs == -1, "rest clocks shared");
            var site = (Site_WarWorkshop)Find.WorldObjects.All[1]; site.Map = new Map(); site.PostMapGenerate();
            Check(second.Map == site.Map && enemies[1].EnemyMaster.Map == site.Map
                && !first.Spawned && enemies[0].EnemyMaster.Map != site.Map, "wrong workshop defenders");
            site.Notify_MyMapAboutToBeRemoved();
            Check(enemies[1].EnemyRestStartTickAbs == tick + 100 && EnemyContractUtility.IsResting(second), "workshop reset rest");
        });
        for (int pawnIndex = 4; pawnIndex <= 13; pawnIndex++)
        {
            int failureAt = pawnIndex;
            Test("later faction generation failure rolls back roster at pawn " + pawnIndex, () => {
                SevenClasses(); Accept(); PawnGenerator.FailAt = failureAt; PawnGenerator.FailAfterValidation = true;
                RejectUnspent(); Check(Find.FactionManager.AllFactionsListForReading.Count == 0, "orphan factions retained");
            });
        }
        Test("late site callback invalidating an earlier participant aborts whole war", () => {
            SevenClasses(); Accept(); Find.WorldObjects.Callback = () => {
                if (Find.WorldObjects.All.Count == 6) PawnGenerator.Created[2].State.Bind(null);
            };
            RejectUnspent(); Check(Find.FactionManager.AllFactionsListForReading.Count == 0, "late rollback orphan factions");
        });
        Test("extra XML-style class needs no enum value to summon and create opposing factions", () => {
            SevenClasses(); var extra = new HolyGrailWarClassDef { defName = "Example_Avenger", label = "复仇者", oppositionFaction = new FactionDef() };
            var identity = new ServantIdentityDef { classDef = extra };
            DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading.Add(extra);
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Add(identity);
            DefDatabase<ServantSummonPoolDef>.AllDefsListForReading.Add(new ServantSummonPoolDef {
                classDef = extra, servants = new List<ServantIdentityDef> { identity } });
            GenCollection.Draws.Enqueue(7); Accept(); Check(Summon(), "extra player class failed");
            Check(State.CurrentWarEntry.PlayerIdentity == identity && State.CurrentWarEntry.Enemies.Count == 7, "extra class ignored");
            // Save preserves the selected Def rather than an enum ordinal or a new roll.
            State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.PlayerIdentity == identity && State.CurrentWarEntry.Enemies.Count == 7, "extra class lost on load");
        });
        Test("extra enemy class uses its own configured faction and supply", () => {
            var extra = new HolyGrailWarClassDef { defName = "Extra", oppositionFaction = new FactionDef() };
            var identity = new ServantIdentityDef { classDef = extra };
            DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading.Add(extra);
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Add(identity);
            DefDatabase<ServantSummonPoolDef>.AllDefsListForReading.Add(new ServantSummonPoolDef {
                classDef = extra, servants = new List<ServantIdentityDef> { identity } });
            PrepareEnemy(); var participant = State.CurrentWarEntry.Enemies.Find(e => e.Seat == extra);
            Check(participant != null && participant.EnemyServant.Faction.def == extra.oppositionFaction
                && EnemyContractUtility.CanReceiveSupply(participant.EnemyServant), "extra enemy did not join lifecycle");
        });
        Test("old two faction war does not expand when new class defs become available", () => {
            PrepareEnemy(); var pawn = State.CurrentWarEntry.EnemyServant; SevenClasses(); State.LoadedGame();
            Check(State.CurrentWarEntry.Enemies.Count == 1 && State.CurrentWarEntry.EnemyServant == pawn
                && Find.WorldObjects.All.Count == 1, "load expanded existing war");
        });
        Test("invitation does not create enemies or site", () => {
            Accept(); Check(Find.WorldObjects.All.Count == 0 && Find.WorldPawns.Pawns.Count == 0
                && !State.CurrentWarEntry.HasEnemyParticipants, "invitation started enemy war");
        });
        Test("participant save fields retain different original pawns and rest clocks", () => {
            SevenClasses(); PrepareEnemy(); var enemies = State.CurrentWarEntry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Scribe.Loading = false; Scribe.Data.Clear(); Find.TickManager.TicksGame += 100;
                enemies[i].RecordEnemyDeparture(enemies[i].EnemyServant); enemies[i].ExposeData();
                var loaded = new EnemyWarParticipant(); Scribe.Loading = true; loaded.ExposeData();
                Check(loaded.Seat == enemies[i].Seat && loaded.EnemyMaster == enemies[i].EnemyMaster
                    && loaded.EnemyServant == enemies[i].EnemyServant && loaded.EnemyPrepared
                    && loaded.EnemyRestStartTickAbs == enemies[i].EnemyRestStartTickAbs, "participant fields lost");
            }
        });
        Test("unfinished class content refuses war without orphan pawns or factions", () => {
            SevenClasses(); DefDatabase<ServantSummonPoolDef>.AllDefsListForReading[6].servants.Clear();
            Accept(); RejectUnspent(); Check(Find.FactionManager.AllFactionsListForReading.Count == 0, "incomplete content leaked factions");
        });
        Test("duplicate configured faction refuses war before enemies are generated", () => {
            SevenClasses(); var seats = DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading;
            seats[6].oppositionFaction = seats[0].oppositionFaction;
            Accept(); RejectUnspent(); Check(PawnGenerator.Created.Count == 1, "duplicate faction accepted");
        });
        Test("quest status updates each faction and keeps completed history frozen", () => {
            SevenClasses(); PrepareEnemy(); var part = State.warQuest.GetFirstPartOfType<QuestPart_HolyGrailWar>();
            State.CurrentWarEntry.Enemies[0].EnemyServant.Dead = true;
            string description = part.DescriptionPart;
            Check(!part.Factions[1].Qualified && part.Factions[2].Qualified && part.Factions.Count == 7, "live status stale");
            for (int i = 1; i < 6; i++) State.CurrentWarEntry.Enemies[i].EnemyServant.Dead = true;
            WarOutcomeService.Tick(State); master.Dead = true; description = part.DescriptionPart;
            Check(part.Factions[0].Qualified && State.warQuest.Historical, "history changed after completion");
        });
        Test("summon prepares site and full opposing pair before first raid", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry;
            Check(entry.EnemyPrepared && !entry.EnemyDeployed && Find.WorldPawns.Pawns.Count == 2
                && !entry.EnemyServant.Spawned && !entry.EnemyMaster.Spawned
                && entry.EnemyServant.needs.Prana.CurLevel == 100, "incomplete off-map enemy");
            Check(Find.WorldObjects.All.Count == 1 && ((Site_WarWorkshop)Find.WorldObjects.All[0]).OwnerMaster == entry.EnemyMaster,
                "site owner missing");
            Check(EnemyRestUtility.TicksRemaining(entry.EnemyServant) == 0 && Deploy(), "first raid incorrectly cooling down");
            Check(GenSpawn.LastRespawning && PawnGenerator.Created.Count == 3, "raid regenerated content or pawn");
        });
        Test("no world site tile rolls back entire summon", () => { Accept(); TileFinder.Fail = true; RejectUnspent(); });
        Test("partial site registration rolls back entire summon and can retry", () => {
            Accept(); Find.WorldObjects.FailAdd = true; RejectUnspent();
            Find.WorldObjects.FailAdd = false; Check(Summon() && Find.WorldObjects.All.Count == 1, "startup retry failed");
        });
        Test("enemy generation fails before pawn capture", () => { Accept(); PawnGenerator.FailAt = 2; RejectUnspent(); });
        Test("enemy master gear failure rolls back captured pawn", () => { Accept(); PawnGenerator.FailAt = 2; PawnGenerator.FailAfterValidation = true; RejectUnspent(); });
        Test("enemy servant gear failure rolls back all three pawns", () => { Accept(); PawnGenerator.FailAt = 3; PawnGenerator.FailAfterValidation = true; RejectUnspent(); });
        Test("site hook invalidating player qualification rolls back startup", () => {
            Accept(); Find.WorldObjects.Callback = () => master.Dead = true; RejectUnspent();
        });
        Test("site hook invalidating enemy contract rolls back startup", () => {
            Accept(); Find.WorldObjects.Callback = () => PawnGenerator.Last.State.Bind(null); RejectUnspent();
        });
        Test("site hook removing enemy world retention rolls back startup", () => {
            Accept(); Find.WorldObjects.Callback = () => Find.WorldPawns.RemovePawn(PawnGenerator.Last); RejectUnspent();
        });
        Test("site creation cannot trigger a reentrant raid or summon", () => {
            Accept(); Find.WorldObjects.Callback = () => Check(!Deploy() && !Summon(), "uncommitted war exposed");
            Check(Summon(), "outer summon failed");
        });
        Test("startup reload keeps original participants site and ready first raid", () => {
            PrepareEnemy(); Pawn enemy = State.CurrentWarEntry.EnemyServant;
            var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
            State.ExposeData(); site.ExposeData(); Scribe.Loading = true; Current.Game = new Game();
            State.ExposeData(); State.LoadedGame(); var loadedSite = new Site_WarWorkshop(); loadedSite.ExposeData();
            Check(State.CurrentWarEntry.EnemyServant == enemy && loadedSite.OwnerMaster == State.CurrentWarEntry.EnemyMaster
                && Find.WorldObjects.All.Count == 1 && PawnGenerator.Created.Count == 3 && Deploy(), "reload recreated or delayed enemy");
        });
        Test("site destruction neither eliminates nor regenerates participants", () => {
            PrepareEnemy(); Find.WorldObjects.All[0].Destroy(); State.LoadedGame(); WarOutcomeService.Tick(State);
            Check(State.CurrentWarOutcome == WarOutcome.Ongoing && Find.WorldObjects.All.Count == 0
                && PawnGenerator.Created.Count == 3 && Deploy(), "destroyed site changed qualification");
        });
        Test("legacy war with no enemy gets startup once without resetting tick", () => {
            Accept(); State.CommitRegularSummon(); var identities = DefDatabase<ServantIdentityDef>.AllDefsListForReading;
            State.CurrentWarEntry.SetParticipants(identities[0], identities[1]); State.LoadedGame(); State.LoadedGame();
            Check(State.CurrentWarEntry.EnemyPrepared && PawnGenerator.Created.Count == 2
                && Find.WorldObjects.All.Count == 1 && State.warStartTick == 1234 && Deploy(), "legacy initialization failed");
        });
        Test("legacy resting pair gains site without healing replacing or restarting rest", () => {
            Pawn enemy = RestEnemy(false); enemy.needs.Prana.CurLevel = 17;
            int since = State.CurrentWarEntry.EnemyRestStartTickAbs;
            State.ExposeData(); Scribe.Data.Remove("enemies");
            Scribe.Data["enemyIdentity"] = State.CurrentWarEntry.EnemyIdentity;
            Scribe.Data["enemyMaster"] = State.CurrentWarEntry.EnemyMaster;
            Scribe.Data["enemyServant"] = enemy; Scribe.Data["enemyDeployed"] = true;
            Scribe.Data["enemyRestStartTickAbs"] = since;
            Scribe.Data.Remove("enemyPrepared"); Find.WorldObjects.All.Clear();
            Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame(); State.LoadedGame();
            Check(State.CurrentWarEntry.EnemyServant == enemy && enemy.needs.Prana.CurLevel == 17
                && State.CurrentWarEntry.EnemyRestStartTickAbs == since && Find.WorldObjects.All.Count == 1
                && PawnGenerator.Created.Count == 3 && !Deploy(), "legacy pair reset");
        });
        Test("eliminated legacy enemy is not resurrected on load", () => {
            PrepareEnemy(); Check(Deploy(), "raid failed"); State.CurrentWarEntry.EnemyMaster.Dead = true;
            State.ExposeData(); Scribe.Data.Remove("enemyPrepared"); Find.WorldObjects.All.Clear();
            Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(Find.WorldObjects.All.Count == 0 && PawnGenerator.Created.Count == 3 && !Deploy(), "legacy enemy resurrected");
        });
        Test("enemy death before any raid still ends war", () => {
            PrepareEnemy(); State.CurrentWarEntry.EnemyServant.Dead = true; WarOutcomeService.Tick(State);
            Check(State.CurrentWarOutcome == WarOutcome.PlayerVictory && !Deploy(), "unraided death ignored");
        });
        Test("war end does not stop ongoing prana cycles", () => {
            PrepareEnemy(); State.TrySetWarOutcome(WarOutcome.PlayerVictory); PranaCycleService.Calls = 0;
            Find.TickManager.TicksGame = 1500; State.GameComponentTick(); Check(PranaCycleService.Calls == 1, "postwar prana frozen");
        });
        Test("rest deadline blocks manual and incident entry until boundary", () => {
            Pawn enemy = RestEnemy(false);
            var worker = new IncidentWorker_EnemyServantRaid(); var parms = new IncidentParms { target = map };
            Check(!Deploy() && !worker.TryExecute(parms), "cooldown bypassed");
            Find.TickManager.TicksGame += 179999; Check(!Deploy(), "one tick early");
            Find.TickManager.TicksGame++; Check(worker.TryExecute(parms), "deadline did not permit raid");
            Check(enemy == State.CurrentWarEntry.EnemyServant && enemy.State.PresenceState == ServantPresenceState.Materialized,
                "incident replaced pawn or left a spirit raiding");
        });
        Test("redeploy preserves pair mana and original player settlement", () => {
            Pawn enemy = RestEnemy(); enemy.needs.Prana.CurLevel = 80;
            Check(Deploy(), "redeploy failed");
            Check(enemy == State.CurrentWarEntry.EnemyServant && enemy.needs.Prana.CurLevel == 80
                && PawnGenerator.Created.Count == 3 && enemy.State.Master == State.CurrentWarEntry.EnemyMaster
                && !State.CurrentWarEntry.EnemyMaster.Spawned && State.warStartTick == 1234, "replaced or reset participants");
            Check(!Deploy(), "duplicate active raid");
        });
        Test("low mana rejects without spawning", () => { Pawn enemy = RestEnemy(); enemy.needs.Prana.CurLevel = 79.9f; Check(!Deploy(), "low mana accepted"); });
        Test("injury rejects despite full mana", () => { Pawn enemy = RestEnemy(); enemy.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 1 }); Check(!Deploy(), "injured raid"); });
        Test("unsafe health rejects even without ordinary injury", () => { Pawn enemy = RestEnemy(); enemy.health.Unsafe = true; Check(!Deploy(), "incapacitated raid"); });
        Test("captured world servant cannot be pulled out", () => { Pawn enemy = RestEnemy(); enemy.IsPrisoner = true; Check(!Deploy(), "prisoner teleported"); });
        Test("enslaved servant cannot return", () => { Pawn enemy = RestEnemy(); enemy.IsSlave = true; Check(!Deploy(), "slave teleported"); });
        Test("transport holder is not free rest", () => { Pawn enemy = RestEnemy(); enemy.ParentHolder = new object(); Check(!Deploy(), "container robbed"); });
        Test("off-map pawn not retained in world cannot return", () => { Pawn enemy = RestEnemy(); Find.WorldPawns.RemovePawn(enemy); Check(!Deploy(), "missing pawn spawned"); });
        Test("captured enemy master blocks return", () => { RestEnemy(); State.CurrentWarEntry.EnemyMaster.IsPrisoner = true; Check(!Deploy(), "captured master active"); });
        Test("master on another map blocks return", () => { RestEnemy(); State.CurrentWarEntry.EnemyMaster.Spawned = true; Check(!Deploy(), "map master ignored"); });
        Test("player master away blocks ready raid", () => { RestEnemy(); master.Spawned = false; Check(!Deploy(), "absent player raided"); });
        Test("non-home map blocks ready raid", () => { RestEnemy(); map.IsPlayerHome = false; Check(!Deploy(), "non-home raided"); });
        Test("annihilated world servant cannot return", () => { Pawn enemy = RestEnemy(); enemy.State.PresenceState = ServantPresenceState.Annihilated; Check(!Deploy(), "annihilated pawn returned"); });
        Test("occupied placement rejects without modifying world pawn", () => { Pawn enemy = RestEnemy(); cell.Occupied = true; Check(!Deploy() && Find.WorldPawns.Contains(enemy), "occupied placement accepted"); });
        Test("rest deadline survives entry reload without restart", () => {
            Pawn enemy = RestEnemy(false); Find.TickManager.TicksGame += 60000;
            State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.EnemyServant == enemy && EnemyRestUtility.TicksRemaining(enemy) == 120000 && !Deploy(), "reload changed rest deadline");
        });
        Test("vanilla world transfer pins original rest deadline", () => {
            Pawn enemy = RestEnemy(false); int startedAt = State.CurrentWarEntry.EnemyRestStartTickAbs;
            Check(Find.WorldPawns.Contains(enemy) && Find.WorldPawns.ForcefullyKeptPawns.Contains(enemy)
                && startedAt == 3601234, "departure did not retain original world pawn");
            Find.TickManager.TicksGame += 60000;
            Check(EnemyRestUtility.TicksRemaining(enemy) == 120000, "rest deadline did not advance from departure");
        });
        Test("redeploy spawn failure returns same spirit and deadline", () => {
            Pawn enemy = RestEnemy(); int since = State.CurrentWarEntry.EnemyRestStartTickAbs; GenSpawn.Fail = true;
            Check(!Deploy(), "spawn failure accepted"); CheckReturned(enemy, since);
            GenSpawn.Fail = false; Check(Deploy(), "failed raid locked retry");
        });
        Test("redeploy unreachable edge returns original pawn", () => {
            Pawn enemy = RestEnemy(); int since = State.CurrentWarEntry.EnemyRestStartTickAbs; Pawn.EdgeBlocked = true;
            Check(!Deploy(), "blocked exit accepted"); CheckReturned(enemy, since);
        });
        Test("partial lord failure removes registered lord", () => {
            Pawn enemy = RestEnemy(); int since = State.CurrentWarEntry.EnemyRestStartTickAbs; Verse.AI.Group.LordMaker.Fail = true;
            Check(!Deploy(), "lord failure accepted"); CheckReturned(enemy, since);
        });
        Test("materialization failure returns resting spirit", () => {
            Pawn enemy = RestEnemy(); int since = State.CurrentWarEntry.EnemyRestStartTickAbs; ServantLifecycleService.Fail = true;
            Check(!Deploy(), "presence failure accepted"); CheckReturned(enemy, since);
        });
        Test("reentrant redeploy cannot create another lord or pawn", () => {
            RestEnemy(); bool nested = true; GenSpawn.Callback = () => nested = Deploy();
            Check(Deploy() && !nested && PawnGenerator.Created.Count == 3, "nested raid accepted");
        });
        Test("circuit alone cannot summon", () => Check(!Summon(), "free qualification"));
        Test("enemy deployment before war rejected", () => Check(!Deploy(), "premature enemy"));
        Test("enemy deployment uses prepared opposing pair", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed");
            Check(EnemyContractUtility.HasEnemyContract(State.CurrentWarEntry.EnemyServant)
                && State.CurrentWarEntry.EnemyMaster.Faction != Faction.OfPlayer
                && State.CurrentWarEntry.EnemyServant.Identity.warClass == HolyGrailWarClass.Archer, "incorrect pair");
            Check(!State.CurrentWarEntry.EnemyMaster.Spawned && Find.WorldPawns.Contains(State.CurrentWarEntry.EnemyMaster)
                && State.CurrentWarEntry.EnemyServant.Spawned, "master entered raid or was not retained");
            Check(Verse.AI.Group.LordMaker.LastPawns.Length == 1
                && Verse.AI.Group.LordMaker.LastPawns[0] == State.CurrentWarEntry.EnemyServant, "raid contains master");
            Check(!Deploy() && State.warStartTick == 1234, "repeat deployed or changed time");
        });
        Test("raid never calls pawn generation", () => { PrepareEnemy(); PawnGenerator.Fail = 1; Check(Deploy(), "raid tried to generate pawn"); });
        Test("dependency initialization failure rolls back startup", () => { Accept(); HolyGrailWarContentBridge.Fail = true; RejectUnspent(); });
        Test("enemy dependency initialization failure rolls back player and enemy", () => {
            Accept(); HolyGrailWarContentBridge.FailAt = 2; RejectUnspent();
            Check(PawnGenerator.Created.Count == 3, "did not reach enemy content initialization");
        });
        Test("player dependency appearance initialized before spawn", () => {
            Accept(); GenSpawn.Callback = () => Check(HolyGrailWarContentBridge.Calls == 1, "player rendered before initialization");
            Check(Summon() && HolyGrailWarContentBridge.Calls == 2, "player/enemy initialization missing");
        });
        Test("first raid spawn failure retains prepared pair", () => { PrepareEnemy(); GenSpawn.Fail = true; FailedDeployment(); });
        Test("first raid materialization failure retains prepared pair", () => { PrepareEnemy(); ServantLifecycleService.Fail = true; FailedDeployment(); });
        Test("first raid lord failure retains prepared pair", () => { PrepareEnemy(); Verse.AI.Group.LordMaker.Fail = true; FailedDeployment(); });
        Test("world retention failure rolls back startup", () => { Accept(); Find.WorldPawns.FailPass = true; RejectUnspent(); });
        Test("enemy placement failure permits retry", () => {
            PrepareEnemy(); Pawn.EdgeBlocked = true; FailedDeployment(); Pawn.EdgeBlocked = false; Check(Deploy(), "retry failed");
        });
        Test("enemy deployment and identities survive round trip", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); Pawn enemy = State.CurrentWarEntry.EnemyServant;
            State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.EnemyServant == enemy && State.CurrentWarEntry.EnemyIdentity == enemy.Identity
                && !Deploy(), "reloaded opponent replaced");
            Check(!State.CurrentWarEntry.EnemyMaster.Spawned && Find.WorldPawns.Contains(State.CurrentWarEntry.EnemyMaster), "world master lost on reload");
        });
        Test("dead enemy cannot respawn", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); State.CurrentWarEntry.EnemyMaster.Dead = true;
            Check(State.CurrentWarEntry.EnemyEliminated && !Deploy(), "eliminated enemy respawned");
        });
        Test("departed enemy is retained as same pawn", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); Pawn enemy = State.CurrentWarEntry.EnemyServant; enemy.Spawned = false;
            Find.WorldPawns.PassToWorld(enemy, RimWorld.Planet.PawnDiscardDecideMode.Decide);
            EnemyWarPartyService.RetainDepartedPawn(enemy); Check(Find.WorldPawns.Contains(enemy), "lost departed enemy");
        });
        Test("Saber prepares Archer opponent without map deployment", () => {
            Accept(); Check(Summon(), "failed");
            Check(State.CurrentWarEntry.PlayerIdentity.warClass == HolyGrailWarClass.Saber
                && State.CurrentWarEntry.EnemyIdentity.warClass == HolyGrailWarClass.Archer
                && !State.CurrentWarEntry.EnemyDeployed, "incorrect opposing seat");
        });
        Test("Archer reserves Saber opponent", () => {
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Reverse(); SetPools(); Accept(); Check(Summon(), "failed");
            Check(State.CurrentWarEntry.EnemyIdentity.warClass == HolyGrailWarClass.Saber, "incorrect opponent");
        });
        Test("missing opposite class leaves summon unspent", () => {
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.RemoveAt(1); SetPools(); Accept(); RejectUnspent();
        });
        for (int i = 0; i < 7; i++)
        {
            int selected = i;
            Test("each class summons and reserves a different enemy: " + (HolyGrailWarClass)(i + 1), () => {
                SevenClasses(); GenCollection.Draws.Enqueue(selected); GenCollection.Draws.Enqueue(0);
                Accept(); Check(Summon(), "class summon failed");
                Check(State.CurrentWarEntry.PlayerIdentity.warClass == (HolyGrailWarClass)(selected + 1)
                    && State.CurrentWarEntry.EnemyIdentity.warClass != State.CurrentWarEntry.PlayerIdentity.warClass,
                    "wrong player or enemy class");
                Check(GenCollection.Sizes[0] == 7 && GenCollection.Sizes[1] == 1
                    && GenCollection.Sizes.Count == 8 && GenCollection.Sizes[2] == 1, "not class-first draws");
                Check(State.CurrentWarEntry.EnemyServant.State.Master == State.CurrentWarEntry.EnemyMaster
                    && Find.WorldObjects.All.Count == 6 && State.CurrentWarEntry.Enemies.Count == 6, "enemy contract/site missing");
                var expectedDefs = new[] { MW_DefOf.MW_WarOpposition_Saber, MW_DefOf.MW_WarOpposition_Archer,
                    MW_DefOf.MW_WarOpposition_Lancer, MW_DefOf.MW_WarOpposition_Assassin,
                    MW_DefOf.MW_WarOpposition_Caster, MW_DefOf.MW_WarOpposition_Rider, MW_DefOf.MW_WarOpposition_Berserker };
                Pawn enemyPawn = State.CurrentWarEntry.EnemyServant;
                Check(enemyPawn.Faction.def == expectedDefs[(int)State.CurrentWarEntry.EnemyIdentity.warClass - 1]
                    && Find.WorldObjects.All[0].Faction == enemyPawn.Faction
                    && EnemyContractUtility.CanReceiveSupply(enemyPawn), "class faction rejected contract/supply or mismatched site");
                var player = State.CurrentWarEntry.PlayerIdentity; var enemy = State.CurrentWarEntry.EnemyIdentity;
                State.LoadedGame();
                Check(State.CurrentWarEntry.PlayerIdentity == player && State.CurrentWarEntry.EnemyIdentity == enemy
                    && State.warStartTick == 1234 && GenCollection.Sizes.Count == 8, "reload rerolled participants");
                Check(Deploy(), "class faction cannot raid");
            });
        }
        Test("legacy opposition contract remains valid while unrelated factions are rejected", () => {
            var legacy = new Faction { def = MW_DefOf.MW_WarOpposition };
            Pawn owner = new Pawn { Faction = legacy }, pawn = new Pawn { Faction = legacy };
            pawn.State.Bind(owner);
            Check(EnemyContractUtility.HasEnemyContract(pawn), "legacy contract rejected");
            pawn.Faction = new Faction { def = MW_DefOf.MW_WarOpposition_Saber };
            Check(!EnemyContractUtility.HasEnemyContract(pawn), "cross-faction contract accepted");
            Check(!EnemyContractUtility.IsWarPawn(new Pawn { Faction = new Faction { def = new FactionDef() } })
                && !EnemyContractUtility.IsWarPawn(null), "unrelated faction accepted");
        });
        Test("failed summon saved neutral class faction can retry", () => {
            var oldFaction = new Faction { def = MW_DefOf.MW_WarOpposition_Archer, Neutral = true };
            Find.FactionManager.Add(oldFaction); Accept(); Check(Summon(), "neutral leftover blocked retry");
            Check(State.CurrentWarEntry.EnemyMaster.Faction == oldFaction && oldFaction.HostileTo(Faction.OfPlayer), "leftover not repaired");
        });
        Test("class weight is independent of population and duplicate extension tables", () => {
            var pools = DefDatabase<ServantSummonPoolDef>.AllDefsListForReading;
            var secondSaber = new ServantIdentityDef { warClass = HolyGrailWarClass.Saber };
            pools[0].servants.Add(secondSaber); pools[0].servants.Add(secondSaber); pools.Add(pools[0]);
            GenCollection.Draws.Enqueue(0); GenCollection.Draws.Enqueue(1);
            Check(ServantSummonPoolDef.Pick() == secondSaber && GenCollection.Sizes[0] == 2
                && GenCollection.Sizes[1] == 2, "duplicate entries or pool size biased class selection");
            GenCollection.Draws.Enqueue(1); GenCollection.Draws.Enqueue(0);
            Check(ServantSummonPoolDef.Pick().warClass == HolyGrailWarClass.Archer
                && GenCollection.Sizes[2] == 2 && GenCollection.Sizes[3] == 1, "small pool lost equal class slot");
        });
        Test("all six opposing classes remain selectable for every player class", () => {
            SevenClasses();
            foreach (ServantIdentityDef player in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            {
                var opponents = new HashSet<HolyGrailWarClass>();
                for (int i = 0; i < 6; i++)
                {
                    GenCollection.Draws.Enqueue(i); GenCollection.Draws.Enqueue(0);
                    var enemy = HolyGrailWarClassUtility.PickOpponent(player);
                    Check(enemy != null && enemy.warClass != player.warClass, "same class opponent");
                    opponents.Add(enemy.warClass);
                }
                Check(opponents.Count == 6, "opposing class unreachable");
            }
        });
        Test("invalid and empty pools are excluded without spawning", () => {
            var pools = DefDatabase<ServantSummonPoolDef>.AllDefsListForReading;
            pools[0].servants.Clear(); pools[1].servants[0].summonable = false;
            pools.Add(new ServantSummonPoolDef { warClass = HolyGrailWarClass.None,
                servants = new List<ServantIdentityDef> { new ServantIdentityDef() } });
            pools.Add(new ServantSummonPoolDef { warClass = HolyGrailWarClass.Lancer,
                servants = new List<ServantIdentityDef> { null, new ServantIdentityDef { warClass = HolyGrailWarClass.Saber },
                    new ServantIdentityDef { warClass = HolyGrailWarClass.Lancer, servantKind = null } } });
            pools.Add(new ServantSummonPoolDef { warClass = HolyGrailWarClass.Rider, servants = null });
            Accept(); RejectUnspent(); Check(GenCollection.Sizes.Count == 0 && PawnGenerator.Created.Count == 0, "empty pools drawn");
        });
        Test("identity outside configured pools is not summonable", () => {
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Add(new ServantIdentityDef { warClass = HolyGrailWarClass.Lancer });
            Check(ServantSummonPoolDef.Pick().warClass == HolyGrailWarClass.Saber && GenCollection.Sizes[0] == 2,
                "unregistered identity entered a pool");
        });
        Test("circuit and independently granted seals cannot summon", () => {
            master.story.traits.GainTrait(new Trait(MW_DefOf.MW_CommandSpell)); Check(!Summon(), "trait bypass");
        });
        Test("only designated master receives qualification", () => {
            Accept(); Pawn other = new Pawn { Map = map }; string reason;
            Check(!HolyGrailWarEntryService.TryAccept(other, out reason) && !Summon(other), "second master accepted");
            Check(other.Spells.Grants == 0, "second master received spells"); Check(Summon(), "designated master failed");
        });
        Test("accept grants three seals exactly once", () => {
            master.Spells.Charges = 0; Accept(); master.Spells.Charges = 1; string reason;
            Check(!HolyGrailWarEntryService.TryAccept(master, out reason) && master.Spells.Charges == 1
                && master.Spells.Grants == 1, "repeat acceptance replenished seals");
        });
        Test("grant failure leaves event available", () => {
            master.Spells.Fail = true; string reason;
            Check(!HolyGrailWarEntryService.TryAccept(master, out reason) && State.CanAcceptInvitation
                && State.warStartTick == -1, "grant failure claimed event");
        });
        Test("no circuit cannot accept", () => { master.Circuit = false; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "nonmage accepted"); });
        Test("prisoner cannot accept", () => { master.IsPrisoner = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "prisoner accepted"); });
        Test("slave cannot accept", () => { master.IsSlave = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "slave accepted"); });
        Test("quest guest cannot accept", () => { master.Lodger = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "guest accepted"); });
        Test("servant cannot accept", () => { master.Servant = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "servant accepted"); });
        Test("off map candidate cannot accept", () => { master.Spawned = false; string reason; Check(!HolyGrailWarEntryService.TryAccept(master, out reason), "off map accepted"); });
        Test("first successful summon consumes one qualification but no seals", () => {
            Accept(); Check(Summon(), "summon failed");
            Check(State.CurrentWarEntry.RegularSummonUsed && State.warStartTick == 1234 && master.Spells.Charges == 3, "wrong settlement");
            Check(PawnGenerator.Created[0].State.Master == master && PawnGenerator.Created[0].Map == map, "wrong contract");
            Check(PawnGenerator.Request.ForceNew && !PawnGenerator.Request.Relations, "generation reused a world pawn or created relations");
        });
        Test("first summon creates one accepted Holy Grail War quest with both factions", () => {
            PrepareEnemy();
            Check(Find.QuestManager.QuestsListForReading.Count == 1, "quest missing or duplicated");
            RimWorld.Quest quest = Find.QuestManager.QuestsListForReading[0];
            var part = quest.GetFirstPartOfType<QuestPart_HolyGrailWar>();
            Check(quest.name == "圣杯战争" && quest.root == MW_DefOf.MW_HolyGrailWarQuest && quest.id > 0
                && quest.Accepted && part != null && part.WarStartTick == 1234, "quest metadata incorrect");
            Check(part.Factions.Count == 2 && part.Factions[0].Master == master
                && part.Factions[0].Servants.Count == 1 && part.Factions[1].Master == State.CurrentWarEntry.EnemyMaster
                && part.Factions[1].Servants.Count == 1 && part.Factions[1].Sites.Count == 1, "quest faction snapshot incomplete");
            State.LoadedGame(); State.LoadedGame();
            Check(Find.QuestManager.QuestsListForReading.Count == 1 && State.warQuest == quest, "quest was recreated on reload");
        });
        Test("second summon cannot overwrite first tick", () => { Accept(); Check(Summon(), "first failed"); Find.TickManager.TicksGame = 9999; Check(!Summon() && State.warStartTick == 1234, "repeat succeeded"); });
        Test("existing allied servant is not a global population cap", () => {
            Pawn existing = new Pawn { Servant = true, Spawned = false }; existing.State.Bind(master);
            Find.WorldPawns.Pawns.Add(existing); Accept(); Check(Summon(), "existing servant blocked event qualification");
            Check(existing.State.Master == master && !existing.Destroyed, "existing contract changed");
        });
        Test("annihilation does not restore qualification", () => { Accept(); Check(Summon(), "first failed"); PawnGenerator.Created[0].Destroy(); Check(!Summon(), "replacement granted"); });
        Test("master loss does not reopen event", () => { Accept(); master.Dead = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(new Pawn { Map = map }, out reason), "replacement master granted"); });
        Test("dead designated master cannot summon", () => { Accept(); master.Dead = true; RejectUnspent(); });
        Test("captured designated master cannot summon", () => { Accept(); master.IsPrisoner = true; RejectUnspent(); });
        Test("exhausted seals cannot summon", () => { Accept(); master.Spells.Charges = 0; RejectUnspent(); });
        Test("removed seal trait cannot summon", () => { Accept(); master.story.traits.allTraits.Clear(); RejectUnspent(); });
        Test("another map rejected", () => { Accept(); map = new Map(); RejectUnspent(); });
        Test("blocked cell rejected", () => { Accept(); cell = new IntVec3(); RejectUnspent(); });
        Test("fogged cell rejected", () => { Accept(); cell.Fog = true; RejectUnspent(); });
        Test("empty candidate pool preserves qualification", () => { Accept(); DefDatabase<ServantSummonPoolDef>.AllDefsListForReading.Clear(); RejectUnspent(); });
        Test("generation failure before returning pawn preserves qualification", () => { Accept(); PawnGenerator.Fail = 1; RejectUnspent(); });
        Test("generation failure after validation cleans partial pawn", () => { Accept(); PawnGenerator.Fail = 2; RejectUnspent(); });
        Test("spawn failure cleans pawn", () => { Accept(); GenSpawn.Fail = true; RejectUnspent(); });
        Test("partial bind failure clears contract and world pawn", () => { Accept(); ServantLifecycleService.Fail = true; RejectUnspent(); });
        Test("eligibility rechecked after external spawn hooks", () => { Accept(); GenSpawn.Callback = () => master.Dead = true; RejectUnspent(); });
        Test("master map rechecked after external spawn hooks", () => { Accept(); GenSpawn.Callback = () => master.Map = new Map(); RejectUnspent(); });
        Test("contract rechecked after external bind hooks", () => { Accept(); ServantLifecycleService.Callback = pawn => pawn.State.Bind(new Pawn()); RejectUnspent(); });
        Test("reentrant summon cannot create second pawn", () => { Accept(); PawnGenerator.Callback = () => Check(!Summon(), "reentrant success"); Check(Summon(), "outer failed"); });
        Test("failure releases runtime lock for retry", () => { Accept(); GenSpawn.Fail = true; RejectUnspent(); GenSpawn.Fail = false; Check(Summon(), "lock retained"); });
        Test("unstarted legacy save must accept invitation", () => { State.LoadedGame(); Check(State.CanAcceptInvitation && !Summon(), "legacy autoqualified"); });
        Test("started legacy save cannot claim extra summon", () => {
            State.warStartTick = 37; State.LoadedGame();
            Check(State.CurrentWarEntry.RegularSummonUsed && !State.CanAcceptInvitation && !Summon() && State.warStartTick == 37, "legacy reopened");
        });
        Test("accepted entry round trip retains designated master", () => {
            Accept(); State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.DesignatedMaster == master && !State.CurrentWarEntry.RegularSummonUsed
                && State.warStartTick == -1 && !State.CanAcceptInvitation, "accept save state lost");
        });
        Test("used entry round trip remains spent", () => {
            Accept(); Check(Summon(), "first failed"); State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.RegularSummonUsed && State.warStartTick == 1234 && !Summon(), "spent save state lost");
        });
        Test("enemy departure does not end war", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); Pawn enemy = State.CurrentWarEntry.EnemyServant;
            enemy.Spawned = false; Find.WorldPawns.PassToWorld(enemy, RimWorld.Planet.PawnDiscardDecideMode.Decide);
            EnemyWarPartyService.RetainDepartedPawn(enemy); WarOutcomeService.Tick(State);
            Check(State.CurrentWarOutcome == WarOutcome.Ongoing, "departed enemy ended war");
        });
        Test("enemy elimination ends war exactly once", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed");
            State.CurrentWarEntry.EnemyServant.Destroyed = true; WarOutcomeService.Tick(State);
            Check(State.CurrentWarOutcome == WarOutcome.PlayerVictory, "enemy elimination did not win war");
            WarOutcomeService.Tick(State); Check(State.CurrentWarOutcome == WarOutcome.PlayerVictory, "war outcome changed on second tick");
        });
        Test("player master death ends war as defeat", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); master.Dead = true; WarOutcomeService.Tick(State);
            Check(State.CurrentWarOutcome == WarOutcome.PlayerDefeat, "master death did not lose war");
        });
        Test("victory completes quest once with notification and preserves participants", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; var quest = State.warQuest;
            var servant = entry.EnemyServant; var workshop = Find.WorldObjects.All[0];
            entry.EnemyMaster.Dead = true; WarOutcomeService.Tick(State); WarOutcomeService.Tick(State);
            Check(quest.State == QuestState.EndedSuccess && quest.EndCalls == 1 && quest.Letters == 1, "quest victory not idempotent");
            Check(!workshop.Destroyed && entry.EnemyServant == servant && PawnGenerator.Created.Count == 3
                && State.warStartTick == 1234 && entry.RegularSummonUsed && !Deploy(), "quest cleanup changed war facts");
        });
        Test("defeat completes quest once and cannot be overwritten by later enemy death", () => {
            PrepareEnemy(); master.Dead = true; WarOutcomeService.Tick(State);
            State.CurrentWarEntry.EnemyMaster.Dead = true; WarOutcomeService.Tick(State);
            Check(State.warQuest.State == QuestState.EndedFailed && State.warQuest.EndCalls == 1
                && State.warQuest.Letters == 1, "quest defeat overwritten");
        });
        Test("rest and site destruction leave quest ongoing", () => {
            RestEnemy(false); Find.WorldObjects.All[0].Destroy(); WarOutcomeService.Tick(State);
            Check(State.warQuest.State == QuestState.Ongoing && State.warQuest.EndCalls == 0, "retreat ended quest");
        });
        Test("legacy ended war reconciles quest silently for both outcomes", () => {
            foreach (var outcome in new[] { WarOutcome.PlayerVictory, WarOutcome.PlayerDefeat }) {
                Setup(); PrepareEnemy(); var quest = State.warQuest; int id = quest.id;
                quest.root = QuestScriptDefOf.WandererJoins;
                State.ExposeData(); Scribe.Data["warOutcome"] = outcome;
                Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame(); State.LoadedGame();
                Check(quest.State == (outcome == WarOutcome.PlayerVictory ? QuestState.EndedSuccess : QuestState.EndedFailed)
                    && quest.EndCalls == 1 && quest.Letters == 0 && quest.id == id
                    && quest.root == MW_DefOf.MW_HolyGrailWarQuest && Find.QuestManager.QuestsListForReading.Count == 1,
                    "legacy result reconciliation failed");
            }
        });
        Test("completed quest reload never replays notification", () => {
            PrepareEnemy(); State.CurrentWarEntry.EnemyServant.Dead = true; WarOutcomeService.Tick(State);
            var quest = State.warQuest; State.ExposeData(); Scribe.Loading = true; Current.Game = new Game();
            State.ExposeData(); State.LoadedGame(); WarOutcomeService.Tick(State);
            Check(State.warQuest == quest && quest.EndCalls == 1 && quest.Letters == 1
                && quest.State == QuestState.EndedSuccess, "completed quest reopened or renotified");
        });
        Test("lost component reference reuses existing quest", () => {
            PrepareEnemy(); var quest = State.warQuest; State.warQuest = null; State.LoadedGame();
            Check(State.warQuest == quest && Find.QuestManager.QuestsListForReading.Count == 1, "duplicate quest on recovery");
        });
        Test("legacy ended war without a quest creates a silent historical record", () => {
            PrepareEnemy(); State.ExposeData(); Scribe.Data.Remove("warQuest");
            Scribe.Data["warOutcome"] = WarOutcome.PlayerVictory;
            Find.QuestManager = new QuestManager(); Scribe.Loading = true; Current.Game = new Game();
            State.ExposeData(); State.LoadedGame(); State.LoadedGame();
            Check(State.warQuest.State == QuestState.EndedSuccess && State.warQuest.Letters == 0
                && Find.QuestManager.QuestsListForReading.Count == 1 && State.warStartTick == 1234
                && PawnGenerator.Created.Count == 3, "missing quest reopened legacy war");
        });
        Test("workshop deploys original pair once without healing or spending qualification", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
            site.Map = new Map { IsPlayerHome = false }; entry.EnemyServant.needs.Prana.CurLevel = 37;
            site.PostMapGenerate(); site.PostMapGenerate();
            Check(entry.EnemyMaster.Map == site.Map && entry.EnemyServant.Map == site.Map
                && entry.EnemyServant.needs.Prana.CurLevel == 37 && PawnGenerator.Created.Count == 3
                && entry.EnemyServant.State.Master == entry.EnemyMaster && State.warStartTick == 1234
                && !Deploy(), "workshop duplicated, reset or redeployed participants");
        });
        Test("workshop does not pull servant back from an active raid", () => {
            PrepareEnemy(); Check(Deploy(), "raid failed"); var entry = State.CurrentWarEntry;
            var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Map = new Map { IsPlayerHome = false };
            site.PostMapGenerate();
            Check(entry.EnemyMaster.Map == site.Map && entry.EnemyServant.Map == map
                && PawnGenerator.Created.Count == 3, "raid servant duplicated or teleported");
        });
        Test("workshop preserves resting spirit and deadline through departure and reentry", () => {
            Pawn enemy = RestEnemy(false); int since = State.CurrentWarEntry.EnemyRestStartTickAbs;
            enemy.needs.Prana.CurLevel = 17; var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
            site.Map = new Map { IsPlayerHome = false }; site.PostMapGenerate();
            Check(enemy.State.PresenceState == ServantPresenceState.DefeatedSpirit && enemy.needs.Prana.CurLevel == 17, "ambush healed defender");
            bool removeSite; Check(site.ShouldRemoveMapNow(out removeSite) && !removeSite, "retreat deleted live site");
            site.Notify_MyMapAboutToBeRemoved();
            Check(!enemy.Spawned && EnemyContractUtility.IsResting(enemy)
                && State.CurrentWarEntry.EnemyRestStartTickAbs == since, "map unload lost pair or restarted rest");
            site.Map = new Map { IsPlayerHome = false }; site.PostMapGenerate();
            Check(enemy.Map == site.Map && PawnGenerator.Created.Count == 3 && enemy.needs.Prana.CurLevel == 17, "reentry reset pawn");
        });
        Test("workshop partial spawn and lord failures return same world pawns", () => {
            foreach (int failure in new[] { 0, 1, 2 }) {
                Setup(); PrepareEnemy(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
                site.Map = new Map { IsPlayerHome = false };
                GenSpawn.Fail = failure == 0; Verse.AI.Group.LordMaker.Fail = failure == 1; CellFinder.Fail = failure == 2;
                Check(!WarWorkshopService.TryPlaceDefenders(site), "workshop failure accepted");
                var entry = State.CurrentWarEntry;
                Check(!entry.EnemyMaster.Spawned && !entry.EnemyServant.Spawned
                    && Find.WorldPawns.Contains(entry.EnemyMaster) && Find.WorldPawns.Contains(entry.EnemyServant)
                    && entry.EnemyMaster.Lord == null && entry.EnemyServant.Lord == null
                    && PawnGenerator.Created.Count == 3, "workshop failed to rollback original pawns");
                GenSpawn.Fail = Verse.AI.Group.LordMaker.Fail = CellFinder.Fail = false;
                Check(WarWorkshopService.TryPlaceDefenders(site), "workshop retry locked");
            }
        });
        Test("captured and transported enemies are never pulled into workshop", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.EnemyMaster.IsPrisoner = true;
            entry.EnemyServant.ParentHolder = new object(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0];
            site.Map = new Map { IsPlayerHome = false }; site.PostMapGenerate();
            Check(!entry.EnemyMaster.Spawned && !entry.EnemyServant.Spawned, "holder stolen");
        });
        Test("defeated workshop owner permits site removal and normal war victory", () => {
            PrepareEnemy(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Map = new Map { IsPlayerHome = false };
            site.PostMapGenerate(); State.CurrentWarEntry.EnemyMaster.Dead = true; WarOutcomeService.Tick(State);
            bool removeSite; Check(site.ShouldRemoveMapNow(out removeSite) && removeSite
                && State.CurrentWarOutcome == WarOutcome.PlayerVictory && State.warQuest.State == QuestState.EndedSuccess,
                "workshop victory mismatch");
        });
        Test("workshop retreat before first raid starts real rest duration", () => {
            PrepareEnemy(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Map = new Map { IsPlayerHome = false };
            site.PostMapGenerate(); site.Notify_MyMapAboutToBeRemoved();
            Check(EnemyRestUtility.TicksRemaining(State.CurrentWarEntry.EnemyServant) == 180000 && !Deploy(), "workshop retreat bypassed rest");
        });
        Test("ready resting spirit can defend workshop without resetting mana or rest", () => {
            Pawn enemy = RestEnemy(); enemy.needs.Prana.CurLevel = 80;
            int since = State.CurrentWarEntry.EnemyRestStartTickAbs;
            var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Map = new Map { IsPlayerHome = false };
            site.PostMapGenerate();
            Check(enemy.State.PresenceState == ServantPresenceState.Materialized && enemy.needs.Prana.CurLevel == 80
                && State.CurrentWarEntry.EnemyRestStartTickAbs == since, "ready defender reset resources");
        });
        Test("workshop loaded deployment marker preserves already deployed defenders", () => {
            PrepareEnemy(); var site = (Site_WarWorkshop)Find.WorldObjects.All[0]; site.Map = new Map { IsPlayerHome = false };
            site.PostMapGenerate(); var enemy = State.CurrentWarEntry.EnemyServant;
            site.ExposeData(); Scribe.Loading = true; var loaded = new Site_WarWorkshop { Map = site.Map };
            loaded.ExposeData(); loaded.PostMapGenerate();
            Check(enemy.Map == site.Map && PawnGenerator.Created.Count == 3
                && loaded.OwnerMaster == site.OwnerMaster, "site reload duplicated participants");
        });
        Console.WriteLine(passed + " entry and summoning scenarios passed. Native UI, XML loading and real save/load require in-game testing.");
    }
}

namespace UnityEngine { public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); public static float Max(float a, float b) => Math.Max(a, b); public static float Clamp01(float v) => Math.Max(0, Math.Min(1, v)); } }
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public class GameComponent { public virtual void LoadedGame() { } public virtual void GameComponentTick() { } public virtual void ExposeData() { } }
    public class Game { public GameComponent_MoonWorld State; public Game() { State = new GameComponent_MoonWorld(this); } public T GetComponent<T>() where T : class => State as T; }
    public static class Current { public static Game Game; }
    public static class Find { public static TickManager TickManager = new TickManager(); public static WorldPawns WorldPawns = new WorldPawns(); public static FactionManager FactionManager = new FactionManager(); public static WorldObjectsHolder WorldObjects = new WorldObjectsHolder(); public static RimWorld.QuestManager QuestManager = new RimWorld.QuestManager(); }
    public class FactionManager
    {
        public List<Faction> AllFactionsListForReading = new List<Faction>();
        public Faction FirstFactionOfDef(FactionDef def) => AllFactionsListForReading.Find(f => f.def == def);
        public void Add(Faction f) { AllFactionsListForReading.Add(f); }
    }
    public class TickManager { public int TicksGame; }
    public static class GenTicks { public static int TicksAbs => Find.TickManager.TicksGame + 3600000; }
    public class WorldPawns
    {
        public bool FailPass;
        public HashSet<Pawn> Pawns = new HashSet<Pawn>();
        public HashSet<Pawn> ForcefullyKeptPawns = new HashSet<Pawn>();
        public bool Contains(Pawn p) => Pawns.Contains(p);
        public void RemoveAndDiscardPawnViaGC(Pawn p) { Pawns.Remove(p); }
        public void RemovePawn(Pawn p) { Pawns.Remove(p); ForcefullyKeptPawns.Remove(p); p.becameWorldPawnTickAbs = -1; }
        public void PassToWorld(Pawn p, RimWorld.Planet.PawnDiscardDecideMode mode)
        { if (!Pawns.Add(p)) throw new Exception("duplicate world pawn"); p.becameWorldPawnTickAbs = GenTicks.TicksAbs; if (FailPass) throw new Exception("world retention"); }
    }
    public class Map { public object Parent; public IntVec3 Center => new IntVec3 { Valid = true }; public Reachability reachability = new Reachability(); public PlanetTile Tile = new PlanetTile(); public bool IsPlayerHome = true, CanEverExit = true; public Verse.AI.Group.LordManager lordManager = new Verse.AI.Group.LordManager(); }
    public enum TraverseMode { PassDoors }
    public struct TraverseParms { public static TraverseParms For(TraverseMode mode) => new TraverseParms(); }
    public class Reachability { public bool CanReachMapEdge(IntVec3 cell, TraverseParms parms) => !Pawn.EdgeBlocked; }
    public struct IntVec3 { public bool Valid, Fog, Occupied; public int Id; public bool InBounds(Map m) => Valid; public bool Standable(Map m) => Valid; public bool Fogged(Map m) => Fog;
        public Pawn GetFirstPawn(Map m) => Occupied ? new Pawn() : null;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Id == b.Id; public static bool operator !=(IntVec3 a, IntVec3 b) => a.Id != b.Id;
        public override bool Equals(object o) => o is IntVec3 && this == (IntVec3)o; public override int GetHashCode() => Id; }
    public static class CellFinder { public static bool Fail; public static bool TryFindRandomCellNear(IntVec3 c, Map m, int r, Predicate<IntVec3> valid, out IntVec3 result) { result = new IntVec3 { Valid = true, Id = 1 }; return !Fail && valid(result); }
        public static bool TryFindRandomEdgeCellWith(Predicate<IntVec3> valid, Map map, float chance, out IntVec3 result) => TryFindRandomCellNear(default(IntVec3), map, 0, valid, out result); }
    public enum DestroyMode { Vanish }
    public enum WipeMode { Vanish }
    public class Pawn
    {
        public Map MapHeld => Spawned ? Map : null;
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Lodger, Servant, Downed, InMentalState;
        public object ParentHolder; public int becameWorldPawnTickAbs = -1;
        public string LabelShortCap => "测试御主"; public object Rotation; public Health health = new Health(); public Verse.AI.Group.Lord Lord;
        public void DeSpawn() { Spawned = false; Map = null; if (Lord != null) { Lord.Pawn = null; Lord = null; } }
        public bool Spawned = true, IsColonistPlayerControlled = true, Circuit = true;
        public Faction Faction = Faction.OfPlayer;
        public Map Map;
        public ServantIdentityDef Identity;
        public Needs needs = new Needs();
        public static bool EdgeBlocked;
        public bool CanReachMapEdge() => !EdgeBlocked;
        public Story story = new Story();
        public CompMasterCommandSpells Spells;
        public CompServantState State = new CompServantState();
        public Pawn() { Spells = new CompMasterCommandSpells { Pawn = this }; }
        public T TryGetComp<T>() where T : class => (typeof(T) == typeof(CompMasterCommandSpells) ? (object)Spells : State) as T;
        public void Destroy(DestroyMode mode = DestroyMode.Vanish) { Destroyed = true; Spawned = false; Find.WorldPawns.Pawns.Add(this); }
    }
    public class Hediff { public float Severity; }
    public class Hediff_Injury : Hediff { }
    public class HediffSet { public List<Hediff> hediffs = new List<Hediff>(); }
    public class Health { public bool Unsafe; public HediffSet hediffSet = new HediffSet(); public bool ShouldBeDead() => Unsafe; public bool ShouldBeDowned() => Unsafe; }
    public class Needs { public Need_Prana Prana = new Need_Prana(); public T TryGetNeed<T>() where T : class => Prana as T; }
    public class Story { public TraitSet traits = new TraitSet(); }
    public class TraitSet { public List<Trait> allTraits = new List<Trait>(); public bool HasTrait(TraitDef d) => allTraits.Exists(t => t.def == d); public void GainTrait(Trait t) { allTraits.Add(t); } }
    public static class PawnExtensions { public static bool IsQuestLodger(this Pawn p) => p.Lodger; }
    public static class Log { public static void Error(string s) { } public static void Warning(string s) { } }
    public static class Messages { public static void Message(string s, Pawn p, object kind, bool historical) { } }
    public static class DefDatabase<T> { public static List<T> AllDefsListForReading; }
    public class Def { public string defName, label; public virtual IEnumerable<string> ConfigErrors() { yield break; } }
    public static class GenCollection
    {
        public static Queue<int> Draws = new Queue<int>();
        public static List<int> Sizes = new List<int>();
        public static T RandomElement<T>(this List<T> list)
        { Sizes.Add(list.Count); return list[Draws.Count == 0 ? 0 : Draws.Dequeue()]; }
    }
    public enum PawnGenerationContext { NonPlayer }
    public struct PawnGenerationRequest
    {
        public bool ForceNew, Relations;
        public PawnKindDef Kind; public Faction Faction;
        public Predicate<Pawn> Validator;
        public PawnGenerationRequest(PawnKindDef kind, Faction faction, PawnGenerationContext context,
            bool forceGenerateNewPawn, bool canGeneratePawnRelations, Predicate<Pawn> validatorPreGear)
        { ForceNew = forceGenerateNewPawn; Relations = canGeneratePawnRelations; Validator = validatorPreGear; Kind = kind; Faction = faction; }
    }
    public static class PawnGenerator
    {
        public static int Fail;
        public static int FailAt; public static bool FailAfterValidation;
        public static Pawn Last;
        public static List<Pawn> Created = new List<Pawn>();
        public static Action Callback;
        public static PawnGenerationRequest Request;
        public static Pawn GeneratePawn(PawnGenerationRequest request)
        {
            Request = request; if (Fail == 1 || (FailAt == Created.Count + 1 && !FailAfterValidation)) throw new Exception("generation");
            Last = new Pawn { Servant = request.Kind != MW_DefOf.MW_EnemyMaster, Spawned = false, Faction = request.Faction,
                Identity = DefDatabase<ServantIdentityDef>.AllDefsListForReading.Find(i => i.servantKind == request.Kind) };
            Created.Add(Last);
            request.Validator(Last); if (Fail == 2 || (FailAt == Created.Count && FailAfterValidation)) throw new Exception("gear");
            Callback?.Invoke(); return Last;
        }
    }
    public static class GenSpawn
    {
        public static bool Fail; public static Action Callback;
        public static bool LastRespawning; public static void Spawn(Pawn p, IntVec3 c, Map m, object rotation, WipeMode mode, bool respawningAfterLoad) { LastRespawning = respawningAfterLoad; Spawn(p, c, m, mode); }
        public static void Spawn(Pawn p, IntVec3 c, Map m, WipeMode mode)
        { p.Spawned = true; p.Map = m; Callback?.Invoke(); if (Fail) throw new Exception("spawn"); }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static bool Loading; public static Dictionary<string, object> Data = new Dictionary<string, object>(); public static LoadSaveMode mode => Loading ? LoadSaveMode.LoadingVars : LoadSaveMode.Saving; }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue)
        { if (Scribe.Loading) value = Scribe.Data.ContainsKey(key) ? (T)Scribe.Data[key] : defaultValue; else Scribe.Data[key] = value; }
    }
    public static class Scribe_References
    {
        public static void Look(ref Pawn value, string key) { Scribe_Values.Look(ref value, key, (Pawn)null); }
        public static void Look(ref RimWorld.Quest value, string key) { Scribe_Values.Look(ref value, key, (RimWorld.Quest)null); }
    }
    public enum LookMode { Undefined, Value, Reference, Deep }
    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> value, string key, LookMode mode) where T : class
        { if (Scribe.Loading) value = Scribe.Data.ContainsKey(key) ? (List<T>)Scribe.Data[key] : null; else Scribe.Data[key] = value; }
    }
    public static class Scribe_Defs
    {
        public static void Look<T>(ref T value, string key) where T : class { Scribe_Values.Look(ref value, key, (T)null); }
    }
    public static class Scribe_Deep
    {
        public static void Look<T>(ref T value, string key) where T : class, IExposable, new()
        {
            if (Scribe.Loading) value = Scribe.Data.ContainsKey(key) ? new T() : null;
            else if (value != null) Scribe.Data[key] = true;
            value?.ExposeData();
        }
    }
}
namespace RimWorld
{
    public class IncidentParms { public object target; }
    public class IncidentWorker { protected virtual bool CanFireNowSub(IncidentParms p) => true; protected virtual bool TryExecuteWorker(IncidentParms p) => false; public bool TryExecute(IncidentParms p) => TryExecuteWorker(p); }
    public static class MessageTypeDefOf { public static object ThreatBig = new object(), NegativeEvent = new object(), PositiveEvent = new object(); }
    public enum FactionRelationKind { Hostile }
    public class Faction
    {
        public FactionDef def; public bool Neutral;
        public bool HostileTo(Faction other) => def != null && def.permanentEnemy && !Neutral && other != this;
        public void SetRelationDirect(Faction other, FactionRelationKind kind, bool letter) { Neutral = false; }
        public void RemoveAllRelations() { }
        public static Faction OfPlayer = new Faction();
    }
    public class FactionDef { public bool permanentEnemy = true, hidden = true, raidsForbidden = true; }
    public struct FactionGeneratorParms { public FactionDef Def; public FactionGeneratorParms(FactionDef def, bool hidden) { Def = def; } }
    public static class FactionGenerator { public static Faction NewGeneratedFaction(FactionGeneratorParms p) => new Faction { def = p.Def }; }
    public static class PawnsFinder { public static List<Pawn> AllMapsAndWorld_Alive => PawnGenerator.Created; }
    public class TraitDef { }
    public class Trait { public TraitDef def; public Trait(TraitDef d) { def = d; } }
    public class PawnKindDef { public object race = new object(); }
}
namespace MoonWorld
{
    public interface IServantSummoningService { }
    public static class MW_DefOf
    {
        public static QuestScriptDef MW_HolyGrailWarQuest = new QuestScriptDef();
        public static TraitDef MW_CommandSpell = new TraitDef(), MW_MagusCircuit_Basic = new TraitDef(), MW_MageRank_Apprentice = new TraitDef();
        public static Settings MW_HolyGrailWarSettings = new Settings();
        public static FactionDef MW_WarOpposition = new FactionDef();
        public static FactionDef MW_WarOpposition_Saber = new FactionDef(), MW_WarOpposition_Archer = new FactionDef(),
            MW_WarOpposition_Lancer = new FactionDef(), MW_WarOpposition_Assassin = new FactionDef(),
            MW_WarOpposition_Caster = new FactionDef(), MW_WarOpposition_Rider = new FactionDef(), MW_WarOpposition_Berserker = new FactionDef();
        public static PawnKindDef MW_EnemyMaster = new PawnKindDef(); public static object MW_Prana = new object(); public static WorldObjectDef MW_WarWorkshop = new WorldObjectDef(); public static SitePartDef MW_WarWorkshopPart = new SitePartDef();
    }
    public class Settings { public int pranaUpdateIntervalTicks = 250, enemyRestDurationTicks = 180000; public float enemyRaidPranaFraction = .8f; }
    public static class PranaCycleService { public static int Calls; public static void Execute(int ticks) { Calls++; } }
    public static class ServantColonyMembership { public static void ReconcileLoadedGame() { } }
    public static class MasterCircuitUtility { public static bool HasCircuit(Pawn p) => p != null && p.Circuit; public static void EnsureMasterPranaNeed(Pawn p) { } }
    public class CompMasterCommandSpells
    {
        public Pawn Pawn; public int Charges = 3, Grants; public bool Fail;
        public bool TryGrantForWar(out string reason)
        { reason = null; if (Fail) return false; Charges = 3; Grants++; Pawn.story.traits.GainTrait(new Trait(MW_DefOf.MW_CommandSpell)); return true; }
    }
    public enum ServantPresenceState { Materialized, Annihilated, DefeatedSpirit }
    public class Need_Prana { public float CurLevel, MaxLevel = 100; }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; public void Bind(Pawn p) { Master = p; } }
    public class ServantQuery { public static ServantQuery Instance = new ServantQuery(); public bool IsServant(Pawn p) => p.Servant; public bool IsSpirit(Pawn p) => p?.State.PresenceState == ServantPresenceState.DefeatedSpirit; public Pawn GetMaster(Pawn p) => p?.State.Master; }
    public static class ServantIdentityUtility { public static ServantIdentityDef GetIdentity(Pawn p) => p?.Identity; public static ServantResourceProfileDef GetProfile(Pawn p) => new ServantResourceProfileDef(); }
    public class ServantResourceProfileDef { public float materializedSustainThreshold = 30; }
    public static class ServantSustainPolicy { public static float Threshold(Pawn p, ServantPresenceState state) => 60; }
    public static class ServantHealingPolicy { public static Hediff FindWorstCurableCondition(Pawn p) => null; }
    public class LordJob_EnemyWarParty { public bool Retreating; internal void BeginRetreat() { Retreating = true; } }
    public class LordJob_WorkshopRetreat { }
    public class ServantIdentityDef { public HolyGrailWarClassDef classDef; public bool summonable = true; public HolyGrailWarClass warClass; public PawnKindDef servantKind = new PawnKindDef(); }
    public class ServantLifecycleService
    {
        public static ServantLifecycleService Instance = new ServantLifecycleService();
        public static bool Fail; public static Action<Pawn> Callback;
        public bool TryBind(Pawn master, Pawn pawn, out string rejection)
        { pawn.State.Bind(master); rejection = "binding failure"; Callback?.Invoke(pawn); if (Fail) throw new Exception("binding"); return true; }
        public bool TryBindEnemy(Pawn master, Pawn pawn, out string rejection) => TryBind(master, pawn, out rejection);
        public bool TryPrepareEnemyRaid(Pawn pawn, out string rejection) { rejection = "materialization"; if (Fail) return false; pawn.State.PresenceState = ServantPresenceState.Materialized; return true; }
        public bool TryRematerialize(Pawn master, Pawn pawn, out string reason) => TryPrepareEnemyRaid(pawn, out reason);
    }
}
namespace RimWorld.Planet { public enum PawnDiscardDecideMode { Decide, KeepForever } }
namespace Verse.AI.Group
{
    public class Lord { public Pawn Pawn; public object LordJob; }
    public static class LordExtensions { public static Lord GetLord(this Pawn p) => p.Lord; }
    public class LordManager { public void RemoveLord(Lord l) { if (l.Pawn != null) l.Pawn.Lord = null; l.Pawn = null; } }
    public static class LordMaker { public static bool Fail; public static Pawn[] LastPawns;
        public static Lord MakeNewLord(Faction f, object job, Map m, Pawn[] pawns) { LastPawns = pawns; Lord result = new Lord { Pawn = pawns[0], LordJob = job }; pawns[0].Lord = result; if (Fail) throw new Exception("lord"); return result; } }
}
