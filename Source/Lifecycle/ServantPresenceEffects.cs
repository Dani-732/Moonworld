using Verse;

namespace MoonWorld
{
    // Applies derived runtime effects. CompServantState remains the only state authority.
    public static class ServantPresenceEffects
    {
        public static void Reconcile(Pawn servant)
        {
            if (servant == null || servant.health == null)
            {
                return;
            }

            bool shouldBeSpirit = ServantQuery.Instance.IsSpirit(servant);
            Hediff spiritForm = servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_SpiritForm);
            if (shouldBeSpirit)
            {
                if (spiritForm == null)
                {
                    spiritForm = servant.health.AddHediff(MW_DefOf.MW_SpiritForm);
                }

                HediffComp_Invisibility invisibility = spiritForm?.TryGetComp<HediffComp_Invisibility>();
                invisibility?.BecomeInvisible(false);
                if (servant.jobs?.curJob != null && !SpiritFollowJobPolicy.IsAllowed(servant, servant.jobs.curJob))
                {
                    StopCurrentActivity(servant);
                }
                return;
            }

            if (spiritForm != null)
            {
                servant.health.RemoveHediff(spiritForm);
            }

            if (servant.jobs?.curJob?.def == MW_DefOf.MW_SpiritFollow)
            {
                StopCurrentActivity(servant);
            }

        }

        private static void StopCurrentActivity(Pawn servant)
        {
            servant.jobs?.StopAll(false, false);
            servant.pather?.StopDead();
            servant.stances?.CancelBusyStanceHard();
        }
    }
}
