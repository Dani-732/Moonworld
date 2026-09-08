using RimWorld;
using Verse;

namespace MoonWorld
{
    // Stores scheduling and participant references only; health, mana and contracts stay on the pawns.
    internal sealed class EnemyBattleSession : IExposable
    {
        internal Pawn attacker, defender, attackerMaster, defenderMaster;
        internal Faction attackerFaction, defenderFaction;
        internal Site_WarWorkshop site;
        internal int rounds, nextRoundTickAbs;
        internal bool onMap;

        public EnemyBattleSession() { }

        internal EnemyBattleSession(Pawn attacker, Pawn defender, Site_WarWorkshop site)
        {
            this.attacker = attacker; this.defender = defender; this.site = site;
            attackerMaster = ServantQuery.Instance.GetMaster(attacker);
            defenderMaster = ServantQuery.Instance.GetMaster(defender);
            attackerFaction = attacker.Faction; defenderFaction = defender.Faction;
            nextRoundTickAbs = GenTicks.TicksAbs + EnemyBattleService.RoundInterval;
        }

        internal bool Contains(Pawn pawn) => pawn != null && (pawn == attacker || pawn == defender);

        public void ExposeData()
        {
            Scribe_References.Look(ref attacker, "attacker");
            Scribe_References.Look(ref defender, "defender");
            Scribe_References.Look(ref attackerMaster, "attackerMaster");
            Scribe_References.Look(ref defenderMaster, "defenderMaster");
            Scribe_References.Look(ref attackerFaction, "attackerFaction");
            Scribe_References.Look(ref defenderFaction, "defenderFaction");
            Scribe_References.Look(ref site, "site");
            Scribe_Values.Look(ref rounds, "rounds", 0);
            Scribe_Values.Look(ref nextRoundTickAbs, "nextRoundTickAbs", 0);
            Scribe_Values.Look(ref onMap, "onMap", false);
        }
    }
}
