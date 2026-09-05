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
        Map map = new Map(); master = new Pawn { Map = map }; servant = new Pawn { Map = map, Master = master, Servant = true };
        party = new LordJob_EnemyWarParty(); Lord lord = new Lord { LordJob = party }; party.lord = lord;
        lord.CurLordToil = party.CreateGraph().Toils[0]; servant.Lord = lord; master.Lord = lord;
        lord.ownedPawns.Add(servant); lord.CurLordToil.lord = lord;
        map.mapPawns.AllPawnsSpawned.Add(servant);
        Current.Game.Entry = new HolyGrailWarEntry { EnemyMaster = master, EnemyServant = servant, EnemyDeployed = true };
        Find.TickManager.TicksGame = 1;
    }
    private static void Check(bool b, string reason) { if (!b) throw new Exception(reason); }
    private static void Test(string name, Action body) { Setup(); body(); passed++; Console.WriteLine("PASS " + name); }
    private static Pawn Target(int x, bool isServant = true)
    {
        Pawn pawn = new Pawn { Map = servant.Map, Enemy = false, Servant = isServant, Position = new IntVec3 { X = x } };
        servant.Map.mapPawns.AllPawnsSpawned.Add(pawn); return pawn;
    }
    public static void Main()
    {
        Test("far servant takes priority over nearby master", () => {
            Pawn human = Target(3, false), hero = Target(90);
            Check(party.GetPreferredTarget(servant) == hero && !party.ValidateAttackTarget(servant, human), "nearest human won");
            Check(party.ValidateAttackTarget(servant, hero), "servant rejected");
        });
        Test("closest eligible servant selected among several", () => {
            Target(40); Pawn close = Target(20); Check(party.GetPreferredTarget(servant) == close, "wrong servant");
        });
        Test("spirit target immediately releases human fallback", () => {
            Pawn hero = Target(20), human = Target(3, false); party.GetPreferredTarget(servant); hero.Spirit = true;
            Check(party.GetPreferredTarget(servant) == null && party.ValidateAttackTarget(servant, human), "spirit retained priority");
        });
        Test("dead downed despawned invisible friendly and disabled targets excluded", () => {
            Target(10).Dead = true; Target(12).Downed = true; Target(14).Spawned = false;
            Target(16).Invisible = true; Target(18).Enemy = true; Target(20).Disabled = true;
            Target(22).AutoTargetable = false;
            Check(party.GetPreferredTarget(servant) == null, "invalid target retained");
        });
        Test("sealed unreachable servant does not stall raid", () => {
            Target(20).Reachable = false; Check(party.GetPreferredTarget(servant) == null, "unreachable servant selected");
        });
        Test("unreachable but shootable servant retains priority", () => {
            Pawn hero = Target(20); hero.Reachable = false; hero.Hittable = true;
            Check(party.GetPreferredTarget(servant) == hero, "shootable target skipped");
        });
        Test("new servant noticed within polling interval and interrupts old job", () => {
            party.LordJobTick(); Pawn hero = Target(20); Find.TickManager.TicksGame += 250; party.LordJobTick();
            Check(party.GetPreferredTarget(servant) == hero && servant.jobs.Ended == 1, "old job retained");
        });
        Test("native combat receives preferred target", () => {
            Pawn hero = Target(90); servant.FallbackTarget = Target(2, false);
            Job job = new JobGiver_EnemyServantAssault().TestGive(servant);
            Check(job != null && servant.mindState.enemyTarget == hero, "combat used human");
        });
        Test("failed shooting position approaches preferred servant", () => {
            Pawn hero = Target(90); servant.NativeJobAvailable = false;
            Job job = new JobGiver_EnemyServantAssault().TestGive(servant);
            Check(job.def == JobDefOf.Goto && job.targetA.Thing == hero && job.expiryInterval == 250, "approach missing");
        });
        Test("no path but shootable target cannot fall through to trashing", () => {
            Pawn hero = Target(20); hero.Reachable = false; hero.Hittable = true; servant.NativeJobAvailable = false;
            Check(new JobGiver_EnemyServantAssault().TestGive(servant).def == JobDefOf.Wait_Combat, "fell through");
        });
        Test("no servant preserves vanilla target selection", () => {
            Pawn human = Target(2, false); servant.FallbackTarget = human;
            new JobGiver_EnemyServantAssault().TestGive(servant);
            Check(servant.mindState.enemyTarget == human, "fallback missing");
        });
        Test("enemy duty installed without affecting legacy master duty", () => {
            party.lord.ownedPawns.Add(master); party.lord.CurLordToil.UpdateAllDuties();
            Check(servant.mindState.duty.def == MW_DefOf.MW_EnemyServantAssault
                && master.mindState.duty.def == DutyDefOf.AssaultColony, "wrong duty");
        });
        Test("test burst targets servant instead of closer master", () => {
            Target(2, false); Pawn hero = Target(20);
            servant.Identity.noblePhantasms.Add(new AbilityDef()); servant.abilities.Ability = new Ability_NoblePhantasm { CanCast = true };
            Find.TickManager.TicksGame = 250; party.LordJobTick();
            Check(servant.abilities.Ability.Casts == 1 && servant.abilities.Ability.LastTarget.Cell.X == hero.Position.X, "burst targeted human");
        });
        Test("out of range servant prevents opportunistic burst on master", () => {
            Target(2, false); Target(60);
            servant.Identity.noblePhantasms.Add(new AbilityDef()); servant.abilities.Ability = new Ability_NoblePhantasm { CanCast = true };
            Find.TickManager.TicksGame = 250; party.LordJobTick();
            Check(servant.abilities.Ability.Casts == 0, "burst bypassed servant priority");
        });
        Test("lone raider with off-map master retreats immediately after defeat", () => {
            master.Spawned = false; master.Map = null; servant.Spirit = true; party.LordJobTick();
            Job job = SpiritFollowJobPolicy.CreateJob(servant);
            Check(party.Retreating && job.exitMapOnArrival && SpiritFollowJobPolicy.IsAllowed(servant, job), "waiting for off-map master");
        });
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
    public class Thing { public bool Spawned = true, Destroyed; public Map Map; public IntVec3 Position; }
    public class Pawn : Thing
    {
        public bool Dead, Downed, IsPrisoner, Spirit, Servant, Invisible, Disabled, Hittable;
        public bool Enemy = true, Reachable = true, AutoTargetable = true, NativeJobAvailable = true;
        public Pawn Master; public Lord Lord;
        public Thing FallbackTarget;
        public MindState mindState = new MindState(); public Jobs jobs = new Jobs();
        public ServantIdentityDef Identity = new ServantIdentityDef();
        public Stances stances = new Stances(); public Abilities abilities = new Abilities();
        public bool HostileTo(Pawn p) => Enemy != p.Enemy;
        public bool IsPsychologicallyInvisible() => Invisible;
        public bool ThreatDisabled(Pawn p) => Disabled;
        public bool CanReach(Pawn p, PathEndMode mode, Danger danger) => p.Reachable;
        public NativeVerb TryGetAttackVerb(Pawn p) => new NativeVerb();
    }
    public enum Danger { Deadly }
    public class NativeVerb { public bool CanHitTarget(Pawn p) => p.Hittable; }
    public class MindState { public Thing enemyTarget; public PawnDuty duty; }
    public class Jobs { public int Ended; public void EndCurrentJob(JobCondition c) { Ended++; } }
    public class Stances { public bool FullBodyBusy; }
    public class Abilities { public Ability_NoblePhantasm Ability; public Ability_NoblePhantasm GetAbility(AbilityDef d) => Ability; }
    public struct IntVec3
    {
        public bool Edge; public int X; public float LengthHorizontalSquared => X * X;
        public bool InBounds(Map m) => m != null; public bool OnEdge(Map m) => Edge;
        public bool InHorDistOf(IntVec3 p, float d) => Math.Abs(X - p.X) <= d;
        public static IntVec3 operator -(IntVec3 a, IntVec3 b) => new IntVec3 { X = a.X - b.X };
    }
    public struct LocalTargetInfo
    {
        public Thing Thing; public IntVec3 Cell;
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
    public static class DutyDefOf { public static DutyDef AssaultColony = new DutyDef(); }
    public static class JobDefOf { public static object Goto = new object(), Wait = new object(), Wait_Combat = new object(), EnterTransporter = new object(); }
    public class JobGiver_AIFightEnemies
    {
        protected virtual Thing FindAttackTarget(Pawn p) => p.FallbackTarget;
        protected virtual Job TryGiveJob(Pawn p)
        { p.mindState.enemyTarget = FindAttackTarget(p); return p.NativeJobAvailable ? JobMaker.MakeJob(JobDefOf.Wait_Combat) : null; }
        public Job TestGive(Pawn p) => TryGiveJob(p);
    }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch }
    public enum JobCondition { InterruptForced }
    public class DutyDef { }
    public class PawnDuty { public DutyDef def; public PawnDuty(DutyDef d) { def = d; } }
    public static class AttackTargetFinder { public static bool IsAutoTargetable(Pawn p) => p.AutoTargetable; }
    public enum LocomotionUrgency { Jog }
    public class Job { public object def; public bool exitMapOnArrival, checkOverrideOnExpire; public LocalTargetInfo targetA; public int expiryInterval; }
    public static class JobMaker { public static Job MakeJob(object d, Pawn p = null) => new Job { def = d, targetA = new LocalTargetInfo { Thing = p } }; }
    public class JobGiver_ExitMapBest { protected Job TryGiveJob(Pawn p) => new Job { def = JobDefOf.Goto, exitMapOnArrival = true, targetA = new LocalTargetInfo(new IntVec3 { Edge = true }) }; }
}
namespace Verse.AI.Group
{
    public class Lord { public LordJob LordJob; public LordToil CurLordToil; public List<Pawn> ownedPawns = new List<Pawn>(); public void ReceiveMemo(string s) { CurLordToil = new LordToil_ExitMap(); } }
    public static class LordExtensions { public static Lord GetLord(this Pawn p) => p.Lord; }
    public abstract class LordJob
    {
        public Lord lord; public virtual bool AddFleeToil => true; public virtual bool CanAutoAddPawns => true;
        public abstract StateGraph CreateGraph(); public virtual void LordJobTick() { }
        public virtual bool ValidateAttackTarget(Pawn p, Thing t) => true;
    }
    public class LordToil { public Lord lord; public virtual void UpdateAllDuties() { } }
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
    public class ServantQuery { public static ServantQuery Instance = new ServantQuery(); public Pawn GetMaster(Pawn p) => p.Master; public bool IsSpirit(Pawn p) => p.Spirit;
        public bool IsMaterialized(Pawn p) => p.Servant && !p.Spirit; }
    public class ServantIdentityDef { public List<AbilityDef> noblePhantasms = new List<AbilityDef>(); }
    public static class ServantIdentityUtility { public static ServantIdentityDef GetIdentity(Pawn p) => p.Identity; }
    public class Ability_NoblePhantasm { public bool CanCast; public int Casts; public LocalTargetInfo LastTarget;
        public Verb verb = new Verb(); public void QueueCastingJob(LocalTargetInfo t, LocalTargetInfo d) { Casts++; LastTarget = t; } }
    public class Verb { public bool ValidateTarget(LocalTargetInfo t, bool m) => Math.Abs(t.Cell.X) <= 30; }
    public static class ServantTravelAutonomy { public static Job GetSpiritBoardingJob(Pawn p) => null; }
    public static class MW_DefOf { public static object MW_SpiritFollow = new object(); public static DutyDef MW_EnemyServantAssault = new DutyDef(); }
}
