using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

// Production entry, war state and summoning service; Unity generation and Scribe use host doubles.
internal static class SummoningTests
{
    private static Pawn master;
    private static Map map;
    private static IntVec3 cell;
    private static int passed;
    private static GameComponent_MoonWorld State => Current.Game.State;
    private static void Check(bool result, string reason) { if (!result) throw new Exception(reason); }
    private static void Setup()
    {
        Current.Game = new Game();
        map = new Map(); master = new Pawn { Map = map }; cell = new IntVec3 { Valid = true };
        Find.TickManager.TicksGame = 1234;
        Find.WorldPawns.Pawns.Clear();
        PawnGenerator.Created.Clear(); Find.FactionManager = new FactionManager();
        CellFinder.Fail = Verse.AI.Group.LordMaker.Fail = false;
        PawnGenerator.Last = null; PawnGenerator.Fail = 0; PawnGenerator.Callback = null;
        GenSpawn.Fail = ServantLifecycleService.Fail = false;
        GenSpawn.Callback = null; ServantLifecycleService.Callback = null;
        DefDatabase<ServantIdentityDef>.AllDefsListForReading = new List<ServantIdentityDef> {
            new ServantIdentityDef { warClass = HolyGrailWarClass.Saber },
            new ServantIdentityDef { warClass = HolyGrailWarClass.Archer } };
        Scribe.Loading = false; Scribe.Data.Clear();
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
        if (PawnGenerator.Last != null)
            Check(PawnGenerator.Last.Destroyed && PawnGenerator.Last.State.Master == null, "failed summon retained pawn or contract");
    }
    private static void PrepareEnemy()
    {
        Accept(); Check(Summon(), "player summon failed"); PawnGenerator.Created.Clear();
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
        foreach (Pawn p in PawnGenerator.Created)
            Check(p.Destroyed && p.State.Master == null && !Find.WorldPawns.Contains(p), "enemy rollback leaked pawn/contract");
    }
    public static void Main()
    {
        Test("circuit alone cannot summon", () => Check(!Summon(), "free qualification"));
        Test("enemy deployment before war rejected", () => Check(!Deploy(), "premature enemy"));
        Test("enemy deployment produces one bound opposing pair", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed");
            Check(EnemyContractUtility.HasEnemyContract(State.CurrentWarEntry.EnemyServant)
                && State.CurrentWarEntry.EnemyMaster.Faction != Faction.OfPlayer
                && State.CurrentWarEntry.EnemyServant.Identity.warClass == HolyGrailWarClass.Archer, "incorrect pair");
            Check(!Deploy() && State.warStartTick == 1234, "repeat deployed or changed time");
        });
        Test("enemy generation failure preserves player settlement", () => { PrepareEnemy(); PawnGenerator.Fail = 1; FailedDeployment(); });
        Test("enemy gear failure cleans captured pawn", () => { PrepareEnemy(); PawnGenerator.Fail = 2; FailedDeployment(); });
        Test("enemy spawn failure cleans both generated pawns", () => { PrepareEnemy(); GenSpawn.Fail = true; FailedDeployment(); });
        Test("enemy partial binding failure cleans both pawns", () => { PrepareEnemy(); ServantLifecycleService.Fail = true; FailedDeployment(); });
        Test("enemy lord failure rolls back pair", () => { PrepareEnemy(); Verse.AI.Group.LordMaker.Fail = true; FailedDeployment(); });
        Test("enemy placement failure permits retry", () => {
            PrepareEnemy(); CellFinder.Fail = true; FailedDeployment(); CellFinder.Fail = false; Check(Deploy(), "retry failed");
        });
        Test("enemy deployment and identities survive round trip", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); Pawn enemy = State.CurrentWarEntry.EnemyServant;
            State.ExposeData(); Scribe.Loading = true; Current.Game = new Game(); State.ExposeData(); State.LoadedGame();
            Check(State.CurrentWarEntry.EnemyServant == enemy && State.CurrentWarEntry.EnemyIdentity == enemy.Identity
                && !Deploy(), "reloaded opponent replaced");
        });
        Test("dead enemy cannot respawn", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); State.CurrentWarEntry.EnemyMaster.Dead = true;
            Check(State.CurrentWarEntry.EnemyEliminated && !Deploy(), "eliminated enemy respawned");
        });
        Test("departed enemy is retained as same pawn", () => {
            PrepareEnemy(); Check(Deploy(), "deployment failed"); Pawn enemy = State.CurrentWarEntry.EnemyServant; enemy.Spawned = false;
            EnemyWarPartyService.RetainDepartedPawn(enemy); Check(Find.WorldPawns.Contains(enemy), "lost departed enemy");
        });
        Test("Saber reserves Archer opponent without spawning", () => {
            Accept(); Check(Summon(), "failed");
            Check(State.CurrentWarEntry.PlayerIdentity.warClass == HolyGrailWarClass.Saber
                && State.CurrentWarEntry.EnemyIdentity.warClass == HolyGrailWarClass.Archer
                && !State.CurrentWarEntry.EnemyDeployed, "incorrect opposing seat");
        });
        Test("Archer reserves Saber opponent", () => {
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.Reverse(); Accept(); Check(Summon(), "failed");
            Check(State.CurrentWarEntry.EnemyIdentity.warClass == HolyGrailWarClass.Saber, "incorrect opponent");
        });
        Test("missing opposite class leaves summon unspent", () => {
            DefDatabase<ServantIdentityDef>.AllDefsListForReading.RemoveAt(1); Accept(); RejectUnspent();
        });
        Test("inactive five classes cannot invent opponents", () => {
            foreach (HolyGrailWarClass seat in new[] { HolyGrailWarClass.Lancer, HolyGrailWarClass.Assassin,
                HolyGrailWarClass.Caster, HolyGrailWarClass.Rider, HolyGrailWarClass.Berserker })
                Check(HolyGrailWarClassUtility.Opponent(seat) == HolyGrailWarClass.None, "inactive seat activated");
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
            Check(PawnGenerator.Last.State.Master == master && PawnGenerator.Last.Map == map, "wrong contract");
            Check(PawnGenerator.Request.ForceNew && !PawnGenerator.Request.Relations, "generation reused a world pawn or created relations");
        });
        Test("second summon cannot overwrite first tick", () => { Accept(); Check(Summon(), "first failed"); Find.TickManager.TicksGame = 9999; Check(!Summon() && State.warStartTick == 1234, "repeat succeeded"); });
        Test("existing allied servant is not a global population cap", () => {
            Pawn existing = new Pawn { Servant = true, Spawned = false }; existing.State.Bind(master);
            Find.WorldPawns.Pawns.Add(existing); Accept(); Check(Summon(), "existing servant blocked event qualification");
            Check(existing.State.Master == master && !existing.Destroyed, "existing contract changed");
        });
        Test("annihilation does not restore qualification", () => { Accept(); Check(Summon(), "first failed"); PawnGenerator.Last.Destroy(); Check(!Summon(), "replacement granted"); });
        Test("master loss does not reopen event", () => { Accept(); master.Dead = true; string reason; Check(!HolyGrailWarEntryService.TryAccept(new Pawn { Map = map }, out reason), "replacement master granted"); });
        Test("dead designated master cannot summon", () => { Accept(); master.Dead = true; RejectUnspent(); });
        Test("captured designated master cannot summon", () => { Accept(); master.IsPrisoner = true; RejectUnspent(); });
        Test("exhausted seals cannot summon", () => { Accept(); master.Spells.Charges = 0; RejectUnspent(); });
        Test("removed seal trait cannot summon", () => { Accept(); master.story.traits.allTraits.Clear(); RejectUnspent(); });
        Test("another map rejected", () => { Accept(); map = new Map(); RejectUnspent(); });
        Test("blocked cell rejected", () => { Accept(); cell = new IntVec3(); RejectUnspent(); });
        Test("fogged cell rejected", () => { Accept(); cell.Fog = true; RejectUnspent(); });
        Test("empty candidate pool preserves qualification", () => { Accept(); DefDatabase<ServantIdentityDef>.AllDefsListForReading.Clear(); RejectUnspent(); });
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
        Console.WriteLine(passed + " entry and summoning scenarios passed. Native UI, XML loading and real save/load require in-game testing.");
    }
}

