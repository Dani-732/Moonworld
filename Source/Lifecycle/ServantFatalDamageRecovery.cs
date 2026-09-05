using UnityEngine;
using Verse;

namespace MoonWorld
{
    public static class ServantFatalDamageRecovery
    {
        private const int SeveritySearchSteps = 12;
        private const float SafetyMargin = 0.01f;

        public static bool TryStabilize(Pawn servant, Hediff triggeringHediff)
        {
            Pawn_HealthTracker health = servant?.health;
            if (health == null || health.Dead)
            {
                return false;
            }
            if (!health.ShouldBeDead())
            {
                return true;
            }

            BodyPartRecord affectedPart = triggeringHediff?.Part;
            if (TryRestoreFatalMissingPart(servant, affectedPart) && !health.ShouldBeDead())
            {
                return true;
            }

            return triggeringHediff != null
                && !(triggeringHediff is Hediff_MissingPart)
                && health.hediffSet.hediffs.Contains(triggeringHediff)
                && TryReduceHediffToNonlethalSeverity(health, triggeringHediff);
        }

        private static bool TryRestoreFatalMissingPart(Pawn servant, BodyPartRecord affectedPart)
        {
            if (affectedPart == null)
            {
                return false;
            }

            Pawn_HealthTracker health = servant.health;
            HediffSet hediffs = health.hediffSet;
            Hediff missingPart = hediffs.GetMissingPartFor(affectedPart);
            if (missingPart == null)
            {
                return false;
            }

            BodyPartRecord corePart = servant.RaceProps.body.corePart;
            bool coreIsMissing = hediffs.GetMissingPartFor(corePart) != null;
            if (!coreIsMissing && health.ShouldBeDeadFromRequiredCapacity() == null)
            {
                return false;
            }

            health.RemoveHediff(missingPart);
            return true;
        }

        private static bool TryReduceHediffToNonlethalSeverity(
            Pawn_HealthTracker health,
            Hediff hediff)
        {
            float lethalSeverity = hediff.Severity;
            hediff.Severity = 0f;
            if (health.ShouldBeDead())
            {
                hediff.Severity = lethalSeverity;
                return false;
            }

            float safeSeverity = 0f;
            for (int i = 0; i < SeveritySearchSteps; i++)
            {
                float candidate = (safeSeverity + lethalSeverity) * 0.5f;
                hediff.Severity = candidate;
                if (health.ShouldBeDead())
                {
                    lethalSeverity = candidate;
                }
                else
                {
                    safeSeverity = candidate;
                }
            }

            hediff.Severity = Mathf.Max(0f, safeSeverity - SafetyMargin);
            return !health.ShouldBeDead();
        }
    }
}
