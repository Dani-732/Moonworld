// Host doubles model SetFaction's kind replacement and lord removal, not Unity or serialization.
using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

internal static class MembershipTests
{
    private static int passed;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        passed++;
    }
    private static Pawn Legacy(ServantPresenceState state = ServantPresenceState.Materialized)
    {
        return new Pawn { Faction = new Faction(), HostFaction = Faction.OfPlayer,
            master = new Pawn { Faction = Faction.OfPlayer }, state = state };
    }
    private static void Main()
    {
        Pawn pawn = Legacy();
        Pawn master = pawn.master;
        new Lord(new LordJob_DefendPoint()).AddPawn(pawn);
        ServantColonyMembership.Initialize(pawn);
        Check(pawn.Faction == Faction.OfPlayer && pawn.HostFaction == null, "legacy guest joins player faction");
        Check(pawn.kind == "Servant" && pawn.prana == 73, "kind and need survive faction component rebuild");
        Check(pawn.master == master && pawn.state == ServantPresenceState.Materialized, "contract and state preserved");
        Check(pawn.lord == null && pawn.workSettings.Initialized, "legacy lord removed and work initialized");
        pawn.workSettings.priority = 1;
        new Lord(new LordJob_LoadAndEnterTransporters()).AddPawn(pawn);
        ServantColonyMembership.Initialize(pawn);
        Check(pawn.factionChanges == 1 && pawn.workSettings.priority == 1, "repeat does not reset priorities or faction");
        Check(pawn.lord != null, "repeat preserves current transporter lord");
        foreach (ServantPresenceState state in new[] { ServantPresenceState.VoluntarySpirit, ServantPresenceState.DefeatedSpirit })
        {
            pawn = Legacy(state);
            ServantColonyMembership.Initialize(pawn);
            Check(pawn.Faction == Faction.OfPlayer && pawn.state == state, "spirit identity migrates without materializing: " + state);
        }
        foreach (object job in new object[] { new LordJob_FormAndSendCaravan(), new LordJob_LoadAndEnterTransporters() })
        {
            pawn = Legacy();
            Lord lord = new Lord(job);
            lord.AddPawn(pawn);
            ServantColonyMembership.Initialize(pawn);
            Check(pawn.lord == lord && lord.ownedPawns.Contains(pawn), "legacy departure lord restored: " + job.GetType().Name);
        }
        foreach (Action<Pawn> exclude in new Action<Pawn>[] {
            p => p.master = null, p => p.kind = "Ordinary", p => p.Dead = true,
            p => p.Destroyed = true, p => p.IsPrisoner = true, p => p.IsSlave = true,
            p => p.master.Dead = true, p => p.master.Faction = new Faction(),
            p => p.state = ServantPresenceState.Annihilated, p => p.HostFaction = null })
        {
            pawn = Legacy();
            exclude(pawn);
            ServantColonyMembership.Initialize(pawn);
            Check(pawn.factionChanges == 0, "ineligible pawn left untouched " + passed);
        }
        pawn = Legacy();
        pawn.HostFaction = null;
        ServantColonyMembership.Initialize(pawn, true);
        Check(pawn.Faction == Faction.OfPlayer, "explicit new contract can join without old guest status");
        pawn = Legacy();
        pawn.throwDuringFactionChange = true;
        try { ServantColonyMembership.Initialize(pawn); } catch (InvalidOperationException) { }
        Check(ServantColonyMembership.JoiningServant == null, "exception clears scoped kind guard");
        Check(Harmony_ServantColonyMembership_KeepKind.Prefix(new Pawn()), "normal kind changes remain available");
        pawn = Legacy();
        PawnsFinder.AllMapsAndWorld_Alive.Add(pawn);
        ServantColonyMembership.ReconcileLoadedGame();
        Check(pawn.Faction == Faction.OfPlayer && pawn.reconciled, "unspawned world pawn included in load migration");
        foreach (ServantPresenceState state in new[] { ServantPresenceState.VoluntarySpirit, ServantPresenceState.DefeatedSpirit })
        {
            pawn = Legacy(state);
            bool canOrder = true;
            Harmony_SpiritForm_PlayerOrders.Postfix(pawn, ref canOrder);
            var drafter = new Pawn_DraftController { pawn = pawn };
            Check(!canOrder && !Harmony_SpiritForm_Drafting.Prefix(drafter, true), "spirit rejects orders and drafting: " + state);
            Check(Harmony_SpiritForm_Drafting.Prefix(drafter, false), "spirit can undraft: " + state);
            bool visible = true;
            Harmony_SpiritForm_DraftGizmo.Postfix(drafter, ref visible);
            Check(!visible, "spirit hides draft command: " + state);
        }
        foreach (string kind in new[] { "Ordinary", "Servant" })
        {
            pawn = new Pawn { kind = kind };
            bool canOrder = true;
            Harmony_SpiritForm_PlayerOrders.Postfix(pawn, ref canOrder);
            Check(canOrder && Harmony_SpiritForm_Drafting.Prefix(new Pawn_DraftController { pawn = pawn }, true), "materialized orders unchanged: " + kind);
            canOrder = false;
            Harmony_SpiritForm_PlayerOrders.Postfix(pawn, ref canOrder);
            Check(!canOrder, "vanilla rejection preserved: " + kind);
        }
        Console.WriteLine(passed + " membership scenarios passed; real save/load and Harmony require in-game testing.");
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
}
namespace Verse
{
    public sealed class Pawn
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, throwDuringFactionChange, reconciled;
        public string kind = "Servant";
        public string LabelShortCap => kind;
        public int prana = 73, factionChanges;
        public Pawn master;
        public ServantPresenceState state;
        public Faction Faction, HostFaction;
        public Lord lord;
        public GuestTracker guest;
        public WorkSettings workSettings = new WorkSettings();
        public Pawn() { guest = new GuestTracker(this); }
        public Lord GetLord() { return lord; }
        public bool CanTakeOrder => true;
        public void ChangeKind() { if (Harmony_ServantColonyMembership_KeepKind.Prefix(this)) kind = "Colonist"; }
        public void SetFaction(Faction value)
        {
            if (throwDuringFactionChange) throw new InvalidOperationException();
            factionChanges++;
            ChangeKind();
            if (kind != "Servant") prana = 0;
            Faction = value;
            HostFaction = null;
            lord?.Notify_PawnLost(this, PawnLostCondition.ChangedFaction);
            workSettings.Initialized = true;
            workSettings.priority = 3;
        }
    }
    public sealed class GuestTracker
    {
        private readonly Pawn pawn;
        public GuestTracker(Pawn pawn) { this.pawn = pawn; }
        public void SetGuestStatus(Faction faction) { pawn.HostFaction = faction; }
    }
    public sealed class WorkSettings
    {
        public bool Initialized;
        public int priority;
        public void EnableAndInitializeIfNotAlreadyInitialized() { if (!Initialized) { Initialized = true; priority = 3; } }
    }
}
namespace Verse.AI.Group
{
    public enum PawnLostCondition { ForcedToJoinOtherLord, ChangedFaction }
    public sealed class Lord
    {
        public object LordJob;
        public List<Pawn> ownedPawns = new List<Pawn>();
        public Lord(object job) { LordJob = job; }
        public void AddPawn(Pawn pawn) { ownedPawns.Add(pawn); pawn.lord = this; }
        public void Notify_PawnLost(Pawn pawn, PawnLostCondition condition) { ownedPawns.Remove(pawn); pawn.lord = null; }
    }
}
namespace RimWorld
{
    public sealed class Pawn_DraftController { public Pawn pawn; }
    public sealed class ColonistBarColonistDrawer { public void DrawColonist() { } }
    public sealed class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public sealed class LordJob_DefendPoint { }
    public sealed class LordJob_LoadAndEnterTransporters { }
    public static class PawnsFinder { public static readonly List<Pawn> AllMapsAndWorld_Alive = new List<Pawn>(); }
}
namespace RimWorld.Planet { public sealed class LordJob_FormAndSendCaravan { } }
namespace MoonWorld
{
    public sealed class LordJob_ServantGuest { }
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public struct ServantSnapshot { public Pawn master; public ServantPresenceState presenceState; }
    public sealed class ServantQuery
    {
        public static readonly ServantQuery Instance = new ServantQuery();
        public bool IsServant(Pawn pawn) { return pawn.kind == "Servant"; }
        public bool IsSpirit(Pawn pawn) { return IsServant(pawn) && (pawn.state == ServantPresenceState.VoluntarySpirit || pawn.state == ServantPresenceState.DefeatedSpirit); }
        public bool TryGetSnapshot(Pawn pawn, out ServantSnapshot snapshot)
        {
            snapshot = new ServantSnapshot { master = pawn.master, presenceState = pawn.state };
            return IsServant(pawn);
        }
    }
    public static class ServantPresenceEffects { public static void Reconcile(Pawn pawn) { pawn.reconciled = true; } }
}
namespace UnityEngine
{
    public struct Rect { public Rect ContractedBy(float margin) { return this; } }
    public struct Color { public Color(float r, float g, float b, float a) { } public static Color white; }
    public static class GUI { public static Color color; }
}
namespace Verse
{
    public static class Widgets { public static void DrawBoxSolid(UnityEngine.Rect rect, UnityEngine.Color color) { } }
    public static class TooltipHandler { public static void TipRegion(UnityEngine.Rect rect, string text) { } }
}
