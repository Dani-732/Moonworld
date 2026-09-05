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
        Find.Maps.Clear(); Map map = new Map(); Find.Maps.Add(map);
        Faction faction = new Faction { def = MW_DefOf.MW_WarOpposition };
        master = new Pawn { Faction = faction, Circuit = true };
        servant = new Pawn { Faction = faction, Servant = true };
        servant.State.Master = master;
        map.mapPawns.AllPawnsSpawned.Add(master); map.mapPawns.AllPawnsSpawned.Add(servant);
        master.needs.Master.CurLevel = 100; servant.needs.Prana.CurLevel = 50;
        MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay = 240;
    }
    private static void Check(bool b, string message) { if (!b) throw new Exception(message); }
    private static void Near(float actual, float expected) { Check(Math.Abs(actual - expected) < .001f, actual + " != " + expected); }
    private static void Test(string name, Action body) { Setup(); body(); passed++; Console.WriteLine("PASS " + name); }
    private static void Cycle() => PranaCycleService.Execute(250);
    public static void Main()
    {
        Test("enemy fixed supply is one point per interval before upkeep", () => {
            Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); Near(master.needs.Master.CurLevel, 100);
            Near(servant.needs.food.CurLevel, 1);
        });
        Test("enemy ignores empty master pool and safety line", () => {
            master.needs.Master.CurLevel = 0; Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); Near(master.needs.Master.CurLevel, 0);
        });
        Test("enemy supply works without a master need", () => { master.needs.Master = null; Cycle(); Near(servant.needs.Prana.CurLevel, 50.975f); });
        Test("off-map master supplies lone raiding servant", () => {
            master.Spawned = false; Find.Maps[0].mapPawns.AllPawnsSpawned.Remove(master); master.needs.Master = null;
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
        Test("world servant is not simulated", () => {
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
    public class Def { }
    public class Pawn
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Circuit, Servant;
        public bool Spawned = true;
        public int thingIDNumber, Defeats;
        public Faction Faction;
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
    public static class Find { public static List<Map> Maps = new List<Map>(); }
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
    public class Faction { public object def; public static Faction OfPlayer = new Faction(); }
    public class Need { public float CurLevel, MaxLevel = 100; public float CurLevelPercentage => CurLevel / MaxLevel; }
    public class Need_Food : Need { }
    public static class HealthUtility { public static void Cure(Hediff h) { } }
}
namespace MoonWorld
{
    public class Need_Prana : Need { }
    public class Need_MasterPrana : Need_Prana { }
    public enum ServantPresenceState { Materialized, DefeatedSpirit, Annihilated }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; }
    public struct ServantSnapshot { public Pawn master; }
    public static class MW_DefOf
    {
        public static object MW_WarOpposition = new object(), MW_PranaShortage = new object();
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
