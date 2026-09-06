using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

internal static class IndependentPresenceTests
{
    private static Pawn master, servant;
    private static int passed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    {
        master = new Pawn { Map = new Map() }; servant = new Pawn { Map = new Map() };
        servant.State.Master = master; body(); passed++; Console.WriteLine("PASS " + name);
    }
    private static bool Enter() => ServantLifecycleService.Instance.TryEnterVoluntarySpirit(master, servant, out _);
    private static bool Return() => ServantLifecycleService.Instance.TryRematerialize(master, servant, out _);
    public static void Main()
    {
        Test("only resolved battle defeat notifies workshop after spirit state commits", () => {
            var site = new Site_WarWorkshop(); servant.Map.Parent = site;
            Check(Enter() && site.Notifications == 0 && Return(), "voluntary spirit notified workshop");
            Check(ServantLifecycleService.Instance.TryResolveDefeat(servant) && site.Notifications == 1
                && site.LastPresence == ServantPresenceState.DefeatedSpirit, "defeat event missing or premature");
            ServantLifecycleService.Instance.TryResolveDefeat(servant);
            Check(site.Notifications == 1, "repeat defeat duplicated notification");
        });
        Test("different maps allow voluntary spirit and rematerialization", () => {
            Check(Enter() && servant.State.PresenceState == ServantPresenceState.VoluntarySpirit, "enter failed");
            Check(Return() && servant.State.PresenceState == ServantPresenceState.Materialized, "return failed");
        });
        Test("master in caravan does not block own presence changes", () => { master.Spawned = false; master.Map = null; Check(Enter() && Return(), "remote blocked"); });
        Test("off-map servant never materializes using stale map", () => { servant.Spawned = false; Check(!Enter(), "off-map entered"); servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; Check(!Return(), "off-map returned"); });
        Test("dead master rejected", () => { master.Dead = true; Check(!Enter(), "dead accepted"); });
        Test("destroyed master rejected", () => { master.Destroyed = true; Check(!Enter(), "destroyed accepted"); });
        Test("captured master rejected", () => { master.IsPrisoner = true; Check(!Enter(), "captured accepted"); });
        Test("enslaved servant rejected", () => { servant.IsSlave = true; Check(!Enter(), "slave accepted"); });
        Test("hosted servant rejected", () => { servant.HostFaction = new Faction(); Check(!Enter(), "hosted accepted"); });
        Test("wrong contract rejected", () => { servant.State.Master = new Pawn(); Check(!Enter(), "wrong contract"); });
        Test("master without circuits rejected", () => { master.Circuit = false; Check(!Enter(), "no circuit"); });
        Test("downed servant cannot voluntarily enter spirit", () => { servant.Downed = true; Check(!Enter(), "downed accepted"); });
        Test("defeated spirit can recover without master on map", () => { servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; master.Spawned = false; Check(Return(), "remote recovery blocked"); });
        Test("fatal injury blocks rematerialization", () => { servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; servant.health.Fatal = true; Check(!Return(), "fatal returned"); });
        Test("downing injury blocks rematerialization", () => { servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; servant.health.Downing = true; Check(!Return(), "downing returned"); });
        Test("unstandable tile blocks rematerialization", () => { servant.State.PresenceState = ServantPresenceState.VoluntarySpirit; servant.Map.Standable = false; Check(!Return(), "bad cell"); });
        Test("annihilated state cannot return", () => { servant.State.PresenceState = ServantPresenceState.Annihilated; Check(!Return() && !Enter(), "resurrection"); });
        Console.WriteLine(passed + " production lifecycle scenarios passed; actual effects and Scribe remain in-game checks.");
    }
}
namespace Verse
{
    public class Map { public bool Standable = true; public object Parent; }
    public struct IntVec3 { public bool Standable(Map map) => map != null && map.Standable; }
    public class Pawn
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Downed;
        public bool Spawned = true, Circuit = true;
        public Faction Faction = Faction.OfPlayer, HostFaction;
        public Map Map; public IntVec3 Position;
        public string LabelShortCap = "servant";
        public CompServantState State = new CompServantState();
        public Needs needs = new Needs(); public Health health = new Health();
        public T TryGetComp<T>() where T : class => State as T;
        public void Kill(object damage) { Dead = true; }
    }
    public class Needs { public T TryGetNeed<T>() where T : class => null; }
    public class Health
    {
        public bool Fatal, Downing; public HediffSet hediffSet = new HediffSet();
        public bool ShouldBeDead() => Fatal; public bool ShouldBeDowned() => Downing;
        public Hediff AddHediff(object def) { var h = new Hediff { def = def }; hediffSet.hediffs.Add(h); return h; }
    }
    public class Hediff { public object def; public float Severity; }
    public class HediffSet { public List<Hediff> hediffs = new List<Hediff>(); public Hediff GetFirstHediffOfDef(object d) => hediffs.Find(h => h.def == d); }
    public static class Log { public static void Warning(string text) { } }
}
namespace RimWorld { public class Faction { public static Faction OfPlayer = new Faction(); } }
namespace MoonWorld
{
    public class Site_WarWorkshop
    {
        public int Notifications; public ServantPresenceState LastPresence;
        public void NotifyServantDefeated(Pawn pawn) { Notifications++; LastPresence = pawn.State.PresenceState; }
    }
    public interface IServantLifecycle { }
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public enum ServantEndReason { SpiritDamageLimit }
    public class Need_Prana { public float CurLevel; }
    public class CompServantState
    {
        public Pawn Master; public ServantPresenceState PresenceState; public bool DefeatResolutionInProgress;
        public void Bind(Pawn master) { Master = master; }
        public void SetPresence(ServantPresenceState state) { PresenceState = state; }
        public void SetDefeatResolutionInProgress(bool value) { DefeatResolutionInProgress = value; }
    }
    public static class MasterCircuitUtility { public static bool HasCircuit(Pawn pawn) => pawn.Circuit; }
    public class ServantQuery
    {
        public static ServantQuery Instance = new ServantQuery();
        public bool IsServant(Pawn pawn) => true;
        public Pawn GetMaster(Pawn pawn) => pawn.State.Master;
        public bool IsSpirit(Pawn pawn) => pawn.State.PresenceState == ServantPresenceState.VoluntarySpirit || pawn.State.PresenceState == ServantPresenceState.DefeatedSpirit;
    }
    public static class EnemyContractUtility
    {
        public static bool IsWarPawn(Pawn pawn) => false;
        public static bool HasEnemyContract(Pawn pawn) => false;
        public static bool CanReceiveSupply(Pawn pawn) => false;
    }
    public static class ServantColonyMembership { public static void Initialize(Pawn pawn, bool newContract = false) { } }
    public static class ServantPresenceEffects { public static void Reconcile(Pawn pawn) { } }
    public static class ServantFatalDamageRecovery { public static bool TryStabilize(Pawn pawn, Hediff hediff) => true; }
    public class ServantResourceProfileDef { public int maxSpiritDamageStages = 4; }
    public static class ServantIdentityUtility { public static ServantResourceProfileDef GetProfile(Pawn pawn) => new ServantResourceProfileDef(); }
    public static class MW_DefOf { public static object MW_SpiritDamage = new object(); }
}
