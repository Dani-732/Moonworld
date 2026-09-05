using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static class EnemyRetreatTests
{
    private static Pawn master, servant;
    private static LordJob_EnemyWarParty party;
    private static int passed;
    private static void Setup()
    {
        Map map = new Map(); master = new Pawn { Map = map }; servant = new Pawn { Map = map, Master = master };
        party = new LordJob_EnemyWarParty(); Lord lord = new Lord { LordJob = party }; party.lord = lord;
        lord.CurLordToil = party.CreateGraph().Toils[0]; servant.Lord = lord; master.Lord = lord;
        Current.Game.Entry = new HolyGrailWarEntry { EnemyMaster = master, EnemyServant = servant, EnemyDeployed = true };
        Find.TickManager.TicksGame = 1;
    }
    private static void Check(bool b, string reason) { if (!b) throw new Exception(reason); }
    private static void Test(string name, Action body) { Setup(); body(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        Test("combat begins in native assault toil", () => { party.LordJobTick(); Check(!party.Retreating && party.lord.CurLordToil is LordToil_AssaultColony, "wrong initial state"); });
        Test("defeated spirit transitions to native exit toil", () => { servant.Spirit = true; party.LordJobTick(); Check(party.Retreating, "no retreat"); });
        Test("master loss triggers retreat", () => { Current.Game.Entry.EnemyEliminated = true; party.LordJobTick(); Check(party.Retreating, "no retreat"); });
        Test("downed master triggers retreat", () => { master.Downed = true; party.LordJobTick(); Check(party.Retreating, "no retreat"); });
        Test("captured master triggers retreat", () => { master.IsPrisoner = true; party.LordJobTick(); Check(party.Retreating, "no retreat"); });
        Test("retreat is one way even if health recovers", () => {
            servant.Spirit = true; party.LordJobTick(); servant.Spirit = false; party.LordJobTick(); Check(party.Retreating, "resumed assault");
        });
        Test("spirit follows master while master remains on map", () => {
            servant.Spirit = true; party.LordJobTick(); Job job = SpiritFollowJobPolicy.CreateJob(servant);
            Check(job.def == MW_DefOf.MW_SpiritFollow && job.targetA.Thing == master && SpiritFollowJobPolicy.IsAllowed(servant, job), "follow failed");
        });
        Test("spirit exits after master leaves", () => {
            servant.Spirit = true; party.LordJobTick(); master.Spawned = false; Job job = SpiritFollowJobPolicy.CreateJob(servant);
            Check(job.def == JobDefOf.Goto && job.exitMapOnArrival && SpiritFollowJobPolicy.IsAllowed(servant, job), "exit job rejected");
        });
        Test("ordinary movement remains rejected in retreat", () => {
            servant.Spirit = true; party.LordJobTick(); master.Spawned = false;
            Check(!SpiritFollowJobPolicy.IsAllowed(servant, JobMaker.MakeJob(JobDefOf.Goto)), "ordinary movement allowed");
        });
        Test("nonedge exit job is rejected", () => {
            servant.Spirit = true; party.LordJobTick(); master.Spawned = false; Job job = JobMaker.MakeJob(JobDefOf.Goto); job.exitMapOnArrival = true;
            Check(!SpiritFollowJobPolicy.IsAllowed(servant, job), "invalid exit allowed");
        });
        Test("player spirit cannot gain enemy exit privilege", () => {
            servant.Enemy = false; master.Spawned = false; Check(SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.Wait, "player escaped");
        });
        Test("combat spirit with absent master does not get retreat exit", () => {
            master.Spawned = false; Check(!EnemyRetreatUtility.ShouldExit(servant), "premature exit");
        });
        Test("restored exit toil preserves retreat behavior", () => {
            party.lord.CurLordToil = party.CreateGraph().Toils[1]; master.Spawned = false;
            Check(EnemyRetreatUtility.ShouldExit(servant), "loaded retreat state ignored");
        });
        Console.WriteLine(passed + " production lord and spirit-job scenarios passed; native pathing and Scribe require in-game testing.");
    }
}
namespace Verse
{
    public class Pawn
    {
        public bool Dead, Downed, IsPrisoner, Spirit; public bool Spawned = true, Enemy = true;
        public Pawn Master; public Map Map; public IntVec3 Position; public Lord Lord;
        public Stances stances = new Stances(); public Abilities abilities = new Abilities();
        public bool HostileTo(Pawn p) => Enemy != p.Enemy;
    }
    public class Stances { public bool FullBodyBusy; }
    public class Abilities { public Ability_NoblePhantasm GetAbility(AbilityDef d) => null; }
    public struct IntVec3
    {
        public bool Edge; public float LengthHorizontalSquared => 1;
        public bool InBounds(Map m) => m != null; public bool OnEdge(Map m) => Edge;
        public bool InHorDistOf(IntVec3 p, float d) => false;
        public static IntVec3 operator -(IntVec3 a, IntVec3 b) => a;
    }
    public struct LocalTargetInfo
    {
        public Pawn Thing; public IntVec3 Cell;
        public LocalTargetInfo(IntVec3 cell) { Cell = cell; Thing = null; }
        public static LocalTargetInfo Invalid => new LocalTargetInfo();
        public static bool operator ==(LocalTargetInfo a, LocalTargetInfo b) => a.Thing == b.Thing;
        public static bool operator !=(LocalTargetInfo a, LocalTargetInfo b) => !(a == b);
        public override bool Equals(object o) => o is LocalTargetInfo && this == (LocalTargetInfo)o;
        public override int GetHashCode() => 0;
    }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
    public class Game { public HolyGrailWarEntry Entry; public T GetComponent<T>() where T : new() => new T(); }
    public static class Current { public static Game Game = new Game(); }
    public class TickManager { public int TicksGame; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public static class Messages { public static void Message(string s, object t, bool h) { } }
}
namespace RimWorld
{
    public class AbilityDef { public float EffectRadius; }
    public static class MessageTypeDefOf { public static object NeutralEvent = new object(); }
    public class LordToil_AssaultColony : LordToil { }
    public static class JobDefOf { public static object Goto = new object(), Wait = new object(), EnterTransporter = new object(); }
}
namespace Verse.AI
{
    public enum LocomotionUrgency { Jog }
    public class Job { public object def; public bool exitMapOnArrival; public LocalTargetInfo targetA; public int expiryInterval; }
    public static class JobMaker { public static Job MakeJob(object d, Pawn p = null) => new Job { def = d, targetA = new LocalTargetInfo { Thing = p } }; }
    public class JobGiver_ExitMapBest { protected Job TryGiveJob(Pawn p) => new Job { def = JobDefOf.Goto, exitMapOnArrival = true, targetA = new LocalTargetInfo(new IntVec3 { Edge = true }) }; }
}
namespace Verse.AI.Group
{
    public class Lord { public LordJob LordJob; public LordToil CurLordToil; public void ReceiveMemo(string s) { CurLordToil = new LordToil_ExitMap(); } }
    public static class LordExtensions { public static Lord GetLord(this Pawn p) => p.Lord; }
    public abstract class LordJob
    {
        public Lord lord; public virtual bool AddFleeToil => true; public virtual bool CanAutoAddPawns => true;
        public abstract StateGraph CreateGraph(); public virtual void LordJobTick() { }
    }
    public class LordToil { }
    public class LordToil_ExitMap : LordToil { public LordToil_ExitMap(LocomotionUrgency l = LocomotionUrgency.Jog, bool interruptCurrentJob = false) { } }
    public class StateGraph { public List<LordToil> Toils = new List<LordToil>(); public void AddToil(LordToil t) { Toils.Add(t); } public void AddTransition(Transition t) { } }
    public class Transition { public Transition(LordToil a, LordToil b) { } public void AddTrigger(object o) { } public void AddPostAction(object o) { } }
    public class Trigger_Memo { public Trigger_Memo(string s) { } }
    public class TransitionAction_EndAllJobs { }
}
namespace MoonWorld
{
    public class GameComponent_MoonWorld { public HolyGrailWarEntry CurrentWarEntry => Current.Game.Entry; }
    public class HolyGrailWarEntry { public Pawn EnemyMaster, EnemyServant; public bool EnemyDeployed, EnemyEliminated; }
    public static class EnemyContractUtility { public static bool HasEnemyContract(Pawn p) => p.Enemy && p.Master != null; }
    public class ServantQuery { public static ServantQuery Instance = new ServantQuery(); public Pawn GetMaster(Pawn p) => p.Master; public bool IsSpirit(Pawn p) => p.Spirit; }
    public class ServantIdentityDef { public List<AbilityDef> noblePhantasms = new List<AbilityDef>(); }
    public static class ServantIdentityUtility { public static ServantIdentityDef GetIdentity(Pawn p) => new ServantIdentityDef(); }
    public class Ability_NoblePhantasm { public bool CanCast; public Verb verb = new Verb(); public void QueueCastingJob(LocalTargetInfo t, LocalTargetInfo d) { } }
    public class Verb { public bool ValidateTarget(LocalTargetInfo t, bool m) => true; }
    public static class ServantTravelAutonomy { public static Job GetSpiritBoardingJob(Pawn p) => null; }
    public static class MW_DefOf { public static object MW_SpiritFollow = new object(); }
}