namespace UnityEngine { public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); } }
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public class GameComponent { public virtual void LoadedGame() { } public virtual void GameComponentTick() { } public virtual void ExposeData() { } }
    public class Game { public GameComponent_MoonWorld State; public Game() { State = new GameComponent_MoonWorld(this); } public T GetComponent<T>() where T : class => State as T; }
    public static class Current { public static Game Game; }
    public static class Find { public static TickManager TickManager = new TickManager(); public static WorldPawns WorldPawns = new WorldPawns(); public static FactionManager FactionManager = new FactionManager(); }
    public class FactionManager
    {
        private Faction faction;
        public Faction FirstFactionOfDef(FactionDef def) => faction;
        public void Add(Faction f) { faction = f; }
    }
    public class TickManager { public int TicksGame; }
    public class WorldPawns
    {
        public HashSet<Pawn> Pawns = new HashSet<Pawn>();
        public bool Contains(Pawn p) => Pawns.Contains(p);
        public void RemoveAndDiscardPawnViaGC(Pawn p) { Pawns.Remove(p); }
        public void RemovePawn(Pawn p) { Pawns.Remove(p); }
        public void PassToWorld(Pawn p, RimWorld.Planet.PawnDiscardDecideMode mode) { Pawns.Add(p); }
    }
    public class Map { public bool IsPlayerHome = true, CanEverExit = true; public Verse.AI.Group.LordManager lordManager = new Verse.AI.Group.LordManager(); }
    public struct IntVec3 { public bool Valid, Fog; public int Id; public bool InBounds(Map m) => Valid; public bool Standable(Map m) => Valid; public bool Fogged(Map m) => Fog;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Id == b.Id; public static bool operator !=(IntVec3 a, IntVec3 b) => a.Id != b.Id;
        public override bool Equals(object o) => o is IntVec3 && this == (IntVec3)o; public override int GetHashCode() => Id; }
    public static class CellFinder { public static bool Fail; public static bool TryFindRandomCellNear(IntVec3 c, Map m, int r, Predicate<IntVec3> valid, out IntVec3 result) { result = new IntVec3 { Valid = true, Id = 1 }; return !Fail && valid(result); } }
    public enum DestroyMode { Vanish }
    public enum WipeMode { Vanish }
    public class Pawn
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Lodger, Servant;
        public bool Spawned = true, IsColonistPlayerControlled = true, Circuit = true;
        public Faction Faction = Faction.OfPlayer;
        public Map Map;
        public ServantIdentityDef Identity;
        public Needs needs = new Needs();
        public bool CanReachMapEdge() => true;
        public Story story = new Story();
        public CompMasterCommandSpells Spells;
        public CompServantState State = new CompServantState();
        public Pawn() { Spells = new CompMasterCommandSpells { Pawn = this }; }
        public T TryGetComp<T>() where T : class => (typeof(T) == typeof(CompMasterCommandSpells) ? (object)Spells : State) as T;
        public void Destroy(DestroyMode mode = DestroyMode.Vanish) { Destroyed = true; Spawned = false; Find.WorldPawns.Pawns.Add(this); }
    }
    public class Needs { public Need_Prana Prana = new Need_Prana(); public T TryGetNeed<T>() where T : class => Prana as T; }
    public class Story { public TraitSet traits = new TraitSet(); }
    public class TraitSet { public List<Trait> allTraits = new List<Trait>(); public bool HasTrait(TraitDef d) => allTraits.Exists(t => t.def == d); public void GainTrait(Trait t) { allTraits.Add(t); } }
    public static class PawnExtensions { public static bool IsQuestLodger(this Pawn p) => p.Lodger; }
    public static class Log { public static void Error(string s) { } }
    public static class DefDatabase<T> { public static List<T> AllDefsListForReading; }
    public static class GenCollection { public static T RandomElement<T>(this List<T> list) => list[0]; }
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
        public static Pawn Last;
        public static List<Pawn> Created = new List<Pawn>();
        public static Action Callback;
        public static PawnGenerationRequest Request;
        public static Pawn GeneratePawn(PawnGenerationRequest request)
        {
            Request = request; if (Fail == 1) throw new Exception("generation");
            Last = new Pawn { Servant = request.Kind != MW_DefOf.MW_EnemyMaster, Spawned = false, Faction = request.Faction,
                Identity = DefDatabase<ServantIdentityDef>.AllDefsListForReading.Find(i => i.servantKind == request.Kind) };
            Created.Add(Last);
            request.Validator(Last); if (Fail == 2) throw new Exception("gear");
            Callback?.Invoke(); return Last;
        }
    }
    public static class GenSpawn
    {
        public static bool Fail; public static Action Callback;
        public static void Spawn(Pawn p, IntVec3 c, Map m, WipeMode mode)
        { p.Spawned = true; p.Map = m; Callback?.Invoke(); if (Fail) throw new Exception("spawn"); }
    }
    public static class Scribe { public static bool Loading; public static Dictionary<string, object> Data = new Dictionary<string, object>(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue)
        { if (Scribe.Loading) value = Scribe.Data.ContainsKey(key) ? (T)Scribe.Data[key] : defaultValue; else Scribe.Data[key] = value; }
    }
    public static class Scribe_References
    {
        public static void Look(ref Pawn value, string key) { Scribe_Values.Look(ref value, key, (Pawn)null); }
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
    public class Faction { public FactionDef def; public bool HostileTo(Faction other) => def == MW_DefOf.MW_WarOpposition; public static Faction OfPlayer = new Faction(); }
    public class FactionDef { }
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
        public static TraitDef MW_CommandSpell = new TraitDef(), MW_MagusCircuit_Basic = new TraitDef(), MW_MageRank_Apprentice = new TraitDef();
        public static Settings MW_HolyGrailWarSettings = new Settings();
        public static FactionDef MW_WarOpposition = new FactionDef();
        public static PawnKindDef MW_EnemyMaster = new PawnKindDef();
    }
    public class Settings { public int pranaUpdateIntervalTicks = 250; }
    public static class PranaCycleService { public static void Execute(int ticks) { } }
    public static class ServantColonyMembership { public static void ReconcileLoadedGame() { } }
    public static class MasterCircuitUtility { public static bool HasCircuit(Pawn p) => p != null && p.Circuit; public static void EnsureMasterPranaNeed(Pawn p) { } }
    public class CompMasterCommandSpells
    {
        public Pawn Pawn; public int Charges = 3, Grants; public bool Fail;
        public bool TryGrantForWar(out string reason)
        { reason = null; if (Fail) return false; Charges = 3; Grants++; Pawn.story.traits.GainTrait(new Trait(MW_DefOf.MW_CommandSpell)); return true; }
    }
    public enum ServantPresenceState { Materialized, Annihilated }
    public class Need_Prana { public float CurLevel, MaxLevel = 100; }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; public void Bind(Pawn p) { Master = p; } }
    public class ServantQuery { public static ServantQuery Instance = new ServantQuery(); public bool IsServant(Pawn p) => p.Servant; public Pawn GetMaster(Pawn p) => p?.State.Master; }
    public static class ServantIdentityUtility { public static ServantIdentityDef GetIdentity(Pawn p) => p?.Identity; }
    public class LordJob_EnemyWarParty { }
    public class ServantIdentityDef { public bool summonable = true; public HolyGrailWarClass warClass; public PawnKindDef servantKind = new PawnKindDef(); }
    public class ServantLifecycleService
    {
        public static ServantLifecycleService Instance = new ServantLifecycleService();
        public static bool Fail; public static Action<Pawn> Callback;
        public bool TryBind(Pawn master, Pawn pawn, out string rejection)
        { pawn.State.Bind(master); rejection = "binding failure"; Callback?.Invoke(pawn); if (Fail) throw new Exception("binding"); return true; }
        public bool TryBindEnemy(Pawn master, Pawn pawn, out string rejection) => TryBind(master, pawn, out rejection);
    }
}
namespace RimWorld.Planet { public enum PawnDiscardDecideMode { KeepForever } }
namespace Verse.AI.Group
{
    public class Lord { }
    public class LordManager { public void RemoveLord(Lord l) { } }
    public static class LordMaker { public static bool Fail; public static Lord MakeNewLord(Faction f, object job, Map m, Pawn[] pawns) { if (Fail) throw new Exception("lord"); return new Lord(); } }
}
