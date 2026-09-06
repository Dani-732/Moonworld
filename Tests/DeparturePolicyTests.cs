// Production travel decisions with minimal host doubles; no Unity pathing or Scribe execution.
using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static class DeparturePolicyTests
{
    private static Pawn master, servant;
    private static int passed;
    private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static void Test(string name, Action body)
    {
        Map map = new Map();
        master = new Pawn { Map = map };
        servant = new Pawn { Map = map, state = new CompServantState { Master = master } };
        body(); passed++; Console.WriteLine("PASS " + name);
    }
    private static Caravan MasterLeaves()
    {
        master.Spawned = false; master.Map = null;
        master.caravan = new Caravan();
        servant.Joinable = master.caravan;
        return master.caravan;
    }
    public static void Main()
    {
        Test("live player contract recognized", () => Check(ServantDepartureService.IsContractServant(servant), "identity lost"));
        Test("ordinary pawn has no servant travel commands", () => Check(!ServantDepartureService.IsContractServant(master), "ordinary pawn affected"));
        Test("prisoner cannot gain exit command", () => { servant.IsPrisoner = true; Check(!ServantTravelAutonomy.CanExitAsPlayer(servant), "prisoner escaped"); });
        Test("slave cannot gain exit command", () => { servant.IsSlave = true; Check(!ServantTravelAutonomy.CanExitAsPlayer(servant), "slave escaped"); });
        Test("enemy cannot gain player exit command", () => { servant.Faction = new Faction(); Check(!ServantTravelAutonomy.CanExitAsPlayer(servant), "enemy privilege"); });
        Test("dead or annihilated servant cannot exit", () => {
            servant.Dead = true; Check(!ServantTravelAutonomy.CanExitAsPlayer(servant), "dead");
            servant.Dead = false; servant.state.PresenceState = ServantPresenceState.Annihilated;
            Check(!ServantTravelAutonomy.CanExitAsPlayer(servant), "annihilated");
        });
        Test("same map still follows master", () => Check(SpiritFollowJobPolicy.CreateJob(servant).def == MW_DefOf.MW_SpiritFollow, "no follow"));
        Test("different map never follows or teleports", () => { master.Map = new Map(); Check(!SpiritFollowJobPolicy.CanFollow(servant, master) && SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.Wait, "cross map follow"); });
        Test("unspawned master never supplies follow coordinates", () => { master.Spawned = false; Check(!SpiritFollowJobPolicy.CanFollow(servant, master), "stale position"); });
        Test("destroyed master never followed", () => { master.Destroyed = true; Check(!SpiritFollowJobPolicy.CanFollow(servant, master), "destroyed master"); });
        Test("master boundary departure uses native caravan exit", () => {
            MasterLeaves(); Job job = SpiritFollowJobPolicy.CreateJob(servant);
            Check(job.exitMapOnArrival && job.failIfCantJoinOrCreateCaravan && SpiritFollowJobPolicy.IsAllowed(servant, job), "retreat missing");
        });
        Test("caravan moved away cannot cause cross map teleport", () => { MasterLeaves(); servant.Joinable = null; Check(SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.Wait, "remote follow"); });
        Test("unrelated caravan cannot attract spirit", () => { MasterLeaves(); servant.Joinable = new Caravan(); Check(!ServantTravelAutonomy.ShouldFollowMasterCaravan(servant), "wrong caravan"); });
        Test("map without native exit never auto exits", () => { MasterLeaves(); servant.Map.CanExit = false; Check(ServantTravelAutonomy.GetSpiritTravelJob(servant) == null, "invalid exit"); });
        Test("unreachable exit leaves spirit safely waiting", () => { MasterLeaves(); servant.Reachable = false; Check(SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.Wait, "bad exit"); });
        Test("player explicitly exits with master still on map", () => {
            Job job = ServantTravelAutonomy.GetPlayerExitJob(servant);
            Check(job.playerForced && SpiritFollowJobPolicy.IsAllowed(servant, job), "player exit blocked");
        });
        Test("defeated spirit also has independent exit", () => {
            servant.state.PresenceState = ServantPresenceState.DefeatedSpirit;
            Check(SpiritFollowJobPolicy.IsAllowed(servant, ServantTravelAutonomy.GetPlayerExitJob(servant)), "defeated blocked");
        });
        Test("job override polling preserves explicit retreat instead of resuming follow", () => {
            servant.CurJob = ServantTravelAutonomy.GetPlayerExitJob(servant);
            Check(SpiritFollowJobPolicy.CreateJob(servant) == servant.CurJob, "retreat replaced by follow");
        });
        Test("ordinary spirit movement remains blocked", () => Check(!SpiritFollowJobPolicy.IsAllowed(servant, JobMaker.MakeJob(JobDefOf.Goto, new IntVec3(1))), "work movement enabled"));
        Test("forged nonedge or out of bounds exit rejected", () => {
            Job job = ServantTravelAutonomy.GetPlayerExitJob(servant);
            job.targetA = new LocalTargetInfo(new IntVec3(1));
            Check(!SpiritFollowJobPolicy.IsAllowed(servant, job), "nonedge exit");
            job.targetA = new LocalTargetInfo(new IntVec3(-1));
            Check(!SpiritFollowJobPolicy.IsAllowed(servant, job), "out of bounds");
        });
        Test("independent caravan assignment suppresses follow and teleport", () => {
            servant.Lord = new Lord { LordJob = new LordJob_FormAndSendCaravan() };
            servant.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, new IntVec3(8));
            Job job = SpiritFollowJobPolicy.CreateJob(servant);
            Check(job.def == JobDefOf.Goto && !job.exitMapOnArrival && SpiritFollowJobPolicy.IsAllowed(servant, job), "caravan travel blocked");
            Check(!SpiritFollowJobPolicy.CanFollow(servant, master), "pulled back to master");
        });
        Test("caravan regroup does not require master in party", () => {
            master.Map = new Map();
            servant.Lord = new Lord { LordJob = new LordJob_FormAndSendCaravan() };
            servant.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, new IntVec3(8));
            Check(SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.Goto, "master still required");
        });
        Test("cancelled caravan rejects stale travel job and resumes follow", () => {
            servant.Lord = new Lord { LordJob = new LordJob_FormAndSendCaravan() };
            servant.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, new IntVec3(8));
            Job job = SpiritFollowJobPolicy.CreateJob(servant); servant.Lord = null;
            Check(!SpiritFollowJobPolicy.IsAllowed(servant, job) && SpiritFollowJobPolicy.CanFollow(servant, master), "stale assignment");
        });
        Test("independent transporter assignment overrides distant master", () => {
            servant.Lord = new Lord { LordJob = new LordJob_LoadAndEnterTransporters() };
            servant.Transporter = new CompTransporter();
            Check(SpiritFollowJobPolicy.CreateJob(servant).def == JobDefOf.EnterTransporter
                && !SpiritFollowJobPolicy.CanFollow(servant, master), "boarding blocked");
        });
        Test("no assigned transporter cannot generate arbitrary boarding", () => Check(!SpiritFollowJobPolicy.IsAllowed(servant, JobMaker.MakeJob(JobDefOf.EnterTransporter, new Thing())), "unassigned boarding"));
        Console.WriteLine(passed + " production travel scenarios passed; actual caravan AI, teleport and save/load require in-game testing.");
    }
}
namespace Verse
{
    public class Thing { }
    public class Map { public bool CanExit = true; }
    public class Pawn : Thing
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave;
        public bool Spawned = true, Reachable = true;
        public Faction Faction = Faction.OfPlayer, HostFaction;
        public Map Map; public CompServantState state; public Lord Lord; public Job CurJob;
        public Caravan caravan, Joinable; public CompTransporter Transporter;
        public MindState mindState = new MindState();
        public T TryGetComp<T>() where T : class => state as T;
        public bool CanReach(Thing target, PathEndMode mode, Danger danger) => Reachable;
    }
    public class MindState { public PawnDuty duty; }
    public enum Danger { Deadly }
    public struct IntVec3
    {
        public int X; public IntVec3(int x) { X = x; }
        public bool InBounds(Map map) => map != null && X >= 0;
        public bool OnEdge(Map map) => X == 9;
    }
    public struct LocalTargetInfo
    {
        public Thing Thing; public IntVec3 Cell;
        public LocalTargetInfo(IntVec3 cell) { Cell = cell; Thing = null; }
        public LocalTargetInfo(Thing thing) { Thing = thing; Cell = new IntVec3(-1); }
        public static bool operator ==(LocalTargetInfo a, LocalTargetInfo b) => a.Thing == b.Thing && a.Cell.X == b.Cell.X;
        public static bool operator !=(LocalTargetInfo a, LocalTargetInfo b) => !(a == b);
        public override bool Equals(object obj) => obj is LocalTargetInfo other && this == other;
        public override int GetHashCode() => Cell.X;
    }
}
namespace RimWorld
{
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public class CompTransporter { public Thing parent = new Thing(); }
    public class LordJob_LoadAndEnterTransporters : LordJob { public int transportersGroup; }
    public class LordJob_FormAndSendCaravan : LordJob { }
    public static class TransporterUtility { public static void GetTransportersInGroup(int id, Map map, List<CompTransporter> group) { } }
    public static class JobGiver_EnterTransporter { public static CompTransporter FindMyTransporter(List<CompTransporter> group, Pawn pawn) => pawn.Transporter; }
    public static class DutyDefOf { public static readonly object TravelOrWait = new object(); }
    public static class JobDefOf { public static readonly object Goto = new object(), Wait = new object(), EnterTransporter = new object(); }
}
namespace RimWorld.Planet
{
    public class Caravan { }
    public static class CaravanUtility { public static Caravan GetCaravan(this Pawn pawn) => pawn.caravan; }
    public static class CaravanExitMapUtility
    {
        public static Caravan FindCaravanToJoinFor(Pawn pawn) => pawn.Joinable;
        public static bool CanExitMapAndJoinOrCreateCaravanNow(Pawn pawn) => pawn.Spawned && pawn.Map.CanExit;
    }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch }
    public class PawnDuty
    {
        public object def; public LocalTargetInfo focus;
        public PawnDuty(object def, IntVec3 cell) { this.def = def; focus = new LocalTargetInfo(cell); }
    }
    public class Job { public object def; public LocalTargetInfo targetA; public bool exitMapOnArrival, failIfCantJoinOrCreateCaravan, playerForced; public int expiryInterval; }
    public static class JobMaker
    {
        public static Job MakeJob(object def) => new Job { def = def };
        public static Job MakeJob(object def, Thing thing) => new Job { def = def, targetA = new LocalTargetInfo(thing) };
        public static Job MakeJob(object def, IntVec3 cell) => new Job { def = def, targetA = new LocalTargetInfo(cell) };
    }
    public class JobGiver_ExitMapBest
    {
        protected bool failIfCantJoinOrCreateCaravan;
        protected Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.Reachable) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, new IntVec3(9));
            job.exitMapOnArrival = true; job.failIfCantJoinOrCreateCaravan = failIfCantJoinOrCreateCaravan; return job;
        }
    }
}
namespace Verse.AI.Group
{
    public class Lord { public LordJob LordJob; }
    public class LordJob { }
    public static class LordExtensions { public static Lord GetLord(this Pawn pawn) => pawn.Lord; }
}
namespace MoonWorld
{
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState = ServantPresenceState.VoluntarySpirit; }
    public struct ServantSnapshot { public Pawn master; public ServantPresenceState presenceState; }
    public class ServantQuery
    {
        public static readonly ServantQuery Instance = new ServantQuery();
        public bool TryGetSnapshot(Pawn pawn, out ServantSnapshot snapshot)
        {
            snapshot = new ServantSnapshot();
            if (pawn.state == null) return false;
            snapshot.master = pawn.state.Master; snapshot.presenceState = pawn.state.PresenceState; return true;
        }
        public Pawn GetMaster(Pawn pawn) => pawn.state?.Master;
    }
    public static class EnemyRetreatUtility
    {
        public static Job ExitJob(Pawn pawn) => null;
        public static bool ShouldExit(Pawn pawn) => false;
    }
    public static class MW_DefOf { public static readonly object MW_SpiritFollow = new object(); }
}
