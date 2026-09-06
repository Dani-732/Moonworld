using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

// Exercise the complete production prana cycle, including exclusive supply routes and upkeep.
internal static class EnemyPranaTests
{
    private static Pawn master, servant;
    private static int passed;
    private static void Setup()
    {
        Current.Game = new Game(); Find.WorldPawns.Pawns.Clear();
        Find.Maps.Clear(); Map map = new Map(); Find.Maps.Add(map);
        Faction faction = new Faction { def = MW_DefOf.MW_WarOpposition };
        master = new Pawn { Faction = faction, Circuit = true, MapHeld = map };
        servant = new Pawn { Faction = faction, Servant = true, MapHeld = map };
        Find.WorldObjects.TravellingTransporters.Clear();
        PawnsFinder.Travel.Clear();
        servant.State.Master = master;
        map.mapPawns.AllPawnsSpawned.Add(master); map.mapPawns.AllPawnsSpawned.Add(servant);
        master.needs.Master.CurLevel = 100; servant.needs.Prana.CurLevel = 50;
        MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay = 240;
    }
    private static void Check(bool b, string message) { if (!b) throw new Exception(message); }
    private static void Near(float actual, float expected) { Check(Math.Abs(actual - expected) < .001f, actual + " != " + expected); }
    private static void Test(string name, Action body) { Setup(); body(); passed++; Console.WriteLine("PASS " + name); }
    private static void Cycle() => PranaCycleService.Execute(250);
    private static void Rest()
    {
        Find.Maps[0].mapPawns.AllPawnsSpawned.Clear();
        master.Spawned = servant.Spawned = false;
        master.MapHeld = servant.MapHeld = null;
        Find.WorldPawns.Pawns.Add(master); Find.WorldPawns.Pawns.Add(servant);
        Current.Game.State.CurrentWarEntry = new HolyGrailWarEntry { EnemyMaster = master, EnemyServant = servant, EnemyDeployed = true };
        servant.State.PresenceState = ServantPresenceState.DefeatedSpirit;
    }
    public static void Main()
    {
        Test("all resting faction servants receive exactly one supply cycle", () => {
            Rest(); var entry = Current.Game.State.CurrentWarEntry;
            for (int i = 0; i < 5; i++)
            {
                var faction = new Faction { def = MW_DefOf.MW_WarOpposition };
                var owner = new Pawn { Faction = faction, Spawned = false, Circuit = true };
                var pawn = new Pawn { Faction = faction, Spawned = false, Servant = true };
                pawn.State.Master = owner; pawn.State.PresenceState = ServantPresenceState.DefeatedSpirit;
                pawn.needs.Prana.CurLevel = 50;
                Find.WorldPawns.Pawns.Add(owner); Find.WorldPawns.Pawns.Add(pawn);
                entry.Additional.Add(new EnemyWarParticipant { EnemyMaster = owner, EnemyServant = pawn, EnemyDeployed = true });
            }
            Cycle();
            foreach (var enemy in entry.Enemies) Near(enemy.EnemyServant.needs.Prana.CurLevel, 50.99375f);
        });
        Test("player caravan receives supply upkeep food and healing once", () => {
            master.Faction = servant.Faction = Faction.OfPlayer;
            servant.Spawned = false; servant.MapHeld = null;
            Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(servant); PawnsFinder.Travel.Add(servant);
            servant.needs.food.CurLevel = .2f;
            servant.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 10 });
            Cycle(); Near(master.needs.Master.CurLevel, 80); Near(servant.needs.Prana.CurLevel, 65.975f);
            Near(servant.health.hediffSet.hediffs[0].Severity, 9);
        });
        Test("master and servant same caravan get base threshold and one regen", () => {
            master.Faction = servant.Faction = Faction.OfPlayer;
            master.Spawned = servant.Spawned = false; master.MapHeld = servant.MapHeld = null;
            master.Caravan = servant.Caravan = new RimWorld.Planet.Caravan();
            Find.Maps[0].mapPawns.AllPawnsSpawned.Clear(); PawnsFinder.Travel.Add(master); PawnsFinder.Travel.Add(servant);
            master.needs.Master.CurLevel = 0; servant.needs.food.CurLevel = .2f;
            Cycle(); Near(master.needs.Master.CurLevel, 1); Near(servant.needs.Prana.CurLevel, 49.975f);
            Near(ServantSustainPolicy.Threshold(servant), 30);
        });
        Test("map and travel overlap never doubles resource cycle", () => {
            PawnsFinder.Travel.Add(servant); PawnsFinder.Travel.Add(master);
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f);
        });
        Test("suspended player held pawn is neither debited nor supplied", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; servant.Suspended = true;
            Cycle(); Near(servant.needs.Prana.CurLevel, 50); Near(master.needs.Master.CurLevel, 100);
        });
        Test("captured player master cannot distribute mana", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; master.IsPrisoner = true;
            servant.needs.food.CurLevel = .2f; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f);
        });
        Test("captured travel servant is outside active player simulation", () => {
            master.Faction = servant.Faction = Faction.OfPlayer;
            servant.Spawned = false; servant.MapHeld = null; servant.IsPrisoner = true;
            Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(servant); PawnsFinder.Travel.Add(servant);
            Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("master captured during player travel stops supply but retains upkeep", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; master.IsPrisoner = true;
            servant.Spawned = false; servant.MapHeld = null; servant.needs.food.CurLevel = .2f;
            Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(servant); PawnsFinder.Travel.Add(servant);
            Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f);
        });
        Test("travel shortage resolves using existing hediff age without advancing twice", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; master.needs.Master.CurLevel = 0;
            servant.Spawned = false; servant.MapHeld = null; servant.needs.Prana.CurLevel = 0; servant.needs.food.CurLevel = .2f;
            Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(servant); PawnsFinder.Travel.Add(servant);
            Hediff shortage = new Hediff { def = MW_DefOf.MW_PranaShortage, ageTicks = 60000 };
            servant.health.hediffSet.hediffs.Add(shortage); Cycle();
            Check(servant.Defeats == 1 && shortage.ageTicks == 60000, "shortage not resolved or double aged");
        });
        Test("mixed map and caravan recipients split surplus once", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; servant.needs.food.CurLevel = .2f;
            var other = new Pawn { Servant = true, Faction = Faction.OfPlayer, Spawned = false };
            other.State.Master = master; other.needs.Prana.CurLevel = 50; other.needs.food.CurLevel = .2f;
            PawnsFinder.Travel.Add(other); Cycle();
            Near(master.needs.Master.CurLevel, 80); Near(servant.needs.Prana.CurLevel, 59.975f); Near(other.needs.Prana.CurLevel, 59.975f);
        });
        Test("same map thresholds remain 30 and 10", () => {
            Near(ServantSustainPolicy.Threshold(servant), 30);
            servant.State.PresenceState = ServantPresenceState.DefeatedSpirit;
            Near(ServantSustainPolicy.Threshold(servant), 10);
        });
        Test("different maps double both thresholds without changing upkeep", () => {
            master.MapHeld = new Map(); Near(ServantSustainPolicy.Threshold(servant), 60);
            servant.State.PresenceState = ServantPresenceState.DefeatedSpirit;
            Near(ServantSustainPolicy.Threshold(servant), 20);
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.99375f);
        });
        Test("two null maps never count as together", () => {
            master.MapHeld = servant.MapHeld = null; Near(ServantSustainPolicy.Threshold(servant), 60);
        });
        Test("same caravan together separate caravans separated", () => {
            master.MapHeld = servant.MapHeld = null;
            master.Caravan = servant.Caravan = new RimWorld.Planet.Caravan();
            Near(ServantSustainPolicy.Threshold(servant), 30);
            master.Caravan = new RimWorld.Planet.Caravan(); Near(ServantSustainPolicy.Threshold(servant), 60);
        });
        Test("same transporter group together different groups separated", () => {
            master.MapHeld = servant.MapHeld = null;
            var transport = new RimWorld.Planet.TravellingTransporters();
            Find.WorldObjects.TravellingTransporters.Add(transport);
            transport.Pawns.Add(master); transport.Pawns.Add(servant);
            Near(ServantSustainPolicy.Threshold(servant), 30);
            transport.Pawns.Remove(master); Near(ServantSustainPolicy.Threshold(servant), 60);
        });
        Test("vanilla stat modifier changes only separated threshold", () => {
            servant.SeparationStat = 1.25f; Near(ServantSustainPolicy.Threshold(servant), 30);
            master.MapHeld = null; Near(ServantSustainPolicy.Threshold(servant), 37.5f);
            servant.SeparationStat = float.NaN; Near(ServantSustainPolicy.Threshold(servant), 60);
            servant.SeparationStat = -1; Near(ServantSustainPolicy.Threshold(servant), 0);
        });
        Test("separated healing cannot spend below effective threshold", () => {
            master.MapHeld = null; servant.needs.Prana.CurLevel = 60;
            var injury = new Hediff_Injury { Severity = 10 }; servant.health.hediffSet.hediffs.Add(injury);
            Cycle(); Near(servant.needs.Prana.CurLevel, 60); Near(injury.Severity, 10 - .975f / 4);
        });
        Test("between thresholds shortage starts apart and clears together", () => {
            Map map = master.MapHeld; master.MapHeld = null;
            Cycle(); Check(servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_PranaShortage) != null, "missing shortage");
            master.MapHeld = map; Cycle(); Check(servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_PranaShortage) == null, "stale shortage");
        });
        Test("current resting opponent receives one fixed supply cycle", () => {
            Rest(); Cycle(); Near(servant.needs.Prana.CurLevel, 50.99375f); Near(master.needs.Master.CurLevel, 100);
        });
        Test("off-map recovery heals injuries and preserves spirit damage", () => {
            Rest(); servant.needs.Prana.CurLevel = 0;
            var injury = new Hediff_Injury { Severity = 20 }; var spiritDamage = new Hediff { Severity = 3 };
            servant.health.hediffSet.hediffs.Add(injury); servant.health.hediffSet.hediffs.Add(spiritDamage);
            for (int i = 0; i < 720; i++) Cycle();
            Near(injury.Severity, 0); Near(spiritDamage.Severity, 3);
            Check(servant.needs.Prana.CurLevel > 80 && servant.State.PresenceState == ServantPresenceState.DefeatedSpirit,
                "rest did not recover mana or prematurely materialized");
        });
        Test("rest healing spends exactly same mana as map healing", () => {
            Rest(); servant.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 10 }); Cycle();
            Near(servant.needs.Prana.CurLevel, 46.99375f);
        });
        Test("captured resting servant receives no remote simulation", () => {
            Rest(); servant.IsPrisoner = true; Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("dead off-map master prevents rest supply", () => {
            Rest(); master.Dead = true; Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("rest does not reach into transport containers", () => {
            Rest(); servant.ParentHolder = new object(); Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("player world servant gains no enemy subsidy", () => {
            Rest(); master.Faction = servant.Faction = Faction.OfPlayer; Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("no double settlement when resting pawn returns to map", () => {
            Rest(); servant.Spawned = true; Find.Maps[0].mapPawns.AllPawnsSpawned.Add(servant);
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.99375f);
        });
        Test("enemy fixed supply is one point per interval before upkeep", () => {
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); Near(master.needs.Master.CurLevel, 100);
            Near(servant.needs.food.CurLevel, 1);
        });
        Test("enemy ignores empty master pool and safety line", () => {
            master.needs.Master.CurLevel = 0; Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); Near(master.needs.Master.CurLevel, 0);
        });
        Test("enemy supply works without a master need", () => { master.needs.Master = null; Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); });
        Test("off-map master supplies lone raiding servant", () => {
            master.Spawned = false; master.MapHeld = null; Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(master); master.needs.Master = null;
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f);
        });
        Test("spirit receives fixed supply with spirit upkeep", () => {
            servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; Cycle(); Near(servant.needs.Prana.CurLevel, 50.99375f);
        });
        Test("dead enemy master provides no supply", () => { master.Dead = true; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f); });
        Test("captured master provides no supply", () => { master.IsPrisoner = true; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f); });
        Test("captured servant receives no supply", () => { servant.IsPrisoner = true; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f); });
        Test("annihilated servant receives no supply or upkeep", () => {
            servant.State.PresenceState = ServantPresenceState.Annihilated; Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("zero configured supply retains upkeep", () => {
            MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay = 0; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f);
        });
        Test("negative configured supply cannot drain extra mana", () => {
            MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay = -100; Cycle(); Near(servant.needs.Prana.CurLevel, 49.975f);
        });
        Test("supply capped at max", () => { servant.needs.Prana.CurLevel = 100; Cycle(); Near(servant.needs.Prana.CurLevel, 99.975f); });
        Test("unregistered world servant is not simulated", () => {
            servant.Spawned = false; Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(servant); Cycle(); Near(servant.needs.Prana.CurLevel, 50);
        });
        Test("enemy healing still spends actual mana", () => {
            servant.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 10 }); Cycle();
            Near(servant.needs.Prana.CurLevel, 46.975f); Near(servant.health.hediffSet.hediffs[0].Severity, 9);
        });
        Test("enemy shortage uses existing defeat path", () => {
            MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay = 0; servant.needs.Prana.CurLevel = 0;
            servant.health.hediffSet.hediffs.Add(new Hediff { def = MW_DefOf.MW_PranaShortage, ageTicks = 60000 });
            Cycle(); Check(servant.Defeats == 1, "shortage did not trigger defeat");
        });
        Test("player still receives surplus distribution and food conversion", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; Cycle();
            Near(master.needs.Master.CurLevel, 80); Check(servant.needs.Prana.CurLevel > 69.975f, "player supply regressed");
            Check(servant.needs.food.CurLevel < 1, "player food route removed");
        });
        Test("player at zero master mana does not get enemy subsidy", () => {
            master.Faction = servant.Faction = Faction.OfPlayer; master.needs.Master.CurLevel = 0;
            servant.needs.food.CurLevel = .2f; Cycle(); Near(master.needs.Master.CurLevel, 1); Near(servant.needs.Prana.CurLevel, 49.975f);
        });
        Console.WriteLine(passed + " production prana cycle scenarios passed; native jobs and saves require in-game testing.");
    }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < .000001f;
    }
}
namespace Verse
{
    public static class DefDatabase<T> { public static List<T> AllDefsListForReading = new List<T>(); }
    public class Def { }
    public class Pawn
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Circuit, Servant, Suspended;
        public bool Spawned = true;
        public Map MapHeld;
        public RimWorld.Planet.Caravan Caravan;
        public float SeparationStat = 2;
        public object ParentHolder;
        public int thingIDNumber, Defeats;
        public Faction Faction, HostFaction;
        public CompServantState State = new CompServantState();
        public Needs needs = new Needs(); public Health health = new Health();
        public T TryGetComp<T>() where T : class => State as T;
    }
    public class Needs
    {
        public Need_Prana Prana = new Need_Prana(); public Need_MasterPrana Master = new Need_MasterPrana(); public Need_Food food = new Need_Food { CurLevel = 1, MaxLevel = 1 };
        public T TryGetNeed<T>() where T : class => (typeof(T) == typeof(Need_MasterPrana) ? (object)Master : Prana) as T;
    }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
    public static class Find { public static List<Map> Maps = new List<Map>(); public static WorldPawns WorldPawns = new WorldPawns(); public static WorldObjects WorldObjects = new WorldObjects(); }
    public class WorldObjects { public List<RimWorld.Planet.TravellingTransporters> TravellingTransporters = new List<RimWorld.Planet.TravellingTransporters>(); }
    public class WorldPawns { public HashSet<Pawn> Pawns = new HashSet<Pawn>(); public bool Contains(Pawn p) => Pawns.Contains(p); }
    public static class Current { public static Game Game; }
    public class Game { public GameComponent_MoonWorld State = new GameComponent_MoonWorld(); public T GetComponent<T>() where T : class => State as T; }
    public class Hediff { public object def; public float Severity; public int ageTicks; }
    public class Hediff_Injury : Hediff { public void Heal(float amount) { Severity -= amount; } }
    public class HediffSet { public List<Hediff> hediffs = new List<Hediff>(); public Hediff GetFirstHediffOfDef(object d) => hediffs.Find(h => h.def == d); }
    public class Health
    {
        public HediffSet hediffSet = new HediffSet();
        public void RemoveHediff(Hediff h) { hediffSet.hediffs.Remove(h); }
        public void AddHediff(object d) { hediffSet.hediffs.Add(new Hediff { def = d }); }
    }
}
namespace RimWorld
{
    public static class PawnsFinder
    {
        public static List<Pawn> Travel = new List<Pawn>();
        public static List<Pawn> AllMapsCaravansAndTravellingTransporters_Alive
        {
            get { var result = new List<Pawn>(Travel); foreach (Map map in Find.Maps) result.AddRange(map.mapPawns.AllPawnsSpawned); return result; }
        }
    }
    public static class StatExtension { public static float GetStatValue(this Pawn p, object def, bool applyPostProcess = true, int cacheStaleAfterTicks = -1) => p.SeparationStat; }
    public class Faction { public object def; public static Faction OfPlayer = new Faction(); public bool HostileTo(Faction f) => def == MW_DefOf.MW_WarOpposition; }
    public class Need { public float CurLevel, MaxLevel = 100; public float CurLevelPercentage => CurLevel / MaxLevel; }
    public class Need_Food : Need { }
    public static class HealthUtility { public static void Cure(Hediff h) { } }
}
namespace RimWorld.Planet
{
    public class Caravan { }
    public class TravellingTransporters { public List<Pawn> Pawns = new List<Pawn>(); }
    public static class CaravanUtility { public static Caravan GetCaravan(this Pawn pawn) => pawn.Caravan; }
}
namespace MoonWorld
{
    public class GameComponent_MoonWorld { public HolyGrailWarEntry CurrentWarEntry; }
    public class EnemyWarParticipant { public Pawn EnemyMaster, EnemyServant; public bool EnemyDeployed; public bool HasEnemyParticipants => EnemyDeployed; public int EnemyRestStartTickAbs = -1;
        public bool EnemyEliminated => EnemyMaster == null || EnemyMaster.Dead || EnemyMaster.Destroyed || EnemyServant == null || EnemyServant.Dead || EnemyServant.Destroyed; }
    public class HolyGrailWarEntry : EnemyWarParticipant
    {
        public List<EnemyWarParticipant> Additional = new List<EnemyWarParticipant>();
        public List<EnemyWarParticipant> Enemies { get { var list = new List<EnemyWarParticipant>(Additional); list.Add(this); return list; } }
        public EnemyWarParticipant FindEnemy(Pawn pawn) => Enemies.Find(e => e.EnemyMaster == pawn || e.EnemyServant == pawn);
    }
    public class HolyGrailWarClassDef { public object oppositionFaction; }
    public class Need_Prana : Need { }
    public class Need_MasterPrana : Need_Prana { }
    public enum ServantPresenceState { Materialized, DefeatedSpirit, Annihilated }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; }
    public struct ServantSnapshot { public Pawn master; }
    public static class MW_DefOf
    {
        public static object MW_WarOpposition = new object(), MW_PranaShortage = new object();
        public static object MW_WarOpposition_Saber = new object(), MW_WarOpposition_Archer = new object(),
            MW_WarOpposition_Lancer = new object(), MW_WarOpposition_Assassin = new object(),
            MW_WarOpposition_Caster = new object(), MW_WarOpposition_Rider = new object(), MW_WarOpposition_Berserker = new object();
        public static object MW_SeparatedSustainMultiplier = new object();
        public static MoonWorldSettingsDef MW_HolyGrailWarSettings = new MoonWorldSettingsDef();
    }
    public class MasterCircuitDef { public float naturalRegenPerDay = 240; }
    public static class MasterCircuitUtility
    {
        public static bool HasCircuit(Pawn p) => p.Circuit;
        public static MasterCircuitDef GetCircuit(Pawn p) => p.Circuit ? new MasterCircuitDef() : null;
    }
    public static class MasterSupplyThresholdService { public static float GetThreshold(Pawn p, Need_MasterPrana need) => 80; }
    public class ServantResourceProfileDef
    {
        public float materializedUpkeepPerDay = 6, spiritUpkeepMultiplier = .25f, materializedSustainThreshold = 30,
            spiritSustainThreshold = 10, foodConversionThreshold = .2f, foodToPranaPerDay = 2,
            foodToPranaEfficiency = .1f, pranaPerHealingPoint = 4, healingMaxPerInterval = 1, conditionCurePranaCost = 40;
        public int shortageDurationTicks = 60000;
    }
    public static class ServantIdentityUtility { public static ServantResourceProfileDef GetProfile(Pawn p) => new ServantResourceProfileDef(); }
    public static class ServantHealingPolicy { public static Hediff FindWorstCurableCondition(Pawn p) => null; }
    public class ServantQuery
    {
        public static ServantQuery Instance = new ServantQuery();
        public Pawn GetMaster(Pawn p) => p?.State.Master;
        public bool IsMaterialized(Pawn p) => p.State.PresenceState == ServantPresenceState.Materialized;
        public bool TryGetSnapshot(Pawn p, out ServantSnapshot s) { s = new ServantSnapshot { master = p.State.Master }; return p.Servant; }
        public void GetBoundServants(Pawn p, List<Pawn> buffer)
        { foreach (Map m in Find.Maps) foreach (Pawn s in m.mapPawns.AllPawnsSpawned) if (s.State.Master == p) buffer.Add(s); }
    }
    public class ServantLifecycleService { public static ServantLifecycleService Instance = new ServantLifecycleService(); public void TryResolveDefeat(Pawn p) { p.Defeats++; } }
}
