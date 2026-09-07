using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

internal static class CommandSpellTests
{
    private static int passed;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action action)
    { action(); passed++; Console.WriteLine("PASS " + name); }
    private static Pawn Master()
    {
        var pawn = new Pawn();
        var arm = new BodyPartRecord();
        var left = new BodyPartRecord { def = BodyPartDefOf.Hand };
        var hand = new BodyPartRecord { def = BodyPartDefOf.Hand, parent = arm };
        hand.parts.Add(new BodyPartRecord { groups = new List<BodyPartGroupDef> { new BodyPartGroupDef { defName = "RightHand" } } });
        arm.parts.Add(hand);
        pawn.RaceProps.body.AllParts.AddRange(new[] { left, arm, hand });
        return pawn;
    }
    private static void Grant(Pawn pawn, int charges = 3)
    { Check(CommandSpellService.TryGrant(pawn, charges, out string reason), reason); }
    private static void Load(Pawn pawn, Dictionary<string, object> data)
    {
        Scribe_Values.Data = data;
        Scribe.mode = LoadSaveMode.LoadingVars; pawn.Spells.PostExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; pawn.Spells.PostExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
    }
    private static Dictionary<string, object> Save(Pawn pawn)
    {
        Scribe_Values.Data = new Dictionary<string, object>();
        Scribe.mode = LoadSaveMode.Saving; pawn.Spells.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        return Scribe_Values.Data;
    }
    private static void LegacyTrait(Pawn pawn) => pawn.story.traits.GainTrait(new Trait(MW_DefOf.MW_CommandSpell));
    private static Pawn Target(Pawn master)
    { return new Pawn { Servant = true, State = new CompServantState { Master = master } }; }
    private static int Main()
    {
        try
        {
            Test("circuit and trait alone never grant health qualification", () => {
                Pawn p = Master(); LegacyTrait(p);
                Check(!CommandSpellService.HasQualification(p) && p.Spells.Charges == 0, "trait granted seals");
            });
            Test("grant uses right hand and exact native severity", () => {
                Pawn p = Master(); Check(p.Spells.TryGrantForWar(out string reason), reason);
                Check(p.Spells.Charges == 3 && CommandSpellService.Mark(p).Part == p.RaceProps.body.AllParts[2], "wrong hand or count");
                Check(p.story.traits.allTraits.Count == 0 && CommandSpellService.HasQualification(p), "trait authority");
            });
            Test("duplicate grant does not refill remaining charge", () => {
                Pawn p = Master(); Grant(p, 1);
                Check(!p.Spells.TryGrantForWar(out _) && p.Spells.Charges == 1 && p.health.hediffSet.hediffs.Count == 1, "grant stacked");
            });
            Test("native hediff merge cannot refill or duplicate", () => {
                Pawn p = Master(); Grant(p, 1);
                p.health.AddHediff(new Hediff_CommandSpell { def = MW_DefOf.MW_CommandSpellMark, Severity = 3 }, CommandSpellService.RightHand(p));
                Check(p.Spells.Charges == 1 && p.health.hediffSet.hediffs.Count == 1, "merged severity");
            });
            Test("third spend removes mark and fourth spend fails", () => {
                Pawn p = Master(); Grant(p);
                for (int i = 2; i >= 0; i--) Check(p.Spells.TrySpendCharge() && p.Spells.Charges == i, "spend mismatch");
                Check(!p.Spells.TrySpendCharge() && !CommandSpellService.HasQualification(p) && p.health.hediffSet.hediffs.Count == 0, "exhaustion retained mark");
            });
            foreach (bool arm in new[] { false, true })
                Test(arm ? "arm loss destroys child mark immediately" : "hand loss destroys mark immediately", () => {
                    Pawn p = Master(); Grant(p, 2); BodyPartRecord hand = CommandSpellService.RightHand(p);
                    p.health.AddHediff(new Hediff_MissingPart(), arm ? hand.parent : hand);
                    Check(!CommandSpellService.HasQualification(p) && p.Spells.Charges == 0, "lost limb retained qualification");
                    Check(p.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_CommandSpellMark) == null, "mark retained");
                    p.health.hediffSet.hediffs.Clear();
                    Check(p.Spells.Charges == 0, "restoration reissued seals");
                });
            Test("left hand loss does not destroy right mark", () => {
                Pawn p = Master(); Grant(p, 2); p.health.AddHediff(new Hediff_MissingPart(), p.RaceProps.body.AllParts[0]);
                Check(p.Spells.Charges == 2, "wrong side lost seals");
            });
            Test("ordinary right hand injury preserves mark", () => {
                Pawn p = Master(); Grant(p, 2); p.health.AddHediff(new Hediff_Injury(), CommandSpellService.RightHand(p));
                Check(p.Spells.Charges == 2, "ordinary injury lost seals");
            });
            Test("added ancestor hides hand and destroys mark", () => {
                Pawn p = Master(); Grant(p); p.health.AddHediff(new Hediff_AddedPart(), CommandSpellService.RightHand(p).parent);
                Check(p.Spells.Charges == 0, "bionic ancestor retained nonexistent hand");
            });
            Test("missing hand cannot receive invitation seals", () => {
                Pawn p = Master(); p.health.AddHediff(new Hediff_MissingPart(), CommandSpellService.RightHand(p));
                Check(!p.Spells.TryGrantForWar(out _), "missing hand accepted");
            });
            Test("unknown or ambiguous right hand rejected", () => {
                Pawn p = Master(); p.RaceProps.body.AllParts[2].parts.Clear();
                Check(!p.Spells.TryGrantForWar(out _), "invented hand");
                p = Master(); p.RaceProps.body.AllParts.Add(p.RaceProps.body.AllParts[2]);
                Check(!p.Spells.TryGrantForWar(out _), "ambiguous hand accepted");
            });
            Test("invalid amount rejected", () => {
                Pawn p = Master(); foreach (int n in new[] { -1, 0, 4 }) Check(!CommandSpellService.TryGrant(p, n, out _), "invalid amount accepted");
            });
            foreach (bool after in new[] { false, true })
                Test(after ? "partial health add exception rolls back" : "rejected health add leaves no mark", () => {
                    Pawn p = Master(); p.health.FailAdd = true; p.health.FailAfterAdd = after;
                    Check(!p.Spells.TryGrantForWar(out _) && p.Spells.Charges == 0 && p.health.hediffSet.hediffs.Count == 0, "partial grant leaked");
                });
            for (int charges = 0; charges <= 3; charges++)
            {
                int n = charges;
                Test("legacy trait migration preserves " + n + " charges", () => {
                    Pawn p = Master(); LegacyTrait(p); Load(p, new Dictionary<string, object> { ["commandSpellCharges"] = n });
                    Check(p.Spells.Charges == n && p.story.traits.allTraits.Count == 0, "legacy count reset");
                    var saved = Save(p);
                    Check((bool)saved["commandSpellHealthMigrated"] && !saved.ContainsKey("commandSpellCharges"), "duplicate persisted charge");
                    p.Spells = new CompMasterCommandSpells { parent = p }; Load(p, saved);
                    Check(p.Spells.Charges == n, "component reload changed hediff");
                });
            }
            Test("omitted old default key migrates three only with old eligibility", () => {
                Pawn p = Master(); LegacyTrait(p); Load(p, new Dictionary<string, object>());
                Check(p.Spells.Charges == 3, "omitted old default lost");
                p = Master(); Load(p, new Dictionary<string, object>());
                Check(p.Spells.Charges == 0, "ordinary pawn gained old default");
            });
            Test("legacy enemy without trait preserves actual component count", () => {
                Pawn p = Master(); p.WarPawn = true;
                Load(p, new Dictionary<string, object> { ["commandSpellCharges"] = 2 });
                Check(p.Spells.Charges == 2, "enemy count reset");
            });
            Test("legacy servant does not gain enemy master seals", () => {
                Pawn p = Master(); p.WarPawn = true; p.Servant = true;
                Load(p, new Dictionary<string, object>());
                Check(p.Spells.Charges == 0, "servant gained seals");
            });
            Test("migration with missing arm is final and cannot refill on restoration", () => {
                Pawn p = Master(); LegacyTrait(p); p.health.AddHediff(new Hediff_MissingPart(), CommandSpellService.RightHand(p).parent);
                Load(p, new Dictionary<string, object> { ["commandSpellCharges"] = 2 });
                var saved = Save(p); p.health.hediffSet.hediffs.Clear(); Load(p, saved);
                Check(p.Spells.Charges == 0, "restoration remigrated");
            });
            Test("existing health mark wins over stale legacy count", () => {
                Pawn p = Master(); Grant(p, 1); LegacyTrait(p);
                Load(p, new Dictionary<string, object> { ["commandSpellCharges"] = 3 });
                Check(p.Spells.Charges == 1 && p.health.hediffSet.hediffs.Count == 1, "dual authority");
            });
            Test("failed legacy migration keeps original data and retries without refill", () => {
                Pawn p = Master(); LegacyTrait(p); p.health.FailAdd = true; p.health.FailAfterAdd = true;
                Load(p, new Dictionary<string, object> { ["commandSpellCharges"] = 2 });
                var saved = Save(p);
                Check(!(bool)saved["commandSpellHealthMigrated"] && (int)saved["commandSpellCharges"] == 2
                    && p.Spells.Charges == 0, "failed migration discarded old data or granted authority");
                p.health.FailAdd = false; Load(p, saved);
                Check(p.Spells.Charges == 2 && !Save(p).ContainsKey("commandSpellCharges"), "retry lost data or kept dual authority");
            });
            Test("generic cure and repeated load never reissue seals", () => {
                Pawn p = Master(); Grant(p, 2); var saved = Save(p); p.health.hediffSet.hediffs.Clear(); LegacyTrait(p);
                Load(p, saved); Load(p, Save(p)); Check(p.Spells.Charges == 0 && p.story.traits.allTraits.Count == 0, "cure reissued");
            });
            Test("death denies spending and resurrection does not refill", () => {
                Pawn p = Master(); Grant(p, 1); p.Dead = true;
                Check(!p.Spells.TrySpendCharge() && !CommandSpellService.HasQualification(p), "dead master spent");
                p.Dead = false; Check(p.Spells.Charges == 1, "resurrection refill");
                p.health.hediffSet.hediffs.Clear(); p.Dead = true; p.Dead = false;
                Check(p.Spells.Charges == 0, "missing mark regenerated");
            });
            Test("miracle spends same health authority and leaves other implants", () => {
                Pawn p = Master(); Grant(p, 1); Pawn servant = Target(p);
                servant.health.AddHediff(new Hediff { def = MW_DefOf.MW_SpiritDamage });
                servant.health.AddHediff(new Hediff_Injury());
                servant.health.AddHediff(new Hediff_Implant());
                Check(p.Spells.TryRecastMiracle(servant, out _) && p.Spells.Charges == 0, "miracle did not spend");
                Check(servant.health.hediffSet.hediffs.Count == 1 && servant.health.hediffSet.hediffs[0] is Hediff_Implant, "miracle removed implant");
            });
            Test("miracle rejects wrong contract without spending", () => {
                Pawn p = Master(); Grant(p); Pawn servant = Target(Master()); servant.health.AddHediff(new Hediff_Injury());
                Check(!p.Spells.TryRecastMiracle(servant, out _) && p.Spells.Charges == 3, "wrong contract spent");
            });
            Test("miracle rejects clean target without spending", () => {
                Pawn p = Master(); Grant(p);
                Check(!p.Spells.TryRecastMiracle(Target(p), out _) && p.Spells.Charges == 3, "clean target spent");
            });
            Test("right hand loss prevents miracle even before health reconciliation", () => {
                Pawn p = Master(); Grant(p); Pawn servant = Target(p); servant.health.AddHediff(new Hediff_Injury());
                p.health.hediffSet.hediffs.Add(new Hediff_MissingPart { Part = CommandSpellService.RightHand(p).parent });
                Check(!p.Spells.TryRecastMiracle(servant, out _) && servant.health.hediffSet.hediffs.Count == 1, "invalid mark healed");
            });
            Console.WriteLine(passed + " command spell scenarios passed; host doubles do not execute Unity or real Scribe.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}

// Only host boundaries are substituted; service, legacy component and health adapter are production sources.
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
}
namespace Verse
{
    public class CompProperties { public Type compClass; }
    public class ThingComp { public Pawn parent; public virtual void PostExposeData() { } public virtual string CompInspectStringExtra() => null; }
    public class Pawn
    {
        public bool Dead, Destroyed, WarPawn, Servant, Circuit = true;
        public string LabelShort = "Pawn", LabelShortCap = "Pawn";
        public Faction Faction = Faction.OfPlayer;
        public Story story = new Story(); public RaceProperties RaceProps = new RaceProperties();
        public Health health; public CompMasterCommandSpells Spells; public CompServantState State;
        public Pawn() { health = new Health(this); Spells = new CompMasterCommandSpells { parent = this }; }
        public T TryGetComp<T>() where T : class => (typeof(T) == typeof(CompMasterCommandSpells) ? (object)Spells : State) as T;
    }
    public class Story { public TraitSet traits = new TraitSet(); }
    public class TraitSet
    {
        public List<Trait> allTraits = new List<Trait>();
        public void GainTrait(Trait t) => allTraits.Add(t);
        public void RemoveTrait(Trait t) => allTraits.Remove(t);
    }
    public class RaceProperties { public BodyDef body = new BodyDef(); }
    public class BodyDef { public List<BodyPartRecord> AllParts = new List<BodyPartRecord>(); }
    public class BodyPartDef { }
    public class BodyPartGroupDef { public string defName; }
    public class BodyPartRecord
    {
        public BodyPartDef def; public BodyPartRecord parent;
        public List<BodyPartRecord> parts = new List<BodyPartRecord>();
        public List<BodyPartGroupDef> groups = new List<BodyPartGroupDef>();
    }
    public class HediffDef { }
    public class Hediff
    {
        public Pawn pawn; public BodyPartRecord Part; public HediffDef def = new HediffDef(); public float Severity = 1;
        public virtual string LabelInBrackets => null; public virtual bool ShouldRemove => Severity <= 0;
        public virtual bool TryMergeWith(Hediff other) { if (other.def != def || other.Part != Part) return false; Severity += other.Severity; return true; }
    }
    public class Hediff_Implant : Hediff { }
    public class Hediff_AddedPart : Hediff { }
    public class Hediff_Injury : Hediff { }
    public class Hediff_MissingPart : Hediff { }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.Find(h => h.def == def);
        public void AddDirect() { }
        public bool PartIsMissing(BodyPartRecord part) => hediffs.Exists(h => h.Part == part && h is Hediff_MissingPart);
        public bool HasDirectlyAddedPartFor(BodyPartRecord part) => hediffs.Exists(h => h.Part == part && h is Hediff_AddedPart);
    }
    public class Health
    {
        public HediffSet hediffSet = new HediffSet(); public bool FailAdd, FailAfterAdd; private Pawn pawn;
        public Health(Pawn pawn) { this.pawn = pawn; }
        public void AddHediff(Hediff h, BodyPartRecord part = null)
        {
            if (FailAdd && !FailAfterAdd) return;
            h.pawn = pawn; h.Part = part ?? h.Part;
            bool merged = false;
            foreach (Hediff current in hediffSet.hediffs) if (current.TryMergeWith(h)) merged = true;
            if (!merged) hediffSet.hediffs.Add(h);
            Harmony_CommandSpellHealth.Postfix(pawn, h);
            if (FailAdd && FailAfterAdd) throw new Exception("Injected health failure");
        }
        public void RemoveHediff(Hediff h) => hediffSet.hediffs.Remove(h);
    }
    public static class HediffMaker
    { public static Hediff MakeHediff(HediffDef def, Pawn pawn, BodyPartRecord part = null) => new Hediff_CommandSpell { def = def, pawn = pawn, Part = part, Severity = 0 }; }
    public class Command_Action
    { public string defaultLabel, defaultDesc; public object icon; public Action action; public float Order; public void Disable(string reason) { } }
    public static class Log { public static void Error(string text) { } public static void Warning(string text) { } }
    public static class Messages { public static void Message(string text, Pawn pawn, object type, bool historical) { } public static void Message(string text, object type, bool historical) { } }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values
    {
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Data.TryGetValue(key, out object found) ? (T)found : defaultValue;
        }
    }
}
namespace RimWorld
{
    public class TraitDef { }
    public class Trait { public TraitDef def; public Trait(TraitDef def) { this.def = def; } }
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public static class BodyPartDefOf { public static BodyPartDef Hand = new BodyPartDef(); }
    public static class TexButton { public static object Reveal; }
    public static class MessageTypeDefOf { public static object PositiveEvent, RejectInput; }
}
namespace MoonWorld
{
    public static class MW_DefOf { public static TraitDef MW_CommandSpell = new TraitDef(); public static HediffDef MW_CommandSpellMark = new HediffDef(), MW_SpiritDamage = new HediffDef(); }
    public static class MasterCircuitUtility { public static bool HasCircuit(Pawn pawn) => pawn?.Circuit == true; }
    public static class EnemyContractUtility { public static bool IsWarPawn(Pawn pawn) => pawn?.WarPawn == true; }
    public enum ServantPresenceState { Materialized, Annihilated }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; }
    public class ServantQuery
    { public static ServantQuery Instance = new ServantQuery(); public bool IsServant(Pawn p) => p?.Servant == true; public Pawn GetMaster(Pawn p) => p?.State?.Master; }
    public static class ServantPresenceEffects { public static void Reconcile(Pawn pawn) { } }
}
