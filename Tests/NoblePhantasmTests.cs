using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

// Production Ability and settlement service, with host doubles for Unity objects and explosion failure injection.
internal static class NoblePhantasmTests
{
    private static Pawn master, servant;
    private static Ability_NoblePhantasm ability;
    private static LocalTargetInfo target;
    private static int passed;
    private static void Setup()
    {
        ThingMaker.Last = null;
        ThingMaker.Fail = GenSpawn.Fail = Explosion.Fail = Ability.FailComplete = false;
        Explosion.Callback = null;
        Find.Targeter = new Targeter();
        Map map = new Map();
        master = new Pawn { Map = map, Faction = Faction.OfPlayer };
        servant = new Pawn { Map = map, Faction = Faction.OfPlayer };
        servant.State.Master = master;
        servant.Identity.noblePhantasms.Add(new AbilityDef());
        NoblePhantasmService.EnsureAbilities(servant);
        ability = (Ability_NoblePhantasm)servant.abilities.GetAbility(servant.Identity.noblePhantasms[0]);
        target = new LocalTargetInfo { Cell = new IntVec3 { Valid = true }, IsValid = true };
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Test(string label, Action body)
    {
        Setup(); body(); passed++; Console.WriteLine("PASS " + label);
    }
    private static bool Cast() { return ability.Activate(target, LocalTargetInfo.Invalid); }
    private static void Reject()
    {
        float before = servant.needs.Prana.CurLevel;
        Check(!Cast(), "unexpected success");
        Check(servant.needs.Prana.CurLevel == before && ThingMaker.Last == null, "failure mutated resources or created explosion");
        Check(ability.CooldownTicksRemaining == 0, "failure started cooldown");
    }
    private static void Overcharge()
    {
        string reason;
        Check(NoblePhantasmService.TryOvercharge(master, servant, out reason), reason);
    }
    public static void Main()
    {
        Test("nonplayer caster rejected", () => { servant.Faction = new Faction(); Reject(); });
        Test("hosted caster rejected", () => { servant.HostFaction = Faction.OfPlayer; Reject(); });
        Test("prisoner caster rejected", () => { servant.IsPrisoner = true; Reject(); });
        Test("slave caster rejected", () => { servant.IsSlave = true; Reject(); });
        Test("normal cast pays once and attributes Bomb damage to servant", () => {
            Check(Cast(), "cast failed");
            Check(servant.needs.Prana.CurLevel == 60 && ThingMaker.Last.damAmount == 40, "wrong cost or damage");
            Check(ThingMaker.Last.instigator == servant && ThingMaker.Last.radius == 3, "wrong instigator or radius");
            Check(ThingMaker.Last.damType == DamageDefOf.Bomb && ability.CooldownTicksRemaining == 300, "wrong damage type or cooldown");
            Check(ThingMaker.Last.doVisualEffects && ThingMaker.Last.doSoundEffects
                && ThingMaker.Last.Sound == DamageDefOf.Bomb.soundExplosion, "vanilla Bomb effects or sound disabled");
            Check(!Cast() && servant.needs.Prana.CurLevel == 60, "cooldown bypass");
        });
        Test("exact magic cost accepted", () => { servant.needs.Prana.CurLevel = 40; Check(Cast() && servant.needs.Prana.CurLevel == 0, "boundary failed"); });
        Test("insufficient magic rejected", () => { servant.needs.Prana.CurLevel = 39; Reject(); });
        Test("invalid cost cannot create free casts", () => { ability.def.Settings.pranaCost = float.NaN; Reject(); });
        Test("invalid damage multiplier rejected", () => { ability.def.Settings.overchargeDamageMultiplier = float.PositiveInfinity; Reject(); });
        Test("magic rechecked after aiming", () => { Check(ability.CanCast, "not available"); servant.needs.Prana.CurLevel = 0; Reject(); });
        Test("aiming without releasing spends nothing", () => { Check(ability.CanCast, "not available"); Check(servant.needs.Prana.CurLevel == 100 && ThingMaker.Last == null, "aiming spent resources"); });
        Test("voluntary spirit rejected", () => { servant.State.PresenceState = ServantPresenceState.VoluntarySpirit; Reject(); });
        Test("defeated spirit rejected", () => { servant.State.PresenceState = ServantPresenceState.DefeatedSpirit; Reject(); });
        Test("annihilated servant rejected", () => { servant.State.PresenceState = ServantPresenceState.Annihilated; Reject(); });
        Test("dead master rejected", () => { master.Dead = true; Reject(); });
        Test("missing contract rejected", () => { servant.State.Master = null; Reject(); });
        Test("different map rejected", () => { master.Map = new Map(); Reject(); });
        Test("downed caster rejected", () => { servant.Downed = true; Reject(); });
        Test("incapable of violence rejected", () => { servant.NoViolence = true; Reject(); });
        Test("unowned ability rejected", () => { servant.Identity.noblePhantasms.Clear(); Reject(); });
        Test("out of bounds rejected", () => { target.Cell = new IntVec3(); Reject(); });
        Test("range or line of sight failure rejected", () => { ability.verb.Valid = false; Reject(); });
        Test("cross map thing target rejected", () => { target.Thing = new Thing { Map = new Map() }; Reject(); });
        Test("overcharge spends one seal and refuses stacking", () => {
            Overcharge(); string reason;
            Check(master.Spells.Charges == 2 && NoblePhantasmService.IsOvercharged(servant), "overcharge not recorded");
            Check(!NoblePhantasmService.TryOvercharge(master, servant, out reason) && master.Spells.Charges == 2, "stack spent charge");
        });
        Test("overcharge permits zero magic and doubles one explosion", () => {
            Overcharge(); servant.needs.Prana.CurLevel = 0;
            Check(Cast() && ThingMaker.Last.damAmount == 80, "no overcharge damage");
            Check(servant.needs.Prana.CurLevel == 0 && !NoblePhantasmService.IsOvercharged(servant) && master.Spells.Charges == 2, "wrong consumption");
            ability.ResetCooldown(); ThingMaker.Last = null; Reject();
        });
        Test("invalid release retains pending overcharge", () => { Overcharge(); ability.verb.Valid = false; Reject(); Check(NoblePhantasmService.IsOvercharged(servant), "lost pending"); });
        Test("no seals cannot apply overcharge", () => { master.Spells.Charges = 0; string reason; Check(!NoblePhantasmService.TryOvercharge(master, servant, out reason) && !NoblePhantasmService.IsOvercharged(servant), "free overcharge"); });
        Test("another master's seal cannot affect servant", () => { string reason; Check(!NoblePhantasmService.TryOvercharge(new Pawn { Map = master.Map, Faction = Faction.OfPlayer }, servant, out reason), "wrong master accepted"); });
        Test("explosion creation failure refunds resources", () => { ThingMaker.Fail = true; Reject(); });
        Test("spawn failure destroys partial explosion and refunds", () => { GenSpawn.Fail = true; Check(!Cast() && servant.needs.Prana.CurLevel == 100 && ThingMaker.Last.Destroyed, "rollback failed"); });
        Test("initialization failure refunds and removes explosion", () => { Explosion.Fail = true; Check(!Cast() && servant.needs.Prana.CurLevel == 100 && ThingMaker.Last.Destroyed && ability.CooldownTicksRemaining == 0, "rollback failed"); });
        Test("overcharge initialization failure restores pending", () => { Overcharge(); Explosion.Fail = true; Check(!Cast() && NoblePhantasmService.IsOvercharged(servant) && master.Spells.Charges == 2 && ThingMaker.Last.Destroyed, "pending rollback failed"); });
        Test("bookkeeping failure resets cooldown and refunds", () => { Ability.FailComplete = true; Check(!Cast() && servant.needs.Prana.CurLevel == 100 && ThingMaker.Last.Destroyed && ability.CooldownTicksRemaining == 0, "bookkeeping rollback failed"); });
        Test("reentrant cast cannot double spend", () => { Explosion.Callback = () => Check(!Cast(), "reentrant cast succeeded"); Check(Cast() && servant.needs.Prana.CurLevel == 60, "outer cast failed"); });
        Test("ability reconciliation does not reset cooldown", () => { Check(Cast(), "cast failed"); NoblePhantasmService.EnsureAbilities(servant); Check(servant.abilities.GetAbility(ability.def) == ability && ability.CooldownTicksRemaining == 300, "ability replaced"); });
        Console.WriteLine(passed + " settlement scenarios passed; vanilla rendering, jobs and save/load need in-game testing.");
    }
}

namespace UnityEngine
{
    public class Event { }
    public static class Mathf { public static int RoundToInt(float f) => (int)Math.Round(f); }
}
namespace Verse.Sound { public static class SoundExtensions { public static void PlayOneShotOnCamera(this object sound) { } } }
namespace Verse
{
    public class Map { }
    public enum DestroyMode { Vanish }
    public enum WorkTags { Violent }
    public struct IntVec3 { public bool Valid; public bool InBounds(Map map) => Valid && map != null; }
    public struct LocalTargetInfo
    {
        public static LocalTargetInfo Invalid = new LocalTargetInfo();
        public IntVec3 Cell; public bool IsValid; public Thing Thing; public bool HasThing => Thing != null;
    }
    public struct AcceptanceReport
    {
        public bool Accepted; public AcceptanceReport(string reason) { Accepted = false; }
        public static implicit operator bool(AcceptanceReport r) => r.Accepted;
        public static implicit operator AcceptanceReport(bool b) => new AcceptanceReport { Accepted = b };
    }
    public class DefModExtension { public virtual IEnumerable<string> ConfigErrors() { yield break; } }
    public class Thing
    {
        public Map Map; public bool Spawned = true, Destroyed;
        public void Destroy(DestroyMode mode) { Destroyed = true; Spawned = false; }
    }
    public class Pawn : Thing
    {
        public bool Dead, Downed, InMentalState, NoViolence, IsPrisoner, IsSlave;
        public Faction Faction, HostFaction; public Pawn_AbilityTracker abilities;
        public Needs needs = new Needs(); public Health health = new Health();
        public CompServantState State = new CompServantState();
        public CompMasterCommandSpells Spells = new CompMasterCommandSpells();
        public ServantIdentityDef Identity = new ServantIdentityDef();
        public bool WorkTagIsDisabled(WorkTags tag) => NoViolence;
        public T TryGetComp<T>() where T : class => (State as T) ?? (Spells as T);
    }
    public class Needs { public Need_Prana Prana = new Need_Prana(); public T TryGetNeed<T>() where T : class => Prana as T; }
    public class Hediff { public object def; }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(object def) => hediffs.Find(h => h.def == def);
    }
    public class Health
    {
        public HediffSet hediffSet = new HediffSet();
        public void AddHediff(Hediff h) { hediffSet.hediffs.Add(h); }
        public void RemoveHediff(Hediff h) { hediffSet.hediffs.Remove(h); }
    }
    public static class HediffMaker { public static Hediff MakeHediff(object def, Pawn pawn) => new Hediff { def = def }; }
    public class Explosion : Thing
    {
        public bool doVisualEffects, doSoundEffects; public object Sound;
        public static bool Fail; public static Action Callback;
        public float radius, armorPenetration; public object damType; public Thing instigator; public int damAmount;
        public void StartExplosion(object sound, object ignored)
        { Sound = sound; if (Fail) throw new Exception("injected initialization failure"); Callback?.Invoke(); }
    }
    public static class ThingMaker
    {
        public static Explosion Last; public static bool Fail;
        public static Thing MakeThing(object def) { if (Fail) throw new Exception("injected creation failure"); return Last = new Explosion(); }
    }
    public static class GenSpawn
    {
        public static bool Fail;
        public static void Spawn(Thing t, IntVec3 cell, Map map) { t.Map = map; if (Fail) throw new Exception("injected spawn failure"); }
    }
    public static class Messages { public static void Message(string s, object type, bool historical) { } }
    public static class Log { public static void Error(string s) { } }
    public static class Find
    {
        public static Targeter Targeter = new Targeter();
        public static DesignatorManager DesignatorManager = new DesignatorManager();
    }
    public class DesignatorManager { public void Deselect() { } }
}
namespace RimWorld
{
    public class Targeter
    {
        public Verb Source; public bool AllowNonSelected;
        public void BeginTargeting(Verb source, bool allowNonSelectedTargetingSource = false)
        { Source = source; AllowNonSelected = allowNonSelectedTargetingSource; }
    }
    public class Command_Ability
    {
        protected Ability ability;
        public Command_Ability(Ability ability, Pawn pawn) { this.ability = ability; }
        public virtual void ProcessInput(UnityEngine.Event ev) { }
    }
    public static class SoundDefOf { public static object Tick_Tiny = new object(); }
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public class DamageDef { public object soundExplosion = new object(); }
    public static class DamageDefOf { public static DamageDef Bomb = new DamageDef(); }
    public static class ThingDefOf { public static object Explosion = new object(); }
    public static class MessageTypeDefOf { public static object RejectInput = new object(); }
    public class AbilityDef
    {
        public float EffectRadius = 3;
        public NoblePhantasmExtension Settings = new NoblePhantasmExtension();
        public T GetModExtension<T>() where T : class => Settings as T;
    }
    public class Verb { public bool Valid = true; public bool ValidateTarget(LocalTargetInfo t, bool m) => Valid; }
    public class Ability
    {
        public static bool FailComplete;
        public Pawn pawn; public AbilityDef def; public Verb verb = new Verb(); public int CooldownTicksRemaining;
        public Ability() { }
        public Ability(Pawn pawn) { this.pawn = pawn; }
        public Ability(Pawn pawn, AbilityDef def) { this.pawn = pawn; this.def = def; }
        public virtual AcceptanceReport CanCast => CooldownTicksRemaining == 0;
        public bool GizmoDisabled(out string reason) { reason = "disabled"; return !CanCast; }
        public virtual string Tooltip => "";
        public void ResetCooldown() { CooldownTicksRemaining = 0; }
        public virtual bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        { CooldownTicksRemaining = 300; if (FailComplete) throw new Exception("injected bookkeeping failure"); return true; }
    }
    public class Pawn_AbilityTracker
    {
        private Pawn pawn; private List<Ability> abilities = new List<Ability>();
        public Pawn_AbilityTracker(Pawn p) { pawn = p; }
        public Ability GetAbility(AbilityDef d) => abilities.Find(a => a.def == d);
        public void GainAbility(AbilityDef d) { abilities.Add(new Ability_NoblePhantasm(pawn, d)); }
    }
}
namespace MoonWorld
{
    public class Need_Prana { public float CurLevel = 100; }
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; }
    public class CompMasterCommandSpells
    {
        public int Charges = 3;
        public bool TrySpendCharge() { if (Charges <= 0) return false; Charges--; return true; }
    }
    public class ServantIdentityDef { public List<AbilityDef> noblePhantasms = new List<AbilityDef>(); }
    public static class ServantIdentityUtility { public static ServantIdentityDef GetIdentity(Pawn p) => p?.Identity; }
    public static class MasterCircuitUtility { public static bool HasCircuit(Pawn p) => p != null; }
    public static class MW_DefOf { public static object MW_NoblePhantasmOvercharge = new object(); }
    public class ServantQuery
    {
        public static ServantQuery Instance = new ServantQuery();
        public Pawn GetMaster(Pawn p) => p?.State.Master;
        public bool IsMaterialized(Pawn p) => p.State.PresenceState == ServantPresenceState.Materialized;
    }
}
