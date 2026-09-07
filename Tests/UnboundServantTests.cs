using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

internal static class UnboundServantTests
{
    private static int passed;
    private static Pawn master, servant;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Test(string name, Action body)
    {
        GenTicks.TicksAbs = 1000; PawnsFinder.AllMapsAndWorld_Alive.Clear(); Current.Game = new Game();
        master = new Pawn(); servant = new Pawn { Servant = true, Magic = 27 };
        servant.State.Bind(master); PawnsFinder.AllMapsAndWorld_Alive.AddRange(new[] { master, servant });
        body(); passed++; Console.WriteLine("PASS " + name);
    }
    private static void Lose() { master.Qualified = false; UnboundServantService.NotifyMasterUnavailable(master); }
    private static int Main()
    {
        try
        {
            Test("qualification loss unbinds immediately and preserves magic and presence", () => {
                servant.State.SetPresence(ServantPresenceState.DefeatedSpirit); Lose();
                Check(servant.State.Master == null && servant.State.UnboundUntilTickAbs == 61000
                    && servant.Magic == 27 && servant.State.PresenceState == ServantPresenceState.DefeatedSpirit && !servant.Dead, "loss mutated resources");
            });
            Test("master death adapter starts unbound life without killing servant", () => {
                master.Dead = true; Harmony_MasterDeath.Postfix(master);
                Check(!servant.Dead && servant.State.Master == null && servant.State.UnboundUntilTickAbs == 61000, "master killed servant");
            });
            Test("destroyed master found by global reconciliation", () => {
                master.Destroyed = true; UnboundServantService.Tick(); Check(servant.State.Master == null, "destroyed master retained");
            });
            Test("valid master and servant retain contract", () => {
                UnboundServantService.NotifyMasterUnavailable(master); UnboundServantService.Tick();
                Check(servant.State.Master == master && servant.State.UnboundUntilTickAbs == -1, "valid contract released");
            });
            Test("multiple bound servants are all released without touching other master", () => {
                var second = new Pawn { Servant = true }; second.State.Bind(master);
                var other = new Pawn { Servant = true }; var owner = new Pawn(); other.State.Bind(owner);
                PawnsFinder.AllMapsAndWorld_Alive.AddRange(new[] { second, other }); Lose();
                Check(second.State.Master == null && other.State.Master == owner, "cross-master corruption");
            });
            Test("repeated loss and ticking do not refresh deadline", () => {
                Lose(); GenTicks.TicksAbs += 5000; Lose(); UnboundServantService.Tick();
                Check(servant.State.UnboundUntilTickAbs == 61000, "deadline refreshed");
            });
            Test("one day boundary annihilates once", () => {
                Lose(); GenTicks.TicksAbs = 60999; UnboundServantService.Tick(); Check(!servant.Dead, "early death");
                GenTicks.TicksAbs++; UnboundServantService.Tick(); UnboundServantService.Tick();
                Check(servant.Dead && servant.EndCalls == 1, "missing or repeated annihilation");
            });
            Test("native stat extends duration at loss", () => {
                servant.SurvivalDays = 2.5f; Lose(); Check(servant.State.UnboundUntilTickAbs == 151000, "modifier ignored");
                servant.SurvivalDays = 9; GenTicks.TicksAbs = 70000; UnboundServantService.Tick();
                Check(!servant.Dead && servant.State.UnboundUntilTickAbs == 151000, "late modifier refreshed");
            });
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Test("invalid stat cannot shorten or disable default life " + value, () => {
                    servant.SurvivalDays = value; Lose(); Check(servant.State.UnboundUntilTickAbs == 61000, "invalid deadline");
                });
            Test("large stat cannot wrap absolute tick negative", () => {
                servant.SurvivalDays = float.MaxValue; Lose(); Check(servant.State.UnboundUntilTickAbs == int.MaxValue, "overflow");
            });
            Test("off-map registered enemy expires even absent from world enumeration", () => {
                Current.Game.State.CurrentWarEntry.Enemies.Add(new EnemyWarParticipant { EnemyServant = servant });
                PawnsFinder.AllMapsAndWorld_Alive.Clear(); servant.Spawned = false; Lose();
                GenTicks.TicksAbs = 61000; UnboundServantService.Tick(); Check(servant.Dead, "off-map leak");
            });
            Test("registered player remains tracked after master loss", () => {
                Current.Game.State.CurrentWarEntry.PlayerServant = servant;
                PawnsFinder.AllMapsAndWorld_Alive.Clear(); Lose(); GenTicks.TicksAbs = 61000;
                UnboundServantService.Tick(); Check(servant.Dead, "player reference lost");
            });
            Test("map travel and world transitions retain original deadline", () => {
                Lose(); servant.Spawned = false; GenTicks.TicksAbs = 11000; UnboundServantService.Tick();
                servant.Spawned = true; GenTicks.TicksAbs = 21000; UnboundServantService.Tick();
                Check(servant.State.UnboundUntilTickAbs == 61000, "transition reset");
            });
            Test("suspension cannot bypass an absolute deadline", () => {
                Lose(); servant.Suspended = true; GenTicks.TicksAbs = 61000;
                UnboundServantService.Tick(); Check(servant.Dead, "suspension stopped lifetime");
            });
            Test("old shortage removed before it can force another defeat", () => {
                servant.health.hediffSet.hediffs.Add(new Hediff { def = MW_DefOf.MW_PranaShortage });
                Lose(); Check(servant.health.hediffSet.hediffs.Count == 0 && servant.EndCalls == 0, "old shortage survived");
            });
            Test("foreign non-servant is excluded", () => {
                servant.Servant = false; Lose(); Check(servant.State.Master == master, "nonservant released");
            });
            Test("true servant death leaves master's qualification intact", () => {
                servant.Dead = true; UnboundServantService.Tick(); Check(master.Qualified && !master.Dead, "master lost mark");
            });
            Test("missing master on old save starts one deadline", () => {
                servant.State.Bind(null); UnboundServantService.Tick();
                GenTicks.TicksAbs += 1000; UnboundServantService.Tick(); Check(servant.State.UnboundUntilTickAbs == 61000, "old-save reset");
            });
            Test("saved component deadline survives field roundtrip and elapsed world time", () => {
                Lose(); Scribe.Data.Clear(); Scribe.Loading = false; servant.State.PostExposeData();
                servant.State = new CompServantState { parent = servant }; GenTicks.TicksAbs = 30000;
                Scribe.Loading = true; servant.State.PostExposeData(); Scribe.Loading = false;
                UnboundServantService.Tick(); Check(servant.State.UnboundUntilTickAbs == 61000, "save reset");
                GenTicks.TicksAbs = 61000; UnboundServantService.Tick(); Check(servant.Dead, "loaded expiry ignored");
            });
            Test("binding cancels prior deadline without refill or form reset", () => {
                servant.State.SetPresence(ServantPresenceState.DefeatedSpirit); Lose();
                var replacement = new Pawn(); servant.State.Bind(replacement); GenTicks.TicksAbs = 70000;
                UnboundServantService.Tick(); Check(!servant.Dead && servant.Magic == 27
                    && servant.State.UnboundUntilTickAbs == -1 && servant.State.PresenceState == ServantPresenceState.DefeatedSpirit, "binding reset resources");
            });
            Test("second loss gets new lifetime only after a binding", () => {
                Lose(); var replacement = new Pawn(); servant.State.Bind(replacement); GenTicks.TicksAbs = 50000;
                replacement.Qualified = false; UnboundServantService.Tick(); Check(servant.State.UnboundUntilTickAbs == 110000, "second loss wrong");
            });
            Test("overlapping world and roster references do not duplicate activity cleanup", () => {
                Current.Game.State.CurrentWarEntry.PlayerServant = servant;
                Current.Game.State.CurrentWarEntry.Enemies.Add(new EnemyWarParticipant { EnemyServant = servant });
                Lose(); Check(servant.Reconciles == 1 && UnboundServantService.KnownServants().Count == 1, "duplicate transition");
            });
            Test("losing master does not unconditionally cancel native travel jobs", () => {
                Lose(); Check(servant.jobs.Stops == 0 && servant.Reconciles == 1, "travel interrupted");
            });
            Console.WriteLine(passed + " unbound lifecycle scenarios passed; host doubles do not execute real Scribe or Unity.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}

namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string n) { } } }
namespace Verse
{
    public class CompProperties { public Type compClass; }
    public class ThingComp
    {
        public Pawn parent;
        public virtual void PostExposeData() { }
        public virtual void PostSpawnSetup(bool loaded) { }
        public virtual void CompTick() { }
        public virtual string CompInspectStringExtra() => null;
    }
    public class Pawn
    {
        public bool Dead, Destroyed, Servant, Suspended, Qualified = true, Spawned = true, Drafted;
        public int EndCalls, Reconciles; public float SurvivalDays = 1, Magic;
        public string LabelShort = "Pawn";
        public Health health = new Health(); public Jobs jobs = new Jobs(); public Drafter drafter = new Drafter(); public Needs needs = new Needs();
        public CompServantState State;
        public Pawn() { State = new CompServantState { parent = this }; }
        public T TryGetComp<T>() where T : class => State as T;
        public bool IsHashIntervalTick(int ticks) => true;
        public void Kill() { Dead = true; }
    }
    public class Jobs { public int Stops; public void ClearQueuedJobs() { } public void StopAll(bool a, bool b) { Stops++; } }
    public class Drafter { public bool Drafted; }
    public class Needs { public void AddOrRemoveNeedsAsAppropriate() { } }
    public class Health { public HediffSet hediffSet = new HediffSet(); public void RemoveHediff(Hediff h) { hediffSet.hediffs.Remove(h); } }
    public class Hediff { public object def; }
    public class HediffSet { public List<Hediff> hediffs = new List<Hediff>(); public Hediff GetFirstHediffOfDef(object d) => hediffs.Find(h => h.def == d); }
    public static class GenTicks { public static int TicksAbs; }
    public static class Current { public static Game Game; }
    public class Game { public GameComponent_MoonWorld State = new GameComponent_MoonWorld(); public T GetComponent<T>() where T : class => State as T; }
    public static class Scribe { public static bool Loading; public static Dictionary<string, object> Data = new Dictionary<string, object>(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T fallback)
        { if (Scribe.Loading) value = Scribe.Data.TryGetValue(key, out object stored) ? (T)stored : fallback; else Scribe.Data[key] = value; }
    }
    public static class Scribe_References { public static void Look(ref Pawn p, string key) => Scribe_Values.Look(ref p, key, null); }
}
namespace RimWorld
{
    public static class PawnsFinder { public static List<Pawn> AllMapsAndWorld_Alive = new List<Pawn>(); }
    public static class StatExtension { public static float GetStatValue(this Pawn p, object d, bool applyPostProcess, int cacheStaleAfterTicks) => p.SurvivalDays; }
}
namespace MoonWorld
{
    public class GameComponent_MoonWorld { public HolyGrailWarEntry CurrentWarEntry = new HolyGrailWarEntry(); }
    public class HolyGrailWarEntry { public Pawn PlayerServant; public List<EnemyWarParticipant> Enemies = new List<EnemyWarParticipant>(); }
    public class EnemyWarParticipant { public Pawn EnemyServant; }
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public enum ServantEndReason { UnboundExpired }
    public class ServantQuery { public static ServantQuery Instance = new ServantQuery(); public bool IsServant(Pawn p) => p?.Servant == true; }
    public static class CommandSpellService { public static bool HasQualification(Pawn p) => p != null && p.Qualified && !p.Dead && !p.Destroyed; }
    public static class MW_DefOf { public static object MW_UnboundSurvivalDays = new object(), MW_PranaShortage = new object(), MW_Prana = new object(); }
    public static class ServantIdentityUtility { public static object GetIdentity(Pawn p) => null; }
    public class HolyGrailWarClassDef { public string label; public static HolyGrailWarClassDef For(object identity) => null; }
    public static class ServantSustainPolicy { public static float Threshold(Pawn p) => 0; public static bool IsTogether(Pawn p) => false; public static float SeparationMultiplier(Pawn p) => 2; }
    public static class PawnNeedAccess { public static void EnsureNeed(Pawn p, object def) { } }
    public static class NoblePhantasmService { public static void EnsureAbilities(Pawn p) { } }
    public static class ServantColonyMembership { public static void Initialize(Pawn p) { } }
    public static class ServantPresenceEffects { public static void Reconcile(Pawn p) { p.Reconciles++; } }
    public class ServantLifecycleService
    {
        public static ServantLifecycleService Instance = new ServantLifecycleService();
        public void PrepareForVanillaDeath(Pawn p) { }
        public void Annihilate(Pawn p, ServantEndReason reason) { p.EndCalls++; p.State.SetPresence(ServantPresenceState.Annihilated); p.Kill(); }
    }
}
