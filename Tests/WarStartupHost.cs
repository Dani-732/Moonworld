using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;

// Host boundaries only: generation, world placement and dependency content are injected doubles.
namespace Verse
{
    public class WorldObjectDef { }
    public class Gizmo { }
    public class FloatMenuOption { }
    public interface IThingHolder { }
    public struct AcceptanceReport { public static implicit operator AcceptanceReport(string text) => new AcceptanceReport(); }
}
namespace RimWorld
{
    public class QuestScriptDef { }
    public enum QuestEndOutcome { Success, Fail }
    public enum QuestState { Ongoing, EndedSuccess, EndedFailed }
    public static class QuestScriptDefOf { public static QuestScriptDef WandererJoins = new QuestScriptDef(); }
    public class QuestPart
    {
        public Quest quest;
        public virtual string DescriptionPart => null;
        public virtual void ExposeData() { }
    }
    public class Quest
    {
        private readonly List<QuestPart> parts = new List<QuestPart>();
        public string name, description; public QuestScriptDef root; public bool hidden, hiddenInUI; public bool Accepted;
        private static int nextId = 1;
        public int id, EndCalls, Letters;
        public QuestState State;
        public bool Historical => State != QuestState.Ongoing;
        public static Quest MakeRaw() => new Quest { id = nextId++ };
        public void End(QuestEndOutcome outcome, bool sendLetter = true, bool playSound = true)
        {
            if (Historical) throw new Exception("historical quest resolved again");
            if (root == null) throw new Exception("quest cleanup requires root");
            State = outcome == QuestEndOutcome.Success ? QuestState.EndedSuccess : QuestState.EndedFailed;
            EndCalls++; if (sendLetter) Letters++;
        }
        public T AddPart<T>() where T : QuestPart, new() { var part = new T { quest = this }; parts.Add(part); return part; }
        public T GetFirstPartOfType<T>() where T : QuestPart { foreach (var part in parts) if (part is T typed) return typed; return null; }
        public void SetInitiallyAccepted() { Accepted = true; }
    }
    public class QuestManager
    {
        public readonly List<Quest> QuestsListForReading = new List<Quest>();
        public void Add(Quest quest) { if (!QuestsListForReading.Contains(quest)) QuestsListForReading.Add(quest); }
    }
    public class SitePartDef { }
    public class SitePartParams { }
}
namespace RimWorld.Planet
{
    public struct PlanetTile { public object Layer => null; }
    public class WorldObject { public virtual void Destroy() { } }
    public class Caravan { }
    public class TransportersArrivalAction { }
    public class SitePart { public SitePart(Site site, SitePartDef def, SitePartParams parms) { } }
    public class Site : WorldObject
    {
        public PlanetTile Tile; public bool Destroyed; public Faction Faction;
        public bool Spawned => Find.WorldObjects.All.Contains(this);
        public void SetFaction(Faction faction) { Faction = faction; }
        public void AddPart(SitePart part) { }
        public override void Destroy() { Destroyed = true; Find.WorldObjects.All.Remove(this); }
        public virtual void ExposeData() { }
        public virtual string GetInspectString() => "工坊";
        public virtual AcceptanceReport CanBeSettled => "不可定居";
        public virtual bool GravShipCanLandOn => true;
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
        public virtual IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan) { yield break; }
        public virtual IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods,
            Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
        public virtual IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(IEnumerable<IThingHolder> pods,
            Action<PlanetTile, TransportersArrivalAction> launchAction) { yield break; }
    }
    public class WorldObjectsHolder
    {
        public List<Site> All = new List<Site>(); public IEnumerable<WorldObject> AllWorldObjects => All; public bool FailAdd; public Action Callback;
        public void Add(Site site) { All.Add(site); Callback?.Invoke(); if (FailAdd) throw new Exception("partial site add"); }
    }
    public static class WorldObjectMaker { public static Site MakeWorldObject(WorldObjectDef def) => new Site_WarWorkshop(); }
    public static class TileFinder
    {
        public static bool Fail;
        public static bool TryFindNewSiteTile(out PlanetTile tile, PlanetTile origin, float selectLandmarkChance, object layer)
        { tile = new PlanetTile(); return !Fail; }
    }
}
namespace MoonWorld
{
    internal static class HolyGrailWarContentBridge
    {
        internal static bool Fail;
        internal static void InitializeWorldServant(Pawn pawn) { if (Fail) throw new Exception("dependency init"); }
    }
    internal static class PawnNeedAccess { internal static void EnsureNeed(Pawn pawn, object def) { } }
    public static class NoblePhantasmService { public static void EnsureAbilities(Pawn pawn) { } }
}
