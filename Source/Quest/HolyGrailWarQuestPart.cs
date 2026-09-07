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
        private Pawn originalMaster;
        private bool qualified;
        private string masterStatus, servantStatus;
        private List<Pawn> servants = new List<Pawn>();
        private List<Site_WarWorkshop> sites = new List<Site_WarWorkshop>();

        public HolyGrailWarClass Seat => seat;
        public string SeatLabel => classDef?.label ?? seat.ToString();
        public Pawn Master => master;
        public Pawn OriginalMaster => originalMaster;
        public bool Qualified => qualified;
        public List<Pawn> Servants => servants;
        public List<Site_WarWorkshop> Sites => sites;
        public string Status => masterStatus == null ? (qualified ? "参战中" : "已失去资格")
            : "御主：" + masterStatus + "；从者：" + servantStatus;

        public HolyGrailWarFactionRecord() { }
        internal HolyGrailWarFactionRecord(HolyGrailWarClass seat, Pawn master)
        { this.seat = seat; this.master = master; qualified = master != null && !master.Dead && !master.Destroyed; }

        internal void AddServant(Pawn pawn) { if (pawn != null && !servants.Contains(pawn)) servants.Add(pawn); }
        internal void AddSite(Site_WarWorkshop site) { if (site != null && !sites.Contains(site)) sites.Add(site); }
        internal void SetClass(HolyGrailWarClassDef value) { classDef = value; }
        internal void SetOriginalMaster(Pawn pawn) { originalMaster = pawn; }
        internal void SetQualified(bool value) { qualified = value; }
        internal void RefreshStatus()
        {
            qualified = CommandSpellService.HasQualification(master);
            bool alive = servants.Exists(UnboundServantService.Exists);
            bool bound = qualified && servants.Exists(p => UnboundServantService.Exists(p) && ServantQuery.Instance.GetMaster(p) == master);
            masterStatus = master == null || master.Dead || master.Destroyed ? "已死亡或失踪"
                : !qualified ? "已失去资格" : bound ? "契约有效" : "退场待重契约（资格保留）";
            if (!bound && master != null && servants.Count > 0)
            {
                var current = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.FindEnemy(master);
                if (current?.CurrentMaster == master && current.EnemyServant != servants[0])
                    masterStatus = "已转属 " + current.Seat?.label + " 阵营";
            }
            servantStatus = !alive ? "已退场" : bound ? "存续（已契约）" : "存续（失契落单）";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref seat, "seat", HolyGrailWarClass.None);
            Scribe_Defs.Look(ref classDef, "classDef");
            Scribe_References.Look(ref master, "master");
            Scribe_References.Look(ref originalMaster, "originalMaster");
            Scribe_Values.Look(ref qualified, "qualified", false);
            Scribe_Values.Look(ref masterStatus, "masterStatus", null);
            Scribe_Values.Look(ref servantStatus, "servantStatus", null);
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
                        .Append(faction.Master?.LabelShortCap ?? "无御主").Append("；").Append(faction.Status);
                    if (faction.OriginalMaster != null && faction.OriginalMaster != faction.Master)
                        text.Append("（开战御主：").Append(faction.OriginalMaster.LabelShortCap).Append("）");
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
            entry.ResolveLegacyPlayerServant();
            foreach (var participant in entry.Participants)
            {
                var enemy = new HolyGrailWarFactionRecord(participant.EnemyIdentity?.warClass ?? HolyGrailWarClass.None, participant.EnemyMaster);
                enemy.SetOriginalMaster(participant.OriginalMaster);
                enemy.SetClass(participant.Seat);
                enemy.AddServant(participant.EnemyServant);
                enemy.RefreshStatus();
                foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                    if (worldObject is Site_WarWorkshop site && site.Participant == participant) enemy.AddSite(site);
                factions.Add(enemy);
            }
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
