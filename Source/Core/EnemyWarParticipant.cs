using Verse;

namespace MoonWorld
{
    // One enemy's authoritative runtime state; owned only by the current war entry.
    public sealed class EnemyWarParticipant : IExposable
    {
        private ServantIdentityDef identity;
        private HolyGrailWarClassDef seat;
        private Pawn master, servant;
        private bool prepared, deployed;
        private int restStartTickAbs = -1;
        public ServantIdentityDef EnemyIdentity => identity;
        public HolyGrailWarClassDef Seat => seat ?? HolyGrailWarClassDef.For(identity);
        public Pawn EnemyMaster => master;
        public Pawn EnemyServant => servant;
        public bool EnemyPrepared => prepared;
        public bool EnemyDeployed => deployed;
        public bool HasEnemyParticipants => prepared || deployed;
        public int EnemyRestStartTickAbs => restStartTickAbs;
        public bool EnemyEliminated => HasEnemyParticipants &&
            (master == null || master.Dead || master.Destroyed || servant == null || servant.Dead || servant.Destroyed
             || servant.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated);
        public EnemyWarParticipant() { }
        internal EnemyWarParticipant(ServantIdentityDef identity, Pawn master, Pawn servant,
            bool prepared = true, bool deployed = false, int restStart = -1)
        {
            this.identity = identity; seat = HolyGrailWarClassDef.For(identity);
            this.master = master; this.servant = servant; this.prepared = prepared;
            this.deployed = deployed; restStartTickAbs = restStart;
        }
        internal void RecordEnemyDeployment(Pawn owner, Pawn pawn)
        {
            if (owner != master || pawn != servant) throw new System.InvalidOperationException("出击参与者与阵营记录不符。");
            deployed = true; restStartTickAbs = -1;
        }
        internal void RecordEnemyDeparture(Pawn pawn)
        { if (pawn == servant && restStartTickAbs < 0) restStartTickAbs = GenTicks.TicksAbs; }
        internal void MarkPrepared() { prepared = true; }
        public void ExposeData()
        {
            Scribe_Defs.Look(ref identity, "identity"); Scribe_Defs.Look(ref seat, "seat");
            Scribe_References.Look(ref master, "master"); Scribe_References.Look(ref servant, "servant");
            Scribe_Values.Look(ref prepared, "prepared", false); Scribe_Values.Look(ref deployed, "deployed", false);
            Scribe_Values.Look(ref restStartTickAbs, "restStartTickAbs", -1);
        }
    }
}
