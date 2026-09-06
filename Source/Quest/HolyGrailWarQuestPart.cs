using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    public sealed class HolyGrailWarFactionRecord : IExposable
    {
        private HolyGrailWarClass seat;
        private HolyGrailWarClassDef classDef;
        private Pawn master;
        private bool qualified;
        private List<Pawn> servants = new List<Pawn>();
        private List<Site_WarWorkshop> sites = new List<Site_WarWorkshop>();

        public HolyGrailWarClass Seat => seat;
        public string SeatLabel => classDef?.label ?? seat.ToString();
        public Pawn Master => master;
        public bool Qualified => qualified;
        public List<Pawn> Servants => servants;
        public List<Site_WarWorkshop> Sites => sites;

        public HolyGrailWarFactionRecord() { }
        internal HolyGrailWarFactionRecord(HolyGrailWarClass seat, Pawn master)
        { this.seat = seat; this.master = master; qualified = master != null && !master.Dead && !master.Destroyed; }

        internal void AddServant(Pawn pawn) { if (pawn != null && !servants.Contains(pawn)) servants.Add(pawn); }
        internal void AddSite(Site_WarWorkshop site) { if (site != null && !sites.Contains(site)) sites.Add(site); }
        internal void SetClass(HolyGrailWarClassDef value) { classDef = value; }
        internal void SetQualified(bool value) { qualified = value; }

        public void ExposeData()
        {
            Scribe_Values.Look(ref seat, "seat", HolyGrailWarClass.None);
            Scribe_Defs.Look(ref classDef, "classDef");
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref qualified, "qualified", false);
            Scribe_Collections.Look(ref servants, "servants", LookMode.Reference);
            Scribe_Collections.Look(ref sites, "sites", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                servants ??= new List<Pawn>();
                sites ??= new List<Site_WarWorkshop>();
            }
        }
    }

    public sealed class QuestPart_HolyGrailWar : QuestPart
    {
        private int warStartTick = -1;
        private List<HolyGrailWarFactionRecord> factions = new List<HolyGrailWarFactionRecord>();

        public int WarStartTick => warStartTick;
        public List<HolyGrailWarFactionRecord> Factions => factions;
        public override string DescriptionPart
        {
            get
            {
                var entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
                if (entry != null && quest != null && !quest.Historical) Initialize(warStartTick, entry);
                var text = new System.Text.StringBuilder("圣杯战争参战阵营：");
                foreach (var faction in factions)
                {
                    text.Append("\n").Append(faction.SeatLabel).Append("：")
                        .Append(faction.Qualified ? "参战中" : "已失去资格");
                    if (faction.Qualified && quest != null && !quest.Historical
                        && entry?.FindEnemy(faction.Master)?.WorkshopRebuildPending == true)
                        text.Append("（工坊失守，等待休整并重建）");
                }
                return text.ToString();
            }
        }

        internal void Initialize(int startTick, HolyGrailWarEntry entry)
        {
            warStartTick = startTick;
            factions.Clear();
            HolyGrailWarFactionRecord player = new HolyGrailWarFactionRecord(
                entry.PlayerIdentity?.warClass ?? HolyGrailWarClass.None, entry.DesignatedMaster);
            player.AddServant(FindPlayerServant(entry));
            player.SetClass(HolyGrailWarClassDef.For(entry.PlayerIdentity));
            factions.Add(player);
            foreach (var participant in entry.Enemies)
            {
                var enemy = new HolyGrailWarFactionRecord(participant.EnemyIdentity?.warClass ?? HolyGrailWarClass.None, participant.EnemyMaster);
                enemy.SetClass(participant.Seat);
                enemy.SetQualified(!participant.EnemyEliminated);
                enemy.AddServant(participant.EnemyServant);
                foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                    if (worldObject is Site_WarWorkshop site && site.OwnerMaster == participant.EnemyMaster) enemy.AddSite(site);
                factions.Add(enemy);
            }
        }

        private static Pawn FindPlayerServant(HolyGrailWarEntry entry)
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
                if (pawn != null && pawn.Faction == Faction.OfPlayer && ServantQuery.Instance.GetMaster(pawn) == entry.DesignatedMaster)
                    return pawn;
            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref warStartTick, "warStartTick", -1);
            Scribe_Collections.Look(ref factions, "factions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) factions ??= new List<HolyGrailWarFactionRecord>();
        }
    }
}
