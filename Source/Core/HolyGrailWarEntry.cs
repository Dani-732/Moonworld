using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // This entry belongs to the current war invitation, not to a pawn's lifetime.
    public sealed class HolyGrailWarEntry : IExposable
    {
        private Pawn designatedMaster;
        private Pawn playerServant;
        private EnemyWarParticipant playerParticipant;
        private bool regularSummonUsed;
        private ServantIdentityDef playerIdentity;
        private ServantIdentityDef enemyIdentity;
        private Pawn enemyMaster;
        private Pawn enemyServant;
        private bool enemyDeployed;
        private bool enemyPrepared;
        private int enemyRestStartTickAbs = -1;
        private List<EnemyWarParticipant> enemies;
        public List<EnemyWarParticipant> Enemies
        {
            get
            {
                if (enemies == null)
                {
                    enemies = new List<EnemyWarParticipant>();
                    if (enemyPrepared || enemyDeployed)
                        enemies.Add(new EnemyWarParticipant(enemyIdentity, enemyMaster, enemyServant,
                            enemyPrepared, enemyDeployed, enemyRestStartTickAbs));
                }
                return enemies;
            }
        }
        public List<EnemyWarParticipant> Participants
        {
            get
            {
                if (playerParticipant == null && playerServant != null)
                    playerParticipant = new EnemyWarParticipant(playerIdentity, designatedMaster, playerServant);
                var result = new List<EnemyWarParticipant>(Enemies);
                if (playerParticipant != null) result.Insert(0, playerParticipant);
                return result;
            }
        }
        public bool IsPlayerParticipant(EnemyWarParticipant participant)
        {
            if (participant == null) return false;
            if (playerParticipant != null) return participant == playerParticipant;
            return playerServant != null && participant.EnemyServant == playerServant;
        }
        public EnemyWarParticipant FindEnemy(Pawn pawn)
        {
            if (pawn == null) return null;
            var participants = Participants;
            return participants.Find(e => e.EnemyServant == pawn)
                ?? participants.Find(e => e.CurrentMaster == pawn)
                ?? participants.Find(e => e.EnemyMaster == pawn)
                ?? participants.Find(e => e.OriginalMaster == pawn);
        }

        // Original and last masters are kept for history and cleanup, not as live combat targets.
        public bool IsCurrentMaster(Pawn pawn)
        {
            if (pawn == null) return false;
            foreach (EnemyWarParticipant participant in Participants)
                if (participant.CurrentMaster == pawn) return true;
            return false;
        }
        internal void SetEnemies(ServantIdentityDef player, List<EnemyWarParticipant> participants)
        { playerIdentity = player; enemies = participants; }
        private EnemyWarParticipant FirstEnemy => Enemies.Count == 0 ? null : Enemies[0];

        public Pawn DesignatedMaster => designatedMaster;
        public Pawn PlayerServant => playerServant;
        internal void RecordPlayerServant(Pawn pawn) { playerServant = pawn; playerParticipant = null; }
        internal void ResolveLegacyPlayerServant()
        {
            if (playerServant != null || designatedMaster == null) return;
            Pawn candidate = null;
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
                if (ServantQuery.Instance.IsServant(pawn) && ServantQuery.Instance.GetMaster(pawn) == designatedMaster
                    && (playerIdentity == null || ServantIdentityUtility.GetIdentity(pawn) == playerIdentity))
                {
                    if (candidate != null && candidate != pawn) return;
                    candidate = pawn;
                }
            playerServant = candidate;
        }
        public bool RegularSummonUsed => regularSummonUsed;
        public ServantIdentityDef PlayerIdentity => playerIdentity;
        // Compatibility accessors for old integrations; runtime operations resolve a participant explicitly.
        public ServantIdentityDef EnemyIdentity => FirstEnemy?.EnemyIdentity ?? enemyIdentity;
        public Pawn EnemyMaster => FirstEnemy?.EnemyMaster ?? enemyMaster;
        public Pawn EnemyServant => FirstEnemy?.EnemyServant ?? enemyServant;
        public bool EnemyDeployed => FirstEnemy?.EnemyDeployed ?? enemyDeployed;
        public bool EnemyPrepared => FirstEnemy?.EnemyPrepared ?? enemyPrepared;
        public bool HasEnemyParticipants => Enemies.Count > 0 || enemyPrepared || enemyDeployed;
        public int EnemyRestStartTickAbs => FirstEnemy?.EnemyRestStartTickAbs ?? enemyRestStartTickAbs;
        public bool EnemyEliminated => FirstEnemy?.EnemyEliminated ?? false;

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
            FindEnemy(servant)?.RecordEnemyDeployment(master, servant);
        }

        internal void RecordEnemyPreparation(Pawn master, Pawn servant)
        {
            enemyMaster = master;
            enemyServant = servant;
            enemyPrepared = true;
            if (Enemies.Count == 0) Enemies.Add(new EnemyWarParticipant(enemyIdentity, master, servant));
            FindEnemy(servant)?.MarkPrepared();
        }

        internal void RecordEnemyDeparture(Pawn servant)
        {
            if (servant == enemyServant && enemyRestStartTickAbs < 0)
                enemyRestStartTickAbs = GenTicks.TicksAbs;
            FindEnemy(servant)?.RecordEnemyDeparture(servant);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref designatedMaster, "designatedMaster");
            Scribe_References.Look(ref playerServant, "playerServant");
            Scribe_Deep.Look(ref playerParticipant, "playerParticipant");
            Scribe_Values.Look(ref regularSummonUsed, "regularSummonUsed", false);
            Scribe_Defs.Look(ref playerIdentity, "playerIdentity");
            Scribe_Defs.Look(ref enemyIdentity, "enemyIdentity");
            Scribe_References.Look(ref enemyMaster, "enemyMaster");
            Scribe_References.Look(ref enemyServant, "enemyServant");
            Scribe_Values.Look(ref enemyDeployed, "enemyDeployed", false);
            Scribe_Values.Look(ref enemyPrepared, "enemyPrepared", false);
            Scribe_Values.Look(ref enemyRestStartTickAbs, "enemyRestStartTickAbs", -1);
            Scribe_Collections.Look(ref enemies, "enemies", LookMode.Deep);
        }
    }
}
