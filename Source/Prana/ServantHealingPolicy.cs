using Verse;

namespace MoonWorld
{
    // Defines which non-injury health conditions belong to prana-based body repair.
    internal static class ServantHealingPolicy
    {
        public static Hediff FindWorstCurableCondition(Pawn servant)
        {
            Hediff worst = null;
            float worstImpact = float.MinValue;
            foreach (Hediff hediff in servant.health.hediffSet.hediffs)
            {
                if (!IsCurableCondition(hediff))
                {
                    continue;
                }

                float impact = hediff.SummaryHealthPercentImpact;
                if (worst == null || impact > worstImpact)
                {
                    worst = hediff;
                    worstImpact = impact;
                }
            }
            return worst;
        }

        private static bool IsCurableCondition(Hediff hediff)
        {
            HediffDef def = hediff?.def;
            return def != null
                && def.isBad
                && !def.countsAsAddedPartOrImplant
                && !(hediff is Hediff_Injury)
                && !(hediff is Hediff_MissingPart)
                && def != MW_DefOf.MW_SpiritDamage
                && def != MW_DefOf.MW_PranaShortage
                && def != MW_DefOf.MW_SpiritForm;
        }
    }
}
