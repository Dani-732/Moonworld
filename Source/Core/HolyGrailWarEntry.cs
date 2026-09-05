using Verse;

namespace MoonWorld
{
    // This entry belongs to the current war invitation, not to a pawn's lifetime.
    public sealed class HolyGrailWarEntry : IExposable
    {
        private Pawn designatedMaster;
        private bool regularSummonUsed;

        public Pawn DesignatedMaster => designatedMaster;
        public bool RegularSummonUsed => regularSummonUsed;

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

        public void ExposeData()
        {
            Scribe_References.Look(ref designatedMaster, "designatedMaster");
            Scribe_Values.Look(ref regularSummonUsed, "regularSummonUsed", false);
        }
    }
}
