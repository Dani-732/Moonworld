using RimWorld;
using Verse;

namespace MoonWorld
{
    internal sealed class EnemyChallengeSession : IExposable
    {
        internal Pawn servant, master;
        internal Faction faction;
        internal Site_WarEncounter site;
        internal int expiresAtTickAbs;
        internal bool onMap;
        public EnemyChallengeSession() { }

        internal EnemyChallengeSession(Pawn servant, Site_WarEncounter site)
        {
            this.servant = servant; this.site = site;
            master = ServantQuery.Instance.GetMaster(servant);
            faction = servant.Faction;
            expiresAtTickAbs = GenTicks.TicksAbs + 60000;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref servant, "servant");
            Scribe_References.Look(ref master, "master");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref site, "site");
            Scribe_Values.Look(ref expiresAtTickAbs, "expiresAtTickAbs", 0);
            Scribe_Values.Look(ref onMap, "onMap", false);
        }
    }
}
