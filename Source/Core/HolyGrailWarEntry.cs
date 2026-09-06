using Verse;

namespace MoonWorld
{
    // This entry belongs to the current war invitation, not to a pawn's lifetime.
    public sealed class HolyGrailWarEntry : IExposable
    {
        private Pawn designatedMaster;
        private bool regularSummonUsed;
        private ServantIdentityDef playerIdentity;
        private ServantIdentityDef enemyIdentity;
        private Pawn enemyMaster;
        private Pawn enemyServant;
        private bool enemyDeployed;
        private int enemyRestStartTickAbs = -1;

        public Pawn DesignatedMaster => designatedMaster;
        public bool RegularSummonUsed => regularSummonUsed;
        public ServantIdentityDef PlayerIdentity => playerIdentity;
        public ServantIdentityDef EnemyIdentity => enemyIdentity;
        public Pawn EnemyMaster => enemyMaster;
        public Pawn EnemyServant => enemyServant;
        public bool EnemyDeployed => enemyDeployed;
        public int EnemyRestStartTickAbs => enemyRestStartTickAbs;
        public bool EnemyEliminated => enemyDeployed &&
            (enemyMaster == null || enemyMaster.Dead || enemyMaster.Destroyed
             || enemyServant == null || enemyServant.Dead || enemyServant.Destroyed);

        public HolyGrailWarEntry() { }

        internal HolyGrailWarEntry(Pawn master, bool alreadySummoned = false)
        {
            designatedMaster = master;
            regularSummonUsed = alreadySummoned;
        }

        internal void ConsumeRegularSummon()
        {
            regularSummonUsed = true;
        }

        internal void SetParticipants(ServantIdentityDef player, ServantIdentityDef enemy)
        {
            playerIdentity = player;
            enemyIdentity = enemy;
        }

        internal void RecordEnemyDeployment(Pawn master, Pawn servant)
        {
            enemyMaster = master;
            enemyServant = servant;
            enemyDeployed = true;
            enemyRestStartTickAbs = -1;
        }

        internal void RecordEnemyDeparture(Pawn servant)
        {
            if (servant == enemyServant && enemyRestStartTickAbs < 0)
                enemyRestStartTickAbs = GenTicks.TicksAbs;
        }

        internal void ClearEnemyRestStart()
        {
            enemyRestStartTickAbs = -1;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref designatedMaster, "designatedMaster");
            Scribe_Values.Look(ref regularSummonUsed, "regularSummonUsed", false);
            Scribe_Defs.Look(ref playerIdentity, "playerIdentity");
            Scribe_Defs.Look(ref enemyIdentity, "enemyIdentity");
            Scribe_References.Look(ref enemyMaster, "enemyMaster");
            Scribe_References.Look(ref enemyServant, "enemyServant");
            Scribe_Values.Look(ref enemyDeployed, "enemyDeployed", false);
            Scribe_Values.Look(ref enemyRestStartTickAbs, "enemyRestStartTickAbs", -1);
        }
    }
}
